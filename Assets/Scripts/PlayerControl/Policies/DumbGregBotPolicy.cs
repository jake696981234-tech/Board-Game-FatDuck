using System;
using Game.Core;

public sealed class DumbGregBotPolicy : IBotPolicy
{
    private readonly Random _rng;

    private readonly double _pctEndTurnAfterFirst;
    private readonly double _pctShootInstead;
    private readonly double _pctMoveAnother;
    private readonly double _pctMoveBuilding;
    private readonly double _pctCreateInstead;

    public DumbGregBotPolicy(
        double endTurnAfterFirstPct = 0.08,
        double shootInsteadPct = 0.12,
        double moveAnotherPct = 0.10,
        double moveBuildingPct = 0.05,
        double createInsteadPct = 0.10,
        int? seed = null)
    {
        _pctEndTurnAfterFirst = Clamp01(endTurnAfterFirstPct);
        _pctShootInstead = Clamp01(shootInsteadPct);
        _pctMoveAnother = Clamp01(moveAnotherPct);
        _pctMoveBuilding = Clamp01(moveBuildingPct);
        _pctCreateInstead = Clamp01(createInsteadPct);
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public int PickAction(in OfferQuery q,
                          ReadOnlySpan<Game.Core.Action> acts,
                          ReadOnlySpan<float> costs,
                          ReadOnlySpan<byte> mask,
                          int gameIndex,
                          byte playerId)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;

        bool firstAction = gameState.ps[playerId].actionIndexThisTurn == 0;
        int endIdx = FindEndTurn(acts);

        // ===== Tier 1: CaptureVP (gated) =====
        int capIdx = FindBestByCost(acts, costs, mask, ActionKind.CaptureVP);
        if (capIdx >= 0)
        {
            if (!firstAction && Chance(_pctEndTurnAfterFirst) && endIdx >= 0) return endIdx;
            int bestShoot = FindBestByCost(acts, costs, mask, ActionKind.Shoot);
            if (Chance(_pctShootInstead) && bestShoot >= 0) return bestShoot;
            return capIdx;
        }

        // ===== Tier 2: CoreDamage (gated) =====
        int coreIdx = FindBestByCost(acts, costs, mask, ActionKind.CoreDamage);
        if (coreIdx >= 0)
        {
            if (!firstAction && Chance(_pctEndTurnAfterFirst) && endIdx >= 0) return endIdx;
            return coreIdx;
        }

        // ===== Tier 3: Move non-building toward VP (gated) =====
        int bestMoveNB = -1, secondMoveNB = -1, bestMoveNBDelta = 0, bestMoveNBAfter = int.MaxValue;
        int bestMoveBld = -1, bestMoveBldDelta = 0;
        int bestShootIdx = -1;
        ScanMovesAndShoot(q, acts, costs, mask,
            out bestMoveNB, out secondMoveNB, out bestMoveNBDelta, out bestMoveNBAfter,
            out bestMoveBld, out bestMoveBldDelta, out bestShootIdx, gameIndex);

        if (bestMoveNB >= 0)
        {
            if (!firstAction && Chance(_pctEndTurnAfterFirst) && endIdx >= 0) return endIdx;

            if (Chance(_pctShootInstead) && bestShootIdx >= 0) return bestShootIdx;
            if (Chance(_pctMoveAnother) && secondMoveNB >= 0) return secondMoveNB;
            if (Chance(_pctMoveBuilding) && bestMoveBld >= 0) return bestMoveBld;

            // small chance to create instead (only if any create exists)
            if (Chance(_pctCreateInstead))
            {
                int createIdx = PickCreateBiased(q, acts, costs, mask, preferNonBuilding: true);
                if (createIdx >= 0) return createIdx;
            }
            return bestMoveNB;
        }

        // ===== Tier 4: Shoot (gated) =====
        bestShootIdx = FindBestByCost(acts, costs, mask, ActionKind.Shoot);
        if (bestShootIdx >= 0)
        {
            if (!firstAction && Chance(_pctEndTurnAfterFirst) && endIdx >= 0) return endIdx;
            return bestShootIdx;
        }

        // ===== Tier 5: Push (gated) =====
        int pushIdx = FindBestByCost(acts, costs, mask, ActionKind.Push);
        if (pushIdx >= 0)
        {
            if (!firstAction && Chance(_pctEndTurnAfterFirst) && endIdx >= 0) return endIdx;
            return pushIdx;
        }

        // ===== Tier 5: Create (gated) =====
        int createPick = PickCreateBiased(q, acts, costs, mask, preferNonBuilding: true);
        if (createPick >= 0)
        {
            if (!firstAction && Chance(_pctEndTurnAfterFirst) && endIdx >= 0) return endIdx;
            return createPick;
        }

        // ===== Fallback: EndTurn =====
        return endIdx >= 0 ? endIdx : -1;
    }

    // ---- Helpers ----
    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
    private bool Chance(double p) => _rng.NextDouble() < p;

    private static bool IsMasked(int i, ReadOnlySpan<byte> mask) => i >= mask.Length || mask[i] == 0;
    private static bool IsKind(in Game.Core.Action a, byte kind) => a.kind == kind;

    private static int FindEndTurn(ReadOnlySpan<Game.Core.Action> acts)
    {
        for (int i = acts.Length - 1; i >= 0; i--) if (acts[i].kind == ActionKind.EndTurn) return i;
        return -1;
    }

