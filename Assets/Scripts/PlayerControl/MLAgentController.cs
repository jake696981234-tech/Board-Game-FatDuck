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

public class MLAgentController : Agent
{
    [Header("Identity")]
    [SerializeField] private byte playerId = 0; // 0..3

    // Immutable config / shared systems (assigned by bootstrapper)
    private GameConfigHub _hub;
    public GameState _gs;
    private BoardModel _bm;
    private PlayerAgent _pa;        // reused for obs + offer build bridge

    private int gameIndex;


    // Offer buffers (capacity = hub.agent.maxOffersToConsider)
    public Game.Core.Action[] _offers;
    private float[] _quoted;
    public byte[] _mask;

    // Observation buffer (21 + 12*obs_maxCells)
    private float[] _obs;

    // Cached offer slice length for this decision
    public int _emitCount;

    // Rewards tuning (set by bootstrapper)
    public struct RewardsTuning
    {
        public float rewardWin, rewardLoss, rewardDraw, rewardCaptureVP, rewardCoreDamage;
        public float moveTowardVpScale, costPenaltyScale, stepPenalty, endTurnPenalty;
    }
    private RewardsTuning _rt;
    private bool _episodeTerminated;
    private Action<byte> _onEpisodeBegin;

    // -------------------- Bootstrap wiring --------------------

    // Call this from your GameBootstrapper after systems are constructed.
    public void Init(
                     GameState gs,
                     BoardModel bm,
                     PlayerAgent pa,
                     byte myPlayerId,
                     in Config.MLRewardsAuthoring rewards,
                     int theGameIndex)
    {
        gameIndex = theGameIndex;
        _hub = GameBootstrapper.hub;
        _gs = gs;
        _bm = bm;
        _pa = pa;
        playerId = myPlayerId;

        _rt = new RewardsTuning
        {
            rewardWin = rewards.rewardWin,
            rewardLoss = rewards.rewardLoss,
            rewardDraw = rewards.rewardDraw,
            rewardCaptureVP = rewards.rewardCaptureVP,
            rewardCoreDamage = rewards.rewardCoreDamage,
            moveTowardVpScale = rewards.moveTowardVpScale,
            costPenaltyScale = rewards.costPenaltyScale,
            stepPenalty = rewards.stepPenalty,
            endTurnPenalty = rewards.endTurnPenalty
        };

        int cap = Math.Max(1, _hub.agent.maxOffersToConsider);
        _offers = new Game.Core.Action[cap];
        _quoted = new float[cap];
        _mask = new byte[cap];

        _obs = new float[21 + 12 * _hub.obs_maxCells];
    }

    public void SetEpisodeBeginCallback(Action<byte> callback) => _onEpisodeBegin = callback;

    // -------------------- ML-Agents lifecycle --------------------

    public override void OnEpisodeBegin()
    {
        _episodeTerminated = false;
        _onEpisodeBegin?.Invoke(playerId);
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
            int cap = _offers.Length;
            for (int i = 0; i < cap; i++) actionMask.SetActionEnabled(0, i, false);
            _emitCount = 0;
            return;
        }

        // Build offers for current player and apply masks
        _emitCount = BuildOffersForCurrentPlayer(); // fills _actions/_quoted/_mask, returns n = min(total, cap)
        int capBranch = _offers.Length;

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
        if (_episodeTerminated) return;
        // Only act on our turn
        if (_gs.CurrentPlayerId != playerId) return;

        int idx = 0;
        var d = actions.DiscreteActions;
        if (d.Length > 0) idx = d[0];

        // Validate index against current emitted prefix and mask
        if (_emitCount <= 0 || idx < 0 || idx >= _emitCount || _mask[idx] == 0)
        {
            // Fallback: try to find EndTurn; else no-op
            int endIdx = FindEndTurn(_offers.AsSpan(0, Math.Max(0, _emitCount)));
            if (endIdx >= 0) idx = endIdx; else return;
        }

        // Precompute shaping components before applying the action
        var aChosen = _offers[idx];
        int distBefore = DistanceBefore(ref aChosen);
        int distAfterPlanned = DistanceAfter(ref aChosen);
        float normalizedCost = 0f;
        if (idx < _quoted.Length)
            normalizedCost = Mathf.Clamp01(_quoted[idx] / Mathf.Max(1f, _hub.cap_maxBudget));

        // Step the game
        bool ok = _gs.Perform(in aChosen, _offers);
        if (!ok) return; // illegal due to race (rare), skip reward

        // --- Minimal reward shaping (optional; safe defaults) ---
        float r = 0f;
        // Step penalty
        r += _rt.stepPenalty;
        // EndTurn penalty
        if (aChosen.kind == ActionKind.EndTurn) r += _rt.endTurnPenalty;
        // Move-toward-VP shaping based on planned geometry
        if (_rt.moveTowardVpScale != 0f && distBefore < int.MaxValue / 8 && distAfterPlanned < int.MaxValue / 8)
        {
            int delta = distBefore - distAfterPlanned;
            r += _rt.moveTowardVpScale * delta;
        }
        // Cost penalty
        if (_rt.costPenaltyScale > 0f)
            r += -_rt.costPenaltyScale * normalizedCost;
        // Kind-specific rewards (simple proxy)
        if (aChosen.kind == ActionKind.CaptureVP) r += _rt.rewardCaptureVP;
        if (aChosen.kind == ActionKind.CoreDamage) r += _rt.rewardCoreDamage;

        AddReward(r);

