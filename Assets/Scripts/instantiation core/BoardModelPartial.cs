using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;

public partial class BoardModel
{

    /// <summary>
    /// Returns an approximate hex direction index 0..5 from fromCell to toCell,
    /// or -1 if same cell or invalid. Based on axial direction.
    /// </summary>
    public int GetDirectionIndex(int fromCell, int toCell)
    {
        if (!IsValidCellId(fromCell) || !IsValidCellId(toCell) || fromCell == toCell)
            return -1;

        var a = geo.coordById[fromCell];
        var b = geo.coordById[toCell];
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
    public int StepInDirection(int startCell, int dir, int steps)
    {
        if (!IsValidCellId(startCell) || (uint)dir >= 6 || steps <= 0)
            return _invalidId;

        int current = startCell;
        var neighbors = geo.neighborsById;

        for (int i = 0; i < steps; i++)
        {
            int next = neighbors[current][dir];
            if (next < 0) return _invalidId;
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
    public int cellIdsRingAroundCell(int originCell, int ringSize, bool requireEmpty, Span<int> outCells)
    {
        if (!IsValidCellId(originCell) || ringSize < 0) return 0;
        if (ringSize == 0)
        {
            if (!requireEmpty || IsEmpty(originCell))
            {
                if (outCells.Length > 0) outCells[0] = originCell;
                return 1;
            }
            return 0;
        }

        EnsureScratchAllocated();
        _stamp++;

        int head = 0, tail = 0;
        _q[tail++] = originCell;
        _seen[originCell] = _stamp;
        _dist[originCell] = 0;

        int write = 0;
        while (head < tail)
        {
            int cell = _q[head++];
            int d = _dist[cell];
            if (d == ringSize)
            {
                // ringSize reached: we only collect; don't expand further
                if (!requireEmpty || IsEmpty(cell))
                {
                    if (write < outCells.Length)
                        outCells[write] = cell;
                    write++;
                }
                continue;
            }
            if (d > ringSize) continue;

            var neigh = geo.neighborsById[cell];
            for (int i = 0; i < 6; i++)
            {
                int n = neigh[i];
                if (n < 0) continue;
                if (_seen[n] == _stamp) continue;
                _seen[n] = _stamp;
                _dist[n] = (short)(d + 1);
                _q[tail++] = n;
            }
        }

        return write; // total cells at exact ringSize (may be > outCells.Length)
    }

    /// <summary>
    /// Collects occupant pieceIds at EXACT hex ringSize from originCell.
    /// Returns total count; writes up to outPieceIds.Length.
    /// Zero-alloc: reuses the ring scratch buffer.
    /// </summary>
    public int pieceIdsRingAroundCell(int originCell, int ringSize, Span<int> outPieceIds)
    {
        if (!IsValidCellId(originCell) || ringSize < 0) return 0;

        var scratchCells = GetScratchCellBuffer();
        int cellsAtRing = cellIdsRingAroundCell(originCell, ringSize, requireEmpty: false, scratchCells.AsSpan());
        if (cellsAtRing <= 0) return 0;

        int write = 0;
        int limit = Math.Min(cellsAtRing, scratchCells.Length);
        for (int i = 0; i < limit; i++)
        {
            int pid = occupantPieceId[scratchCells[i]];
            if (pid == _invalidId || !IsValidPieceId(pid)) continue;
            if (write < outPieceIds.Length)
                outPieceIds[write] = pid;
            write++;
        }

        return write; // total pieceIds found (may exceed outPieceIds.Length)
    }


    /// <summary>
    /// Checks if a piece can move/land here under generic rules
    /// (on-board, empty). More rules can be layered in call-site.
    /// </summary>
    public bool IsLegalDestination(int cellId)
    {
        return IsValidCellId(cellId) && IsEmpty(cellId);
    }

    
    

}


