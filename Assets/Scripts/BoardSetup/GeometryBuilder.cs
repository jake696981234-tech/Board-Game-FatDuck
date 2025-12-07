// Assets/Scripts/Core/GeometryBuilder.cs
using System;
using System.Collections.Generic;

public static class GeometryBuilder
{
    private static readonly (int dq, int dr)[] DIRS = new[]
    {
        ( +1,  0), ( +1, -1), ( 0, -1),
        ( -1,  0), ( -1, +1), ( 0, +1),
    };

    public static BoardGeometry Build()
    {
        byte radius = GameBootstrapper.hub.board_radius;
        // 1) Enumerate axial coords
        var coords = new List<(short q, short r)>(EstimateCellCount(radius));
        var idByAxial = new Dictionary<(short, short), int>(coords.Capacity);

        for (int q = -radius; q <= radius; q++)
        {
            int rmin = Math.Max(-radius, -q - radius);
            int rmax = Math.Min(radius, -q + radius);
            for (int r = rmin; r <= rmax; r++)
            {
                int id = coords.Count;
                var key = ((short)q, (short)r);
                coords.Add(key);
                idByAxial[key] = id;
            }
        }

        int cellCount = coords.Count;

        // 2) Neighbors
        var neighborsById = new int[cellCount][];
        for (int id = 0; id < cellCount; id++)
        {
            var (q, r) = coords[id];
            var neigh = new int[6];
            for (int d = 0; d < 6; d++)
            {
                var nq = (short)(q + DIRS[d].dq);
                var nr = (short)(r + DIRS[d].dr);
                neigh[d] = idByAxial.TryGetValue((nq, nr), out var nid) ? nid : -1;
            }
            neighborsById[id] = neigh;
        }

        // 3) Dist from center (0,0)
        var distFromCenter = new short[cellCount];
        for (int id = 0; id < cellCount; id++)
        {
            var (q, r) = coords[id];
            distFromCenter[id] = (short)HexDistanceAxial(q, r, 0, 0);
        }

        // 4) Rings
        var ringsLists = new List<int>[radius + 1];
        for (int k = 0; k <= radius; k++) ringsLists[k] = new List<int>();
        for (int id = 0; id < cellCount; id++)
        {
            int d = distFromCenter[id];
            ringsLists[d].Add(id);
        }
        var ringsByRadius = new int[radius + 1][];
        for (int k = 0; k <= radius; k++) ringsByRadius[k] = ringsLists[k].ToArray();

        // 5) Return geometry (now includes idByAxial)
        return new BoardGeometry(
            coordById: coords.ToArray(),
            neighborsById: neighborsById,
            ringsByRadius: ringsByRadius,
            distFromCenter: distFromCenter,
            idByAxial: idByAxial
        );
    }

    public static short[] ComputeDistancesTo(int vpId, int[][] neighborsById)
    {
        int n = neighborsById.Length;
        var dist = new short[n];
        for (int i = 0; i < n; i++) dist[i] = short.MaxValue;

        var q = new Queue<int>(n);
        dist[vpId] = 0; q.Enqueue(vpId);
        while (q.Count > 0)
        {
            int cur = q.Dequeue();
            short nd = (short)(dist[cur] + 1);
            var neigh = neighborsById[cur];
            for (int d = 0; d < 6; d++)
            {
                int nb = neigh[d];
                if (nb < 0) continue;
                if (dist[nb] == short.MaxValue)
                {
                    dist[nb] = nd;
                    q.Enqueue(nb);
                }
            }
        }
        return dist;
    }

    private static int EstimateCellCount(byte radius) => 1 + 3 * radius * (radius + 1);

    private static int HexDistanceAxial(int q1, int r1, int q2, int r2)
    {
        int dq = q1 - q2;
        int dr = r1 - r2;
        int ds = -(q1 + r1) + (q2 + r2);
        return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(ds)) / 2;
    }
}