        // Terminal handling
        // Terminal handling moved to controller broadcast so all seats end together.
    }

    public void ApplyTerminal(byte winner)
    {
        if (_episodeTerminated) return;

        if (winner == 255 || winner >= _hub.player_count)
        {
            AddReward(_rt.rewardDraw);
        }
        else if (winner == playerId)
        {
            AddReward(_rt.rewardWin);
        }
        else
        {
            AddReward(_rt.rewardLoss);
        }

        _episodeTerminated = true;
        EndEpisode();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DistanceBefore(ref Game.Core.Action a)
    {
        switch (a.kind)
        {
            case ActionKind.Move:
                return _bm.DistToVictoryPoint(a.ActorsCellId);
            case ActionKind.CaptureVP:
            case ActionKind.Create:
            case ActionKind.GroupBuild:
            case ActionKind.Upgrade:
                return _bm.DistToVictoryPoint(a.TargetCellId);
            case ActionKind.Shoot:
            case ActionKind.CoreDamage:
            case ActionKind.Push:
                return _bm.DistToVictoryPoint(a.ActorsCellId);
            default:
                return int.MaxValue / 4;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DistanceAfter(ref Game.Core.Action a)
    {
        switch (a.kind)
        {
            case ActionKind.Move:
            case ActionKind.CaptureVP:
            case ActionKind.Create:
            case ActionKind.GroupBuild:
            case ActionKind.Upgrade:
            case ActionKind.Spawner:
                return _bm.DistToVictoryPoint(a.TargetCellId);
            case ActionKind.Shoot:
            case ActionKind.CoreDamage:
            case ActionKind.Push:
            case ActionKind.Launcher:
                return _bm.DistToVictoryPoint(a.ActorsCellId);
            default:
                return int.MaxValue / 4;
        }
    }

    // Optional: Let the editor "Heuristic" mode act like the cheap policy
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discrete = actionsOut.DiscreteActions;
        // Build offers to align with masking; pick cheapest affordable non-EndTurn
        int n = BuildOffersForCurrentPlayer();
        int pick = PickCheapestAffordableNonEndTurn(_offers.AsSpan(0, n),
                                                    _quoted.AsSpan(0, Math.Min(n, _quoted.Length)),
                                                    _mask.AsSpan(0, Math.Min(n, _mask.Length)));
        if (pick < 0) pick = FindEndTurn(_offers.AsSpan(0, n));
        if (pick < 0) pick = 0; // fallback safe index
        discrete[0] = pick;
    }

    private void FixedUpdate()
    {
        if (_episodeTerminated) return;
        // Drive decisions only on this player's turn.
        if (_gs != null && _gs.CurrentPlayerId == playerId)
        {
            RequestDecision();
        }
    }

    // -------------------- Internals --------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int BuildOffersForCurrentPlayer()
    {
        // Build OfferQuery: (bm, pcs, ps, playerId, cost)
        // GameActions.GetMultiCreateState(out bool mcActive, out byte mcType, out bool mcBorder, out int mcRemaining, out int[] mcCells, out int mcCellCount, gameIndex);
        var query = new OfferQuery(playerId, _gs.PieceLimitEnabled, _gs.pieceLimitPerPlayer
            /*mcActive, mcType, mcBorder, mcRemaining, mcCells, mcCellCount */);

        var acts = _offers.AsSpan();
        var costs = _quoted.AsSpan();
        var mask = _mask.AsSpan();

        OfferBuild offerBuild;
        offerBuild.query = query;
        offerBuild.outActions = acts;
        offerBuild.outCosts = costs;
        offerBuild.outMask = mask;
        offerBuild.gameIndex = gameIndex;
        offerBuild.write = 0;
        offerBuild.total = 0;
        offerBuild.cap = 0;

        int total = OfferProvider.BuildActionList(ref offerBuild);
        // We only allow the emitted prefix to be selectable by the policy
        return Math.Min(total, acts.Length);
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PickCheapestAffordableNonEndTurn(ReadOnlySpan<Game.Core.Action> acts,
                                                 ReadOnlySpan<float> costs,
                                                 ReadOnlySpan<byte> mask)
    {
        const float EPS = 1e-4f;
        float bestCost = float.PositiveInfinity;
        int bestIdx = -1;
        int bestDist = int.MaxValue;

        for (int i = 0; i < acts.Length; i++)
        {
            if (i >= mask.Length || mask[i] == 0) continue;
            if (acts[i].kind == ActionKind.EndTurn) continue;
            float c = (i < costs.Length) ? costs[i] : 0f;

            // Prefer lower cost; on ties, prefer reducing distance to VP
            int dist = DistanceToVpForAction(in acts[i]);
            bool better = (c < bestCost - EPS) || (Math.Abs(c - bestCost) <= EPS && dist < bestDist);
            if (better) { bestCost = c; bestIdx = i; bestDist = dist; }
        }
        return bestIdx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int DistanceToVpForAction(in Game.Core.Action a)
    {
        // Use dstCell for move-like actions; srcCell for non-move actions
        switch (a.kind)
        {
            case ActionKind.Move:
            case ActionKind.CaptureVP:
            case ActionKind.Create:
            case ActionKind.GroupBuild:
            case ActionKind.Upgrade:
                return _bm.DistToVictoryPoint(a.TargetCellId);
            case ActionKind.Shoot:
            case ActionKind.CoreDamage:
            case ActionKind.Push:
                return _bm.DistToVictoryPoint(a.ActorsCellId);
            default:
                return int.MaxValue / 4;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindEndTurn(ReadOnlySpan<Game.Core.Action> acts)
    {
        for (int i = acts.Length - 1; i >= 0; i--)
            if (acts[i].kind == ActionKind.EndTurn) return i;
        return -1;
    }
}
