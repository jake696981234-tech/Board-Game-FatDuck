// Assets/Scripts/Agents/MLAgentController.cs
// Unity ML-Agents bridge (one instance per player).
// - Observations: calls PlayerAgent.FillObservationsPhaseA
// - Actions: single discrete branch (index into OfferProvider-emitted actions)
// - Masks: from OfferProvider mask[] (affordability/once-per-turn caps)
// - Step: calls GameState.Perform(in Action)
// - Rewards: minimal shaping + terminal

using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Game.Core; // Action, OfferQuery, GameState, PlayerState

public sealed class MLAgentController : Agent
{
    [Header("Identity")]
    [SerializeField] private byte playerId = 0; // 0..3

    // Immutable config / shared systems (assigned by bootstrapper)
    private GameConfigHub _hub;
    private GameState     _gs;
    private BoardModel    _bm;
    private Pieces        _pcs;
    private CostEngine    _cost;      // can be null in structural-only runs
    private OfferProvider _offers;
    private PlayerAgent   _pa;        // reused for obs + offer build bridge

    // Offer buffers (capacity = hub.agent.maxOffersToConsider)
    private Game.Core.Action[] _actions;
    private float[]            _quoted;
    private byte[]             _mask;

    // Observation buffer (21 + 12*obs_maxCells)
    private float[] _obs;

    // Cached offer slice length for this decision
    private int _emitCount;

    // -------------------- Bootstrap wiring --------------------

    // Call this from your GameBootstrapper after systems are constructed.
    public void Init(GameConfigHub hub,
                     GameState gs,
                     BoardModel bm,
                     Pieces pcs,
                     CostEngine cost,
                     OfferProvider offers,
                     PlayerAgent pa,
                     byte myPlayerId)
    {
        _hub    = hub;
        _gs     = gs;
        _bm     = bm;
        _pcs    = pcs;
        _cost   = cost;
        _offers = offers;
        _pa     = pa;
        playerId = myPlayerId;

        int cap = Math.Max(1, _hub.agent.maxOffersToConsider);
        _actions = new Game.Core.Action[cap];
        _quoted  = new float[cap];
        _mask    = new byte[cap];

        _obs = new float[21 + 12 * _hub.obs_maxCells];
    }

    // -------------------- ML-Agents lifecycle --------------------

    public override void OnEpisodeBegin()
    {
        // Your bootstrapper should reset the match externally.
        // This method can remain empty if resets are managed outside ML-Agents.
        // (If you want internal resets, call a GameState.ResetMatch(hub, bm, pcs) here.)
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (_pa == null) { sensor.AddObservation(0f); return; }

        var span = _obs.AsSpan();
        span.Clear();
        int written = _pa.FillObservationsPhaseA(span);
        // ML-Agents VectorSensor accepts incremental adds; loop avoids allocs.
        for (int i = 0; i < written; i++) sensor.AddObservation(span[i]);
    }

    // Apply action mask for the single discrete branch.
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        // If not this player's turn, mask everything so the policy can only pick 0
        if (_gs.CurrentPlayerId != playerId)
        {
            int cap = _actions.Length;
            for (int i = 0; i < cap; i++) actionMask.SetActionEnabled(0, i, false);
            _emitCount = 0;
            return;
        }

        // Build offers for current player and apply masks
        _emitCount = BuildOffersForCurrentPlayer(); // fills _actions/_quoted/_mask, returns n = min(total, cap)
        int capBranch = _actions.Length;

        // Disable indices >= _emitCount
        for (int i = _emitCount; i < capBranch; i++) actionMask.SetActionEnabled(0, i, false);

        // Disable masked-out items within emitted prefix
        for (int i = 0; i < _emitCount; i++)
        {
            if (_mask[i] == 0) actionMask.SetActionEnabled(0, i, false);
        }
        // Note: EndTurn is appended by OfferProvider; if cost prohibits
        // everything else, mask should leave EndTurn enabled.
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Only act on our turn
        if (_gs.CurrentPlayerId != playerId) return;

        int idx = 0;
        var d = actions.DiscreteActions;
        if (d.Length > 0) idx = d[0];

        // Validate index against current emitted prefix and mask
        if (_emitCount <= 0 || idx < 0 || idx >= _emitCount || _mask[idx] == 0)
        {
            // Fallback: try to find EndTurn; else no-op
            int endIdx = FindEndTurn(_actions.AsSpan(0, Math.Max(0, _emitCount)));
            if (endIdx >= 0) idx = endIdx; else return;
        }

        // Step the game
        bool ok = _gs.Perform(in _actions[idx]);
        if (!ok) return; // illegal due to race (rare), skip reward

        // --- Minimal reward shaping (optional; safe defaults) ---
        // You can expand this using deltas tracked inside GameState if available.
        float r = 0f;

        // Example quick signals (replace with your actual getters if present)
        // r += _gs.LastCaptureVPBy(playerId) ? +1.0f : 0f;
        // r += _gs.LastCoreHitBy(playerId)   ? +0.5f : 0f;
        // If you can access the quoted cost of the chosen action, add a tiny penalty:
        // r += -0.05f * Mathf.Clamp01((_quoted[idx] / Mathf.Max(1f, _hub.cap_maxBudget)));

        AddReward(r);

        // Terminal handling
        if (_gs.IsGameOver)
        {
            if (_gs.Winner == playerId) AddReward(+10f);
            else                        AddReward(-10f);
            EndEpisode();
        }
    }

    // Optional: Let the editor "Heuristic" mode act like the cheap policy
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discrete = actionsOut.DiscreteActions;
        // Build offers to align with masking; pick cheapest affordable non-EndTurn
        int n = BuildOffersForCurrentPlayer();
        int pick = PickCheapestAffordableNonEndTurn(_actions.AsSpan(0, n),
                                                    _quoted.AsSpan(0, Math.Min(n, _quoted.Length)),
                                                    _mask.AsSpan(0, Math.Min(n, _mask.Length)));
        if (pick < 0) pick = FindEndTurn(_actions.AsSpan(0, n));
        if (pick < 0) pick = 0; // fallback safe index
        discrete[0] = pick;
    }

    private void FixedUpdate()
    {
        // Drive decisions only on this player's turn.
        if (_gs != null && _gs.CurrentPlayerId == playerId)
        {
            RequestDecision();
        }
    }

    // -------------------- Internals --------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int BuildOffersForCurrentPlayer()
    {
        // Build OfferQuery: (bm, pcs, ps, playerId, cost)
        var q = new OfferQuery(_bm, _pcs, _gs.CurrentPlayerRef, playerId, _cost);

        var acts  = _actions.AsSpan();
        var costs = _quoted.AsSpan();
        var mask  = _mask.AsSpan();

        int total = _offers.BuildActionList(in q, acts, costs, mask);
        // We only allow the emitted prefix to be selectable by the policy
        return Math.Min(total, acts.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PickCheapestAffordableNonEndTurn(ReadOnlySpan<Game.Core.Action> acts,
                                                        ReadOnlySpan<float> costs,
                                                        ReadOnlySpan<byte>  mask)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindEndTurn(ReadOnlySpan<Game.Core.Action> acts)
    {
        for (int i = acts.Length - 1; i >= 0; i--)
            if (acts[i].kind == ActionKind.EndTurn) return i;
        return -1;
    }
}