    private static int FindBestByCost(ReadOnlySpan<Game.Core.Action> acts,
                                      ReadOnlySpan<float> costs,
                                      ReadOnlySpan<byte> mask,
                                      byte kind)
    {
        float best = float.PositiveInfinity;
        int bestIdx = -1;
        for (int i = 0; i < acts.Length; i++)
        {
            if (IsMasked(i, mask)) continue;
            if (!IsKind(in acts[i], kind)) continue;
            float c = (i < costs.Length) ? costs[i] : 0f;
            if (c < best) { best = c; bestIdx = i; }
        }
        return bestIdx;
    }

    private static void ScanMovesAndShoot(in OfferQuery q,
                                          ReadOnlySpan<Game.Core.Action> acts,
                                          ReadOnlySpan<float> costs,
                                          ReadOnlySpan<byte> mask,
                                          out int bestMoveNB, out int secondMoveNB, out int bestMoveNBDelta, out int bestMoveNBAfter,
                                          out int bestMoveBld, out int bestMoveBldDelta,
                                          out int bestShootIdx,
                                          int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        bestMoveNB = -1; secondMoveNB = -1; bestMoveNBDelta = 0; bestMoveNBAfter = int.MaxValue;
        bestMoveBld = -1; bestMoveBldDelta = 0; bestShootIdx = -1;

        float bestShootCost = float.PositiveInfinity;


        for (int i = 0; i < acts.Length; i++)
        {
            if (IsMasked(i, mask)) continue;
            ref readonly var a = ref acts[i];

            if (a.kind == ActionKind.Shoot)
            {
                float c = (i < costs.Length) ? costs[i] : 0f;
                if (c < bestShootCost) { bestShootCost = c; bestShootIdx = i; }
                continue;
            }

            if (a.kind != ActionKind.Move) continue;

            int src = a.ActorsCellId; int dst = a.TargetCellId;
            int before = bm.DistToVictoryPoint(src);
            int after = bm.DistToVictoryPoint(dst);
            int delta = before - after;
            if (delta <= 0) continue; // only improving moves

            int pid = bm.GetCellOccupant(src);
            byte typ = bm.GetPieceType(pid);
            bool isBuilding = PieceDefinition.isBuilding[typ];

            if (!isBuilding)
            {
                // Rank by delta desc, then after asc, then cost asc
                if (delta > bestMoveNBDelta || (delta == bestMoveNBDelta && (after < bestMoveNBAfter || (after == bestMoveNBAfter && Cost(costs, i) < Cost(costs, bestMoveNB)))))
                {
                    // shift current best to second
                    if (bestMoveNB >= 0) secondMoveNB = bestMoveNB;
                    bestMoveNB = i; bestMoveNBDelta = delta; bestMoveNBAfter = after;
                }
                else if (secondMoveNB < 0)
                {
                    secondMoveNB = i; // keep a simple 2nd best; fine for our use
                }
            }
            else
            {
                if (delta > bestMoveBldDelta) { bestMoveBldDelta = delta; bestMoveBld = i; }
            }
        }
    }

    private static float Cost(ReadOnlySpan<float> costs, int i) => (i >= 0 && i < costs.Length) ? costs[i] : 0f;

    private int PickCreateBiased(in OfferQuery q,
                                 ReadOnlySpan<Game.Core.Action> acts,
                                 ReadOnlySpan<float> costs,
                                 ReadOnlySpan<byte> mask,
                                 bool preferNonBuilding)
    {
        // First try non-building creates
        int pick = PickCreateWeighted(q, acts, costs, mask, mustBeBuilding: false);
        if (pick >= 0) return pick;
        if (!preferNonBuilding)
        {
            // Allow building creates next
            pick = PickCreateWeighted(q, acts, costs, mask, mustBeBuilding: true);
            if (pick >= 0) return pick;
        }
        else
        {
            // If we prefer non-building but none exist, then consider buildings as a last resort
            pick = PickCreateWeighted(q, acts, costs, mask, mustBeBuilding: true);
            if (pick >= 0) return pick;
        }
        return -1;
    }

    private int PickCreateWeighted(in OfferQuery q,
                                   ReadOnlySpan<Game.Core.Action> acts,
                                   ReadOnlySpan<float> costs,
                                   ReadOnlySpan<byte> mask,
                                   bool mustBeBuilding)
    {
        // Compute total weight
        double total = 0;
        for (int i = 0; i < acts.Length; i++)
        {
            if (IsMasked(i, mask)) continue;
            ref readonly var a = ref acts[i];
            if (a.kind != ActionKind.Create) continue;
            bool isB = PieceDefinition.isBuilding[a.pieceType];
            if (mustBeBuilding != isB) continue;
            float c = Cost(costs, i);
            double w = 1.0 / (1.0 + Math.Max(0.0, c));
            total += w;
        }
        if (total <= 0) return -1;
        double r = _rng.NextDouble() * total;
        for (int i = 0; i < acts.Length; i++)
        {
            if (IsMasked(i, mask)) continue;
            ref readonly var a = ref acts[i];
            if (a.kind != ActionKind.Create) continue;
            bool isB = PieceDefinition.isBuilding[a.pieceType];
            if (mustBeBuilding != isB) continue;
            float c = Cost(costs, i);
            double w = 1.0 / (1.0 + Math.Max(0.0, c));
            r -= w;
            if (r <= 0) return i;
        }
        return -1;
    }
}
