using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class BmCac
{
    //The Aim of this script is to contain board Model related Methods, that are needed for abilitys

    // ---- Geometry-free snapshots for analytics/logging ----
    public static Dictionary<(int owner, int type), int> SnapshotOwnerTypeCounts(BoardModel bm)
    {
        var map = new Dictionary<(int, int), int>(32);
        for (int pid = 0; pid < bm.pieceCount; pid++)
        {
            int owner = bm.pieceOwner[pid];
            int type = bm.pieceType[pid];
            var key = (owner, type);
            map.TryGetValue(key, out var c);
            map[key] = c + 1;
        }
        return map;
    }

    public static bool isConnectorSideFromCell(int cell, int Direction, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        return PiecesSides.IsConnectorSide(bm.pieceConnectorConfig[bm.occupantPieceId[cell]], Direction);
    }

    public static int[] OccupiedCellsInLine(int StartingCell, int maxRange, int minRange, int direction, bool needsLineOfSight, bool friendlyFire, bool onlySoldiers, int player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (!bm.IsValidCellId(StartingCell) || maxRange <= 0 || (uint)direction >= 6)
            return Array.Empty<int>();

        // normalize ranges
        if (maxRange < minRange) { int t = maxRange; maxRange = minRange; minRange = t; }
        if (minRange < 1) minRange = 1;

        List<int> occupiedCells = null;

        for (int step = 1; step <= maxRange; step++)
        {
            int cell = StepInDirection(StartingCell, direction, step, gameIndex);
            if (!bm.IsValidCellId(cell) || cell == Info.invalidId)
                break;

            int occupant = bm.GetCellOccupant(cell);

            // line of sight stops at the first blocker, even if outside minRange
            if (step < minRange)
            {
                if (needsLineOfSight && occupant != Info.invalidId)
                    break;
                continue;
            }

            if (occupant == Info.invalidId)
                continue;

            if (onlySoldiers && Piece.isBuilding[bm.GetPieceType(occupant)])
            {
                if (needsLineOfSight) break;
                continue;
            }

            if (friendlyFire && bm.GetPieceOwner(occupant) == player)
            {
                if (needsLineOfSight) break;
                continue;
            }

            occupiedCells ??= new List<int>();
            occupiedCells.Add(cell);

            if (needsLineOfSight)
                break;
        }

        return occupiedCells?.ToArray() ?? Array.Empty<int>();
    }

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

    public static int ReverseDirection(int dir)
    {
        return (dir >= 0 && dir < 6) ? (dir + 3) % 6 : -1;
    }

    public static bool TryReverseDirection(int cell, int dir, int gameIndex, out int reversedDir)
    {
        reversedDir = -1;
        var bm = GameRegistry.game[gameIndex].boardModel;
        if (!bm.IsValidCellId(cell)) return false;

        int rev = ReverseDirection(dir);
        if (rev < 0) return false;

        int neighbor = bm.geo.neighborsById[cell][rev];
        if (neighbor < 0) return false;

        reversedDir = rev;
        return true;
    }

    /// <summary>
    /// Walks up to <paramref name="steps"/> steps from startCell in direction dir.
    /// Stops early if off-board. Returns final cellId or InvalidId if we stepped off.
    /// </summary>
    public static int StepInDirection(int startCell, int dir, int steps, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (!bm.IsValidCellId(startCell) || (uint)dir >= 6 || steps <= 0)
            return Info.invalidId;

        int current = startCell;
        var neighbors = bm.geo.neighborsById;

        for (int i = 0; i < steps; i++)
        {
            int next = neighbors[current][dir];
            if (next < 0) return Info.invalidId;
            current = next;
        }

        return current;
    }

    public static int FindDirectionAndStep(int fromCell, int toCell, int steps, int gameIndex) => StepInDirection(startCell: fromCell, dir: GetDirectionIndex(fromCell: fromCell, toCell: toCell, gameIndex: gameIndex), steps: steps, gameIndex: gameIndex);

    public static int[] CellIdsLessPieceIds(int[] cellIds, int[] removedCells)
    {
        int[] result = cellIds.Except(removedCells).ToArray();
        return result;
    }



        
    
    public static List<int> CellIdsRingAndLessthanRing(int originCell, int ringSize, bool requireEmpty, bool requireOcc, int requireOwned, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        List<int> outCells = new();

        Span<int> cells = Scratch.GetScratchCellBuffer(gameIndex);
        for (int CheckRingSize = 0; CheckRingSize < ringSize; CheckRingSize++)
        {
            int foundAtDist = cellIdsRingAroundCell(originCell, CheckRingSize, requireEmpty, cells, gameIndex);
            for (int i = 0; i < foundAtDist; i++)
            {
                if (requireOcc)
                {
                    if (bm.IsEmpty(cells[i])) continue;
                    if (requireOwned != -1 && bm.isPieceIDOwnedFromCell(requireOwned, cells[i])) continue;
                }
                outCells.Add(cells[i]);
            } 
        }
        return outCells;
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

        Span<int> scratchCells = Scratch.GetScratchCellBuffer(gameIndex);
        int cellsAtRing = cellIdsRingAroundCell(originCell, ringSize, requireEmpty: false, scratchCells, gameIndex);
        if (cellsAtRing <= 0) return 0;

        int write = 0;
        int limit = Math.Min(cellsAtRing, scratchCells.Length);
        for (int i = 0; i < limit; i++)
        {
            int pid = bm.occupantPieceId[scratchCells[i]];
            if (pid == Info.invalidId || !bm.IsValidPieceId(pid)) continue;
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

        if (!bm.IsValidCellId(centerCell)) return Info.invalidId;

        var neigh = bm.geo.neighborsById[centerCell];
        int best = Info.invalidId;
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
            if (bm.occupantPieceId[midId] != Info.invalidId)
                return false; // blocked by any piece
        }
        return true;
    }

    public static int ComputePushDestination(
      int actorPieceId,
      int actorType,
      int targetPieceId,
      int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int actorCell = bm.GetPieceCell(actorPieceId);
        int targetCell = bm.GetPieceCell(targetPieceId);

        if (actorCell < 0 || targetCell < 0)
            return Info.invalidId;


        int pushAmount = Piece.push_pushAmount[actorType];
        if (pushAmount <= 0)
            return Info.invalidId;

        bool isPull = Piece.push_isPull[actorType];

        int dir = isPull
            ? GetDirectionIndex(targetCell, actorCell, gameIndex)
            : GetDirectionIndex(actorCell, targetCell, gameIndex);
        if (dir < 0)
            return Info.invalidId;

        int targetOwner = bm.GetPieceOwner(targetPieceId);
        int ownerCoreCell = bm.GetPlayerCoreCellId((byte)targetOwner);

        const int MaxRingCells = 256;
        Span<int> ringCells = stackalloc int[MaxRingCells];
        Span<int> farCells = stackalloc int[MaxRingCells];

        for (int dist = pushAmount; dist > 0; dist--)
        {
            int totalOnRing = cellIdsRingAroundCell(
                originCell: targetCell,
                ringSize: dist,
                requireEmpty: true,
                outCells: ringCells,
                gameIndex);

            if (totalOnRing <= 0)
                continue;

            int ringCount = Math.Min(totalOnRing, MaxRingCells);

            int extremeDistFromActor = isPull ? int.MaxValue : -1;
            for (int i = 0; i < ringCount; i++)
            {
                int cell = ringCells[i];
                int dAct = bm.Distance(actorCell, cell);
                if (isPull)
                {
                    if (dAct < extremeDistFromActor)
                        extremeDistFromActor = dAct;
                }
                else
                {
                    if (dAct > extremeDistFromActor)
                        extremeDistFromActor = dAct;
                }
            }
            if ((!isPull && extremeDistFromActor < 0) || (isPull && extremeDistFromActor == int.MaxValue))
                continue;

            int farCount = 0;
            for (int i = 0; i < ringCount; i++)
            {
                int cell = ringCells[i];
                int dAct = bm.Distance(actorCell, cell);
                if (dAct == extremeDistFromActor)
                    farCells[farCount++] = cell;
            }
            if (farCount == 0)
                continue;

            int idealBehindCell = StepInDirection(targetCell, dir, dist, gameIndex);

            if (idealBehindCell >= 0 && bm.IsValidCellId(idealBehindCell) && bm.IsEmpty(idealBehindCell))
            {
                for (int i = 0; i < farCount; i++)
                {
                    if (farCells[i] == idealBehindCell)
                        return idealBehindCell;
                }
            }

            int bestCell = Info.invalidId;
            int bestScore = int.MaxValue;

            for (int i = 0; i < farCount; i++)
            {
                int cell = farCells[i];

                if (!bm.IsValidCellId(cell) || !bm.IsEmpty(cell))
                    continue;

                int dCore = ownerCoreCell >= 0 ? bm.Distance(cell, ownerCoreCell) : 0;
                if (dCore < bestScore)
                {
                    bestScore = dCore;
                    bestCell = cell;
                }
            }

            if (bestCell >= 0)
                return bestCell;
        }

        return Info.invalidId;
    }



    public static int CountClusterOfType(byte type, int startCell, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (startCell < 0) return 0;
        var visited = Scratch.GetScratchCellBuffer(gameIndex);
        Array.Clear(visited, 0, visited.Length);
        int[] queue = Scratch.GetScratchCellBuffer(gameIndex);
        int head = 0, tail = 0;
        queue[tail++] = startCell;
        visited[startCell] = 1;
        int count = 0;
        while (head < tail)
        {
            int cell = queue[head++];
            int pid = bm.GetCellOccupant(cell);
            if (pid >= 0 && bm.GetPieceType(pid) == type) count++;
            int[] neigh = Scratch.GetScratchNeighborBuffer(gameIndex);
            int n = bm.GetNeighbors(cell, neigh);
            for (int i = 0; i < n; i++)
            {
                int nb = neigh[i];
                if (nb < 0 || nb >= visited.Length) continue;
                if (visited[nb] != 0) continue;
                int nbPid = bm.GetCellOccupant(nb);
                if (nbPid < 0 || bm.GetPieceType(nbPid) != type) continue;
                visited[nb] = 1;
                queue[tail++] = nb;
            }
        }
        return count;
    }

    public static bool IsCreateGeometryLegal(int cell, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (!bm.IsEmpty(cell)) return false;
        int core = bm.GetPlayerCoreCellId(player);
        if (cell == core) return true;
        var scratch = Scratch.GetScratchCellBuffer(gameIndex);
        int n = bm.GetNeighbors(core, scratch);
        for (int i = 0; i < n; i++) if (scratch[i] == cell) return true;
        int n2 = bm.GetNeighbors(cell, scratch);
        for (int i = 0; i < n2; i++)
        {
            int nb = scratch[i];
            int pid = bm.GetCellOccupant(nb);
            if (pid < 0) continue;
            if (bm.GetPieceOwner(pid) != player) continue;
            byte t = bm.GetPieceType(pid);
            if (Piece.isBuilding[t]) return true;
        }
        return false;
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
