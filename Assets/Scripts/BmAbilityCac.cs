using UnityEngine;
using System;

public static class BmAbilityCac
{
    /// <summary>
    /// Returns an approximate hex direction index 0..5 from fromCell to toCell,
    /// or -1 if same cell or invalid. Based on axial direction.
    /// </summary>
    public static int GetDirectionIndex(int fromCell, int toCell, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (!bm.IsValidCellId(fromCell) || !bm.IsValidCellId(toCell) || fromCell == toCell)
            return -1;

        var a = bm.geo.coordById[fromCell];
        var b = bm.geo.coordById[toCell];
        int dq = b.q - a.q;
        int dr = b.r - a.r;

        // Hex direction basis vectors in axial coords
        // Must match the order used for geo.neighborsById
        (int dq, int dr)[] DIRS = new[]
        {
        (1, 0), (1, -1), (0, -1),
        (-1, 0), (-1, 1), (0, 1)
    };

        // Choose dir whose vector is "closest" to (dq,dr)
        int bestDir = -1;
        int bestScore = int.MinValue;
        for (int d = 0; d < 6; d++)
        {
            int vq = DIRS[d].dq;
            int vr = DIRS[d].dr;
            int score = dq * vq + dr * vr; // dot product-ish
            if (score > bestScore)
            {
                bestScore = score;
                bestDir = d;
            }
        }
        return bestDir;
    }

    /// <summary>
    /// Walks up to <paramref name="steps"/> steps from startCell in direction dir.
    /// Stops early if off-board. Returns final cellId or InvalidId if we stepped off.
    /// </summary>
    public static int StepInDirection(int startCell, int dir, int steps, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (!bm.IsValidCellId(startCell) || (uint)dir >= 6 || steps <= 0)
            return bm._invalidId;

        int current = startCell;
        var neighbors = bm.geo.neighborsById;

        for (int i = 0; i < steps; i++)
        {
            int next = neighbors[current][dir];
            if (next < 0) return bm._invalidId;
            current = next;
        }

        return current;
    }

    /// <summary>
    /// Collects all cells at EXACT hex ringSize == ringSize from originCell.
    /// If requireEmpty == true, only returns empty cells.
    /// Returns total count; writes up to outCells.Length.
    /// Zero-alloc: uses internal BFS scratch buffers.
    /// </summary>
    public static int cellIdsRingAroundCell(int originCell, int ringSize, bool requireEmpty, Span<int> outCells, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (!bm.IsValidCellId(originCell) || ringSize < 0) return 0;
        if (ringSize == 0)
        {
            if (!requireEmpty || bm.IsEmpty(originCell))
            {
                if (outCells.Length > 0) outCells[0] = originCell;
                return 1;
            }
            return 0;
        }

        bm.EnsureScratchAllocated();
        bm._stamp++;

        int head = 0, tail = 0;
        bm._q[tail++] = originCell;
        bm._seen[originCell] = bm._stamp;
        bm._dist[originCell] = 0;

        int write = 0;
        while (head < tail)
        {
            int cell = bm._q[head++];
            int d = bm._dist[cell];
            if (d == ringSize)
            {
                // ringSize reached: we only collect; don't expand further
                if (!requireEmpty || bm.IsEmpty(cell))
                {
                    if (write < outCells.Length)
                        outCells[write] = cell;
                    write++;
                }
                continue;
            }
            if (d > ringSize) continue;

            var neigh = bm.geo.neighborsById[cell];
            for (int i = 0; i < 6; i++)
            {
                int n = neigh[i];
                if (n < 0) continue;
                if (bm._seen[n] == bm._stamp) continue;
                bm._seen[n] = bm._stamp;
                bm._dist[n] = (short)(d + 1);
                bm._q[tail++] = n;
            }
        }

        return write; // total cells at exact ringSize (may be > outCells.Length)
    }


