// Assets/Scripts/Agents/PlayerAgent.cs
//
// Deterministic agent:
//  - Enumerates actions via OfferProvider (no RNG, epsilon = 0).
//  - Picks the cheapest affordable non-EndTurn action (tie: lowest index).
//  - Falls back to EndTurn if nothing affordable.
//  - Builds Phase A obs: length = 21 + 12 * hub.obs_maxCells, channel-major, zero-padded.
//
// Live state source of truth: GameState.
// Geometry/types: BoardModel + Pieces.
// Immutable config: GameConfigHub (agent knobs + obs constants).

using System;
using System.Runtime.CompilerServices;
using Game.Core; // Action, OfferQuery, GameState, PlayerState

public sealed class PlayerAgent
{
    // ----- Immutable match config (snapshot) -----
    private GameConfigHub _hub;                    // includes obs_maxCells, obs_maxDistance, caps, etc.
    private GameConfigHub.AgentConfig _cfg;        // maxOffersToConsider, rolloutDepth, thinkBudgetMs

    // ----- Live systems (read-only handles) -----
    private Game.Core.GameState _gs;               // reducers live here; single mutator authority
    private BoardModel _bm;
    private Pieces _pcs;
    private CostEngine _cost;
    private OfferProvider _offers;

    private byte _mySeat;

    // ----- Scratch buffers (reused) -----
    private Game.Core.Action[] _actions;
    private float[] _quotedCosts;
    private byte[] _mask;

    /// <summary>Call once from GameBootstrapper after systems are constructed.</summary>
    public void Init(in GameConfigHub hub,
                     Game.Core.GameState gs,
                     BoardModel bm,
                     Pieces pcs,
                     CostEngine cost,
                     OfferProvider offers)
    {
        _hub = hub;
        _cfg = hub.agent;
        _gs = gs;
        _bm = bm;
        _pcs = pcs;
        _cost = cost;
        _offers = offers;

        int cap = Math.Max(1, _cfg.maxOffersToConsider);   // allocate once, no mid-episode growth
        _actions     = new Game.Core.Action[cap];
        _quotedCosts = new float[cap];
        _mask        = new byte[cap];
    }
    
     public void BindSeat(byte seat) => _mySeat = seat;

    public void Tick()
    {
        if (_gs.CurrentPlayerId != _mySeat) return;
        DecideAndAct();
    }

    /// <summary>
    /// Enumerate, pick, and apply one action for the current player.
    /// Returns true if an action was performed (including EndTurn).
    /// </summary>
    public bool DecideAndAct()
    {
        // Build the query the OfferProvider expects: (bm, pcs, PlayerState snapshot, playerId, cost).
        var q = new OfferQuery(_bm, _pcs, _gs.CurrentPlayerRef, _gs.CurrentPlayerId, _cost); // :contentReference[oaicite:3]{index=3}

        var acts  = _actions.AsSpan();
        var costs = _quotedCosts.AsSpan();
        var mask  = _mask.AsSpan();

        int total = _offers.BuildActionList(in q, acts, costs, mask);
        if (total <= 0) return false;

        int n = Math.Min(total, _cfg.maxOffersToConsider);
        var actsN  = acts.Slice(0, n);
        var costsN = costs.Slice(0, Math.Min(n, costs.Length));
        var maskN  = mask.Slice(0, Math.Min(n, mask.Length));

        int chosen = PickCheapestAffordableNonEndTurn(actsN, costsN, maskN);
        if (chosen < 0) chosen = FindEndTurn(actsN);

        if (chosen < 0) return false;
        return _gs.Perform(in actsN[chosen]);

    }

