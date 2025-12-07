using System;
using System.Collections.Generic;

public static class SnapshotDiffUtil
{
    private readonly struct OT : IEquatable<OT>
    {
        public readonly int Owner;
        public readonly int Type;
        public OT(int o, int t) { Owner = o; Type = t; }
        public bool Equals(OT other) => Owner == other.Owner && Type == other.Type;
        public override bool Equals(object obj) => obj is OT k && Equals(k);
        public override int GetHashCode() => (Owner * 397) ^ Type;
    }

    public static Dictionary<(int owner, int type), int> ComputeOwnerTypeLosses(GameSnapshot before, GameSnapshot after)
    {
        var countB = CountOwnerType(before);
        var countA = CountOwnerType(after);
        var losses = new Dictionary<(int, int), int>(16);

        foreach (var kv in countB)
        {
            countA.TryGetValue(kv.Key, out var aCount);
            int lost = kv.Value - aCount;
            if (lost > 0)
                losses[(kv.Key.Owner, kv.Key.Type)] = lost;
        }
        return losses;
    }

    public static List<int> ComputeCoreDamageTargets(GameSnapshot before, GameSnapshot after)
    {
        var hits = new List<int>(2);
        if (before.coreHPByPlayer == null || after.coreHPByPlayer == null) return hits;
        int n = Math.Min(before.coreHPByPlayer.Length, after.coreHPByPlayer.Length);
        for (int p = 0; p < n; p++)
        {
            if (after.coreHPByPlayer[p] < before.coreHPByPlayer[p]) hits.Add(p);
        }
        return hits;
    }

    public static int? ChooseTargetPlayer(
        Dictionary<(int owner, int type), int> losses,
        List<int> coreHits)
    {
        // Prefer core damage signal if present
        if (coreHits != null && coreHits.Count > 0)
            return coreHits[0]; // single target in current rules

        // Else, owner with max piece losses
        int bestOwner = -1, bestLoss = 0;
        if (losses != null)
        {
            foreach (var kv in losses)
            {
                int lost = kv.Value;
                if (lost > bestLoss) { bestLoss = lost; bestOwner = kv.Key.owner; }
            }
        }
        return (bestLoss > 0) ? bestOwner : (int?)null;
    }

    private static Dictionary<OT, int> CountOwnerType(GameSnapshot s)
    {
        var map = new Dictionary<OT, int>(16);
        for (int i = 0; i < s.pieceCount; i++)
        {
            var key = new OT(s.pieceOwner[i], s.pieceType[i]);
            map.TryGetValue(key, out var c);
            map[key] = c + 1;
        }
        return map;
    }
}