    /// <summary>
    /// Collects occupant pieceIds at EXACT hex ringSize from originCell.
    /// Returns total count; writes up to outPieceIds.Length.
    /// Zero-alloc: reuses the ring scratch buffer.
    /// </summary>
    public static int pieceIdsRingAroundCell(int originCell, int ringSize, Span<int> outPieceIds, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (!bm.IsValidCellId(originCell) || ringSize < 0) return 0;

        var scratchCells = bm.GetScratchCellBuffer();
        int cellsAtRing = cellIdsRingAroundCell(originCell, ringSize, requireEmpty: false, scratchCells.AsSpan(), gameIndex);
        if (cellsAtRing <= 0) return 0;

        int write = 0;
        int limit = Math.Min(cellsAtRing, scratchCells.Length);
        for (int i = 0; i < limit; i++)
        {
            int pid = bm.occupantPieceId[scratchCells[i]];
            if (pid == bm._invalidId || !bm.IsValidPieceId(pid)) continue;
            if (write < outPieceIds.Length)
                outPieceIds[write] = pid;
            write++;
        }

        return write; // total pieceIds found (may exceed outPieceIds.Length)
    }

    /// <summary>
    /// Among the 6 neighbors of <paramref name="centerCell"/>, returns the empty cell that is
    /// closest (by hex distance) to <paramref name="originCell"/>. Ties break by smaller cellId.
    /// Returns -1 if none are empty.
    /// </summary>
    public static int FindNearestEmptyAdjacent(int originCell, int centerCell, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (!bm.IsValidCellId(centerCell)) return bm._invalidId;

        var neigh = bm.geo.neighborsById[centerCell];
        int best = bm._invalidId;
        int bestDist = int.MaxValue;

        for (int d = 0; d < 6; d++)
        {
            int n = neigh[d];
            if (n < 0) continue;         // off board
            if (!bm.IsEmpty(n)) continue;    // occupied

            int dist = bm.DistanceCells(originCell, n);
            if (dist < bestDist || (dist == bestDist && n < best))
            {
                best = n;
                bestDist = dist;
            }
        }

        return best;
    }


    /// <summary>
    /// True if every intermediate cell on the straight hex line is empty (endpoints may be occupied).
    /// Uses cube-lerp rounding (Red Blob). Requires geo.coordById & geo.idByAxial.
    /// </summary>
    public static bool LineOfSightClear(int fromCell, int toCell, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (!bm.IsValidCellId(fromCell) || !bm.IsValidCellId(toCell)) return false;
        int steps = bm.DistanceCells(fromCell, toCell);
        if (steps <= 1) return true; // adjacent or same

        var a = bm.geo.coordById[fromCell];
        var b = bm.geo.coordById[toCell];

        // cube coords
        double ax = a.q, az = a.r, ay = -ax - az;
        double bx = b.q, bz = b.r, by = -bx - bz;

        const double EPS = 1e-6;
        for (int i = 1; i < steps; i++)
        {
            double t = (double)i / (double)steps;
            double x = Lerp(ax + EPS, bx - EPS, t);
            double y = Lerp(ay + EPS, by - EPS, t);
            double z = Lerp(az + EPS, bz - EPS, t);
            CubeRound(x, y, z, out int rx, out int ry, out int rz);
            var rq = (short)rx; var rr = (short)rz;

            if (!bm.geo.idByAxial.TryGetValue((rq, rr), out int midId))
                return false; // Off-board—treat as blocked (topology mismatch)
            if (bm.occupantPieceId[midId] != bm._invalidId)
                return false; // blocked by any piece
        }
        return true;
    }

    #region helpers to the methods Above 
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static void CubeRound(double x, double y, double z, out int rx, out int ry, out int rz)
    {
        rx = (int)Math.Round(x); ry = (int)Math.Round(y); rz = (int)Math.Round(z);
        double dx = Math.Abs(rx - x), dy = Math.Abs(ry - y), dz = Math.Abs(rz - z);
        if (dx > dy && dx > dz) rx = -ry - rz;
        else if (dy > dz) ry = -rx - rz;
        else rz = -rx - ry;
    }
    #endregion
}