    // -----------------------------------------------------------------
    // Phase A Observation Builder (channel-major, zero-padded, normalized)
    // Output length = 21 + 12 * hub.obs_maxCells
    // -----------------------------------------------------------------
    public int FillObservationsPhaseA(Span<float> obs)
    {
        const int MAX_PLAYERS = 4;
        const int C_GLOBAL = 21;
        const int C_CELL   = 12;

        int MAX_CELLS    = _hub.obs_maxCells;
        int MAX_DISTANCE = _hub.obs_maxDistance; // e.g., 2 * board_radius (or training-time constant)

        int expectedLen = C_GLOBAL + C_CELL * MAX_CELLS;
        if (obs.Length < expectedLen) return 0;

        int w = 0;

        // --- Globals (21) ---
        // 1..4 current player one-hot
        for (int p = 0; p < MAX_PLAYERS; p++) obs[w++] = (p == _gs.CurrentPlayerId) ? 1f : 0f;

        // 5..8 budgets per player (normalize by cap_maxBudget)
        for (int p = 0; p < 4; p++)
        {
            float b = _gs.GetBudget((byte)p);
            obs[w++] = Safe01(b, _hub.cap_maxBudget);
        }


        // 9..12 core HP per player (normalize by cap_maxCoreHealth)
        for (byte p = 0; p < MAX_PLAYERS; p++)
        {
            int hp = _gs.GetCoreHealth(p);
            obs[w++] = Safe01(hp, _hub.cap_maxCoreHealth);
        }

        // 13 rounds remaining / TOTAL_ROUNDS
        obs[w++] = Safe01(_gs.RoundsLeft, _hub.match_numberOfRounds);

        // 14 center VP remaining / VP_PER_ROUND
        obs[w++] = Safe01(_gs.GetCenterVP(), _hub.match_startCenterVP);

        // 15 action index (current player) / cap_maxActionsPerTurn
        obs[w++] = Safe01(_gs.CurrentActionIndex, _hub.cap_maxActionsPerTurn);

        // 16 didCaptureVP flag (current player)
        obs[w++] = _gs.CurrentDidCaptureVP ? 1f : 0f;

        // 17 didCoreDamage flag (current player)
        obs[w++] = _gs.CurrentDidCoreDamage ? 1f : 0f;

        // 18..21 VP totals per player / MAX_VP_TOTAL
        int MAX_VP_TOTAL = _hub.match_numberOfRounds * _hub.match_startCenterVP;
        for (byte p = 0; p < MAX_PLAYERS; p++) obs[w++] = Safe01(_gs.GetVP(p), MAX_VP_TOTAL);

        // --- Per-cell channels (12), channel-major, zero-padded to MAX_CELLS ---
        int cellCount = _bm.GetCellCount();
        int vpCell    = _bm.GetVictoryPointCellId();
        int myCore    = _bm.GetPlayerCoreCellId(_gs.CurrentPlayerId);

        // ch0: occupancy
        for (int id = 0; id < MAX_CELLS; id++)
        {
            if (id >= cellCount) { obs[w++] = 0f; continue; }
            int pid = BM_PieceAt(id);
            obs[w++] = (pid >= 0) ? 1f : 0f;
        }

        // ch1..4: owner one-hot for players 0..3 (0 if empty)
        for (byte owner = 0; owner < MAX_PLAYERS; owner++)
        {
            for (int id = 0; id < MAX_CELLS; id++)
            {
                if (id >= cellCount) { obs[w++] = 0f; continue; }
                int pid = BM_PieceAt(id);
                if (pid < 0) { obs[w++] = 0f; continue; }
                obs[w++] = (_bm.GetPieceOwner(pid) == owner) ? 1f : 0f;
            }
        }

        // ch5: type / 255 (0 if empty)
        for (int id = 0; id < MAX_CELLS; id++)
        {
            if (id >= cellCount) { obs[w++] = 0f; continue; }
            int pid = BM_PieceAt(id);
            byte typ = (pid >= 0) ? _bm.GetPieceType(pid) : (byte)0;
            obs[w++] = typ / 255f;
        }

        // ch6: hp / maxHp (0 if empty)
        for (int id = 0; id < MAX_CELLS; id++)
        {
            if (id >= cellCount) { obs[w++] = 0f; continue; }
            int pid = BM_PieceAt(id);
            if (pid < 0) { obs[w++] = 0f; continue; }
            byte typ = _bm.GetPieceType(pid);
            int  hp  = BM_PieceHP(pid);
            int  mh  = _pcs.maxHPByType[typ];
            obs[w++] = Safe01(hp, mh);
        }

        // ch7: isBuilding
        for (int id = 0; id < MAX_CELLS; id++)
        {
            if (id >= cellCount) { obs[w++] = 0f; continue; }
            int pid = BM_PieceAt(id);
            if (pid < 0) { obs[w++] = 0f; continue; }
            byte typ = _bm.GetPieceType(pid);
            obs[w++] = _pcs.IsBuilding(typ) ? 1f : 0f;
        }

        // ch8: is VP cell
        for (int id = 0; id < MAX_CELLS; id++)
        {
            if (id >= cellCount) { obs[w++] = 0f; continue; }
            obs[w++] = (id == vpCell) ? 1f : 0f;
        }

        // ch9: is own core
        for (int id = 0; id < MAX_CELLS; id++)
        {
            if (id >= cellCount) { obs[w++] = 0f; continue; }
            obs[w++] = (id == myCore) ? 1f : 0f;
        }

        // ch10: is enemy core (any)
        for (int id = 0; id < MAX_CELLS; id++)
        {
            if (id >= cellCount) { obs[w++] = 0f; continue; }
            bool enemy = false;
            for (byte p = 0; p < MAX_PLAYERS; p++)
            {
                if (p == _gs.CurrentPlayerId) continue;
                if (id == _bm.GetPlayerCoreCellId(p)) { enemy = true; break; }
            }
            obs[w++] = enemy ? 1f : 0f;
        }

        // ch11: distance to own core / MAX_DISTANCE
        for (int id = 0; id < MAX_CELLS; id++)
        {
            if (id >= cellCount) { obs[w++] = 0f; continue; }
            int d = BM_Distance(myCore, id);
            obs[w++] = Safe01(d, MAX_DISTANCE);
        }

        return w; // should equal expectedLen
    }

