using System;
using Game.Core;

public sealed class HeuristicPolicy : IBotPolicy
{
    public int PickAction(in OfferQuery q,
                          ReadOnlySpan<Game.Core.Action> acts,
                          ReadOnlySpan<float> costs,
                          ReadOnlySpan<byte> mask)
    {
        const float EPS = 1e-4f;
        float bestCost = float.PositiveInfinity;
        int bestIdx = -1;
        int bestDist = int.MaxValue;

        var bm = q.bm;

        for (int i = 0; i < acts.Length; i++)
        {
            if (i >= mask.Length || mask[i] == 0) continue;
            if (acts[i].kind == ActionKind.EndTurn) continue;
            float c = (i < costs.Length) ? costs[i] : 0f;

            int dist = DistanceToVpForAction(bm, in acts[i]);
            bool better = (c < bestCost - EPS) || (Math.Abs(c - bestCost) <= EPS && dist < bestDist);
            if (better) { bestCost = c; bestIdx = i; bestDist = dist; }
        }
        return bestIdx;
    }

    private static int DistanceToVpForAction(BoardModel bm, in Game.Core.Action a)
    {
        switch (a.kind)
        {
            case ActionKind.Move:
            case ActionKind.CaptureVP:
            case ActionKind.Create:
                return bm.DistToVictoryPoint(a.dstCell);
            case ActionKind.Shoot:
            case ActionKind.CoreDamage:
                return bm.DistToVictoryPoint(a.srcCell);
            default:
                return int.MaxValue / 4;
        }
    }
}