    // =================================================================
    // Internals
    // =================================================================

    private int PickCheapestAffordableNonEndTurn(ReadOnlySpan<Game.Core.Action> acts,
                                             ReadOnlySpan<float> costs,
                                             ReadOnlySpan<byte> mask)
    {
        float best = float.PositiveInfinity;
        int bestIdx = -1;
        for (int i = 0; i < acts.Length; i++)
        {
            if (i >= mask.Length || mask[i] == 0) continue;
            if (acts[i].kind == ActionKind.EndTurn) continue;
            float c = (i < costs.Length) ? costs[i] : 0f;
            if (c < best) { best = c; bestIdx = i; }
        }
        return bestIdx;
    }


    private int FindEndTurn(ReadOnlySpan<Game.Core.Action> acts)
    {
        for (int i = acts.Length - 1; i >= 0; i--)
            if (acts[i].kind == ActionKind.EndTurn) return i;
        return -1;
    }


    // --- BoardModel compatibility shims (name wrappers) ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int BM_PieceAt(int cellId) => _bm.GetCellOccupant(cellId);   // −1 if empty

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int BM_PieceHP(int pieceId) => _bm.PieceHP(pieceId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int BM_Distance(int a, int b) => _bm.Distance(a, b);


    // --- Normalization helpers ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Safe01(float num, float den)
    {
        if (den <= 0f) return 0f;
        if (num <= 0f) return 0f;
        if (num >= den) return 1f;
        return num / den;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Safe01(int num, int den)
    {
        if (den <= 0) return 0f;
        if (num <= 0) return 0f;
        if (num >= den) return 1f;
        return (float)num / den;
    }
}
