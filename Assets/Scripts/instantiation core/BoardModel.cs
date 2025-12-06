// Assets/Scripts/Model/BoardModel.cs
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

/// <summary>
/// ID-centric, data-only board state.
/// - No ScriptableObject reads; everything is injected at init.
/// - No VP/Core HP/rounds/caps here (GameState owns live match counters).
/// - Stores only anchors (VP cell, per-player core cells), occupancy, and dense piece tables.
/// - Geometry is injected (BoardGeometry) for zero-alloc neighbors/distance/LOS. 
/// </summary>
public partial class BoardModel
{
    // ---------- Immutable board constants (set once at Init) ----------
    private int _radius;
    private int _cellCount;
    private int _invalidId;

    // ---------- Scenario anchors (cell IDs; set at Init) ----------
    private int _vpCellId;
    private int[] _coreCellIdByPlayer; // len = playerCount, cores assumed static

    // ---------- Geometry (injected at Init; shared, readonly) ----------
    private BoardGeometry geo;

    // Precomputed per-cell shortest-path distances to the configured VP cell
    private int[] _distFromVP;

    // ---------- Cells (mutable occupancy) ----------
    // occupantPieceId[cellId] = pieceId | -1
    public int[] occupantPieceId;

    // ---------- Pieces (mutable dense table) ----------
    public int pieceCount;    // rows in use [0..pieceCount-1]
    public int pieceCapacity; // allocated length of columns

    public int[] pieceOwner;   // [pieceId] -> player index
    public int[] pieceCellId;  // [pieceId] -> cellId
    public byte[] pieceType;    // [pieceId] -> type index (semantics live in Pieces.cs)
    public short[] pieceHP;      // [pieceId] -> hp (unit/building maxHP comes from Pieces.cs)

    #region Currently Working on
    public int[] pieceFactoryAux;
    #endregion
    public byte[] pieceConnectorConfig; // [pieceId] -> connector configuration index (0-63) if hasConnectors, else 0
    public int[] pieceCapitalHP;       // [pieceId] -> current capital HP buff (0 if none)

    public HashSet<int> spawnerUsedThisTurn = new HashSet<int>();

    // ---------- Optional hook from Pieces (perf helper) ----------
    public Func<byte, bool> IsBuildingType;

    // =====================================================================
    // Init
    // =====================================================================

    /// <summary>
    /// Inject all board constants, anchors, and precomputed geometry. 
    /// This allocates occupancy and piece columns, but does NOT set any VP/core HP.
    /// (GameState will seed/own live match counters.)
    /// </summary>
    // BoardModel.cs
    public void Init(in BoardGeometry geometry, in GameConfigHub hub, int playerCount, int initialPieceCapacity = 8, int[] coreCellIdOverride = null)
    {
        // store snapshots
        geo = geometry;
        _radius = hub.board_radius;
        _cellCount = hub.board_totalCells;
        _invalidId = hub.board_invalidCellId;
        _vpCellId = hub.board_vpCellId;
        _coreCellIdByPlayer = coreCellIdOverride != null
            ? (int[])coreCellIdOverride.Clone()
            : (int[])hub.board_coreCellIdByPlayer.Clone();

        occupantPieceId = new int[_cellCount];
        for (int i = 0; i < _cellCount; i++) occupantPieceId[i] = _invalidId;

        // inside BoardModel.Init(...)
        occupantPieceId = new int[_cellCount];
        for (int i = 0; i < _cellCount; i++) occupantPieceId[i] = _invalidId;


        // allocate occupancy & pieces as before, using initialPieceCapacity
        EnsurePieceCapacity(Math.Max(1, initialPieceCapacity));
        EnsureScratchAllocated();

        // Precompute distance map from the configured VP cell for fast lookups
        _distFromVP = ComputeDistFromCell(_vpCellId);
    }


    // =====================================================================
    // Properties / shallow queries
    // =====================================================================

    public int Radius => _radius;
    public int CellCount => _cellCount;
    public int InvalidId => _invalidId;
    public int VictoryPointCellId => _vpCellId;
    public int CoreCellIdForPlayer(byte p) => _coreCellIdByPlayer[p];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidCellId(int cellId) => (uint)cellId < (uint)_cellCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidPieceId(int pieceId) => (uint)pieceId < (uint)pieceCount;

    // =====================================================================
    // Minimal wrappers many systems expect (ID-only)
    // =====================================================================

    public int GetCellCount() => _cellCount; // The Places that refrence this can should refrence the struct directly

    public int GetCellOccupant(int cellId)
        => (IsValidCellId(cellId) && occupantPieceId != null) ? occupantPieceId[cellId] : _invalidId;

    public int GetPieceCell(int pieceId)
        => (IsValidPieceId(pieceId) && pieceCellId != null) ? pieceCellId[pieceId] : _invalidId;

    public int GetPieceOwner(int pieceId)
        => (IsValidPieceId(pieceId) && pieceOwner != null) ? pieceOwner[pieceId] : -1;

    public byte GetPieceType(int pieceId)
        => (IsValidPieceId(pieceId) && pieceType != null) ? pieceType[pieceId] : (byte)0;

    public int GetNeighborCell(int cellId, int dir)
    {
        if (!IsValidCellId(cellId) || (uint)dir >= 6) return _invalidId;
        return geo.neighborsById[cellId][dir];
    }

    public int GetVictoryPointCellId() => _vpCellId; // The Places that refrence this can should refrence the struct directly

    public int GetPlayerCoreCellId(int owner)
        => (owner >= 0 && owner < _coreCellIdByPlayer.Length) ? _coreCellIdByPlayer[owner] : _invalidId;

    public void SetPlayerCoreCells(int[] coreCellIds)
    {
        _coreCellIdByPlayer = coreCellIds != null ? (int[])coreCellIds.Clone() : Array.Empty<int>();
    }

    /// <summary>True if cell belongs to any opponent core (owner != actorOwner).</summary>
    public bool IsEnemyCoreCell(int cellId, int actorOwner)
    {
        for (int i = 0; i < _coreCellIdByPlayer.Length; i++)
        {
            if (i == actorOwner) continue;
            if (_coreCellIdByPlayer[i] == cellId) return true;
        }
        return false;
    }


    // =====================================================================
    // Geometry passthrough (no allocations)
    // =====================================================================

    /// <summary>Writes up to 6 neighbor ids to out6 in fixed dir order 0..5. Returns count written.</summary>
    public int GetNeighbors(int cellId, Span<int> out6)
    {
        if (!IsValidCellId(cellId)) return 0;
        var row = geo.neighborsById[cellId];
        int n = out6.Length < 6 ? out6.Length : 6;
        for (int i = 0; i < n; i++) out6[i] = row[i];
        return n;
    }

    /// <summary>Neighbor row view. Read-only; do not cache across resizes (IDs are stable).</summary>
    public int[] Neighbors(int cellId) => IsValidCellId(cellId) ? geo.neighborsById[cellId] : Array.Empty<int>();

    public bool TryGetNeighbor(int cellId, int dir, out int neighborId)
    {
        neighborId = _invalidId;
        if (!IsValidCellId(cellId) || (uint)dir >= 6) return false;
        neighborId = geo.neighborsById[cellId][dir];
        return neighborId >= 0;
    }

    /// <summary>Hex distance via axial cube-manhattan/2.</summary>
    public int DistanceCells(int aCellId, int bCellId)
    {
        if (!IsValidCellId(aCellId) || !IsValidCellId(bCellId)) return int.MaxValue / 4;
        var a = geo.coordById[aCellId];
        var b = geo.coordById[bCellId];
        int dq = a.q - b.q, dr = a.r - b.r;
        return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
    }

    public bool IsAdjacent(int aCellId, int bCellId) => DistanceCells(aCellId, bCellId) == 1;

    /// <summary>Distance to VP: if your geometry carries a dist map, use it; else fall back to DistanceCells.</summary>
    public int DistToVictoryPoint(int cellId)
    {
        if (!IsValidCellId(cellId) || _vpCellId == _invalidId) return int.MaxValue / 4;
        if (_distFromVP != null && _distFromVP.Length == _cellCount)
            return _distFromVP[cellId];
        // Fallback: compute on the fly if precompute is unavailable
        return DistanceCells(cellId, _vpCellId);
    }

    // ----------------------------------------------------------------------------
    // Internal helpers
    // ----------------------------------------------------------------------------
    private int[] ComputeDistFromCell(int startCell)
    {
        if (!IsValidCellId(startCell)) return null;
        var dist = new int[_cellCount];
        for (int i = 0; i < dist.Length; i++) dist[i] = int.MaxValue / 4;
        var q = new System.Collections.Generic.Queue<int>();
        dist[startCell] = 0;
        q.Enqueue(startCell);
        while (q.Count > 0)
        {
            int c = q.Dequeue();
            var nbrs = geo.neighborsById[c];
            for (int i = 0; i < 6 && i < nbrs.Length; i++)
            {
                int nb = nbrs[i];
                if (nb < 0 || nb >= _cellCount) continue;
                if (dist[nb] <= dist[c] + 1) continue;
                dist[nb] = dist[c] + 1;
                q.Enqueue(nb);
            }
        }
        return dist;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEmpty(int cellId) => IsValidCellId(cellId) && occupantPieceId[cellId] == _invalidId;

    public bool IsVictoryPointCell(int cellId) => cellId == _vpCellId;

    public bool IsPlayerCoreCell(int cellId, int playerIdx)
        => playerIdx >= 0 && playerIdx < _coreCellIdByPlayer.Length && _coreCellIdByPlayer[playerIdx] == cellId;

    public int GetPieceCountForPlayer(int playerIdx)
    {
        if (playerIdx < 0) return 0;
        int count = 0;
        for (int pid = 0; pid < pieceCount; pid++)
        {
            if (pieceOwner[pid] == playerIdx) count++;
        }
        return count;
    }

    public int PieceAt(int cellId) => IsValidCellId(cellId) ? occupantPieceId[cellId] : _invalidId;

    public bool IsAlive(int pieceId) => IsValidPieceId(pieceId);
    public int PieceOwner(int pieceId) => GetPieceOwner(pieceId);
    public int PieceCell(int pieceId) => GetPieceCell(pieceId);
    public byte PieceType(int pieceId) => GetPieceType(pieceId);
    public short PieceHP(int pieceId) => (IsValidPieceId(pieceId) && pieceHP != null) ? pieceHP[pieceId] : (short)0;
    public int PieceFactoryAux(int pieceId) => (IsValidPieceId(pieceId) && pieceFactoryAux != null) ? pieceFactoryAux[pieceId] : 0;

    // =====================================================================
    // Atomic piece ops (dense columns + cell occupancy kept in sync)
    // =====================================================================

    private void EnsurePieceCapacity(int min)
    {
        if (pieceCapacity >= min) return;
        int newCap = pieceCapacity > 0 ? pieceCapacity * 2 : 8;
        if (newCap < min) newCap = min;
        Array.Resize(ref pieceOwner, newCap);
        Array.Resize(ref pieceCellId, newCap);
        Array.Resize(ref pieceType, newCap);
        Array.Resize(ref pieceHP, newCap);
        Array.Resize(ref pieceFactoryAux, newCap);
        Array.Resize(ref pieceConnectorConfig, newCap);
        Array.Resize(ref pieceCapitalHP, newCap);
        pieceCapacity = newCap;
    }

    public int AllocateRow()
    {
        EnsurePieceCapacity(pieceCount + 1);
        int pid = pieceCount;
        pieceCount = pid + 1;
        return pid;
    }

    /// <summary>Removes row by swapping back with the last row and fixing occupancy.</summary>
    public void FreeRowSwapBack(int pieceId)
    {
        if (!IsValidPieceId(pieceId)) return;

        int last = pieceCount - 1;

        // clear previous cell occupancy
        int oldCell = pieceCellId[pieceId];
        if (IsValidCellId(oldCell) && occupantPieceId[oldCell] == pieceId)
            occupantPieceId[oldCell] = _invalidId;

        // swap with last if needed
        if (pieceId != last)
        {
            pieceOwner[pieceId] = pieceOwner[last];
            pieceCellId[pieceId] = pieceCellId[last];
            pieceType[pieceId] = pieceType[last];
            pieceHP[pieceId] = pieceHP[last];
            pieceFactoryAux[pieceId] = pieceFactoryAux[last];
            pieceConnectorConfig[pieceId] = pieceConnectorConfig[last];
            pieceCapitalHP[pieceId] = pieceCapitalHP[last];

            int movedCell = pieceCellId[pieceId];
            if (IsValidCellId(movedCell)) occupantPieceId[movedCell] = pieceId;
        }

        pieceCount = last;
    }



    /// <summary>Write fields for an existing/allocated row and set occupancy.</summary>
    public void PlacePieceRow(int pieceId, int owner, byte type, int cellId, short hp)
    {
        pieceOwner[pieceId] = owner;
        pieceType[pieceId] = type;
        pieceCellId[pieceId] = cellId;
        if (hp < 0) hp = 0;
        pieceHP[pieceId] = hp; // clamp to type maxHP happens in GameState via Pieces metadata, if needed
        pieceFactoryAux[pieceId] = 0; //Add to the paramter if you want this to actually have a starting value
        pieceConnectorConfig[pieceId] = 0;
        pieceCapitalHP[pieceId] = 0;
        if (IsValidCellId(cellId)) occupantPieceId[cellId] = pieceId;
    }

    /// <summary>Move row to a new cell, updating occupancy.</summary>
    public void MovePieceRow(int pieceId, int dstCellId)
    {
        int src = pieceCellId[pieceId];
        if (IsValidCellId(src) && occupantPieceId[src] == pieceId) occupantPieceId[src] = _invalidId;
        pieceCellId[pieceId] = dstCellId;
        if (IsValidCellId(dstCellId)) occupantPieceId[dstCellId] = pieceId;
    }

    /// <summary>Subtract hp; returns true if the row died (hp <= 0). Caller will FreeRowSwapBack.</summary>
    public bool DamagePieceRow(int pieceId, int delta)
    {
        int hp = pieceHP[pieceId] - delta;
        if (hp > 0) { pieceHP[pieceId] = (short)hp; return false; }

        pieceHP[pieceId] = 0;
        return true;
    }

    /// <summary>Clears all occupancy and rows; returns number removed.</summary>
    public int RemoveAllPieces()
    {
        int removed = pieceCount;
        if (occupantPieceId != null)
            for (int i = 0; i < occupantPieceId.Length; i++) occupantPieceId[i] = _invalidId;
        pieceCount = 0;
        return removed;
    }

    // =====================================================================
    // Zero-alloc traversal helpers (optional but useful)
    // =====================================================================

    // Scratch (allocated once) for deterministic BFS/LOS
    private int[] _q;        // queue
    private int[] _seen;     // stamp per cell
    private short[] _dist;   // distance per cell
    private int _stamp;      // increments per call

    // Public scratch buffers
    private int[] _scratchCells;     // len == CellCount
    private int[] _scratchNeighbors; // len >= 6

    public int[] GetScratchCellBuffer()
    {
        if (_scratchCells == null || _scratchCells.Length != _cellCount)
            _scratchCells = new int[_cellCount];
        return _scratchCells;
    }

    public int[] GetScratchNeighborBuffer()
    {
        if (_scratchNeighbors == null || _scratchNeighbors.Length < 6)
            _scratchNeighbors = new int[6];
        return _scratchNeighbors;
    }

    private void EnsureScratchAllocated()
    {
        if (_q == null || _q.Length != _cellCount) _q = new int[_cellCount];
        if (_seen == null || _seen.Length != _cellCount) _seen = new int[_cellCount];
        if (_dist == null || _dist.Length != _cellCount) _dist = new short[_cellCount];
        if (_stamp == int.MaxValue) { Array.Clear(_seen, 0, _seen.Length); _stamp = 0; }
    }

    /// <summary>
    /// BFS over EMPTY cells only (includes origin if empty).
    /// Deterministic layer order (cellId ascending). Returns total reachable count.
    /// </summary>
    public int EnumerateReachableEmpty(int originCell, int maxSteps, Span<int> outCells)
    {
        if (!IsValidCellId(originCell) || maxSteps < 0) return 0;

        EnsureScratchAllocated();
        _stamp++;

        int total = 0, write = 0, tail = 0;
        _q[tail++] = originCell;
        _seen[originCell] = _stamp; _dist[originCell] = 0;

        int layerStart = 0, layerEnd = 1, step = 0;

        while (layerStart < layerEnd && step <= maxSteps)
        {
            // Emit current layer
            for (int i = layerStart; i < layerEnd; i++)
            {
                int cell = _q[i];
                if (IsEmpty(cell))
                {
                    total++;
                    if (write < outCells.Length) outCells[write++] = cell;
                }
            }
            if (step == maxSteps) break;

            // Expand
            for (int i = layerStart; i < layerEnd; i++)
            {
                int cell = _q[i];
                var neigh = geo.neighborsById[cell];
                for (int d = 0; d < 6; d++)
                {
                    int n = neigh[d];
                    if (n < 0) continue;
                    if (_seen[n] == _stamp) continue;
                    if (!IsEmpty(n)) continue;
                    _seen[n] = _stamp;
                    _dist[n] = (short)(step + 1);
                    _q[tail++] = n;
                }
            }

            // Sort next frontier deterministically
            InsertionSortSegment(_q, layerEnd, tail);

            layerStart = layerEnd;
            layerEnd = tail;
            step++;
        }
        return total;
    }

    private static void InsertionSortSegment(int[] arr, int from, int to)
    {
        for (int i = from + 1; i < to; i++)
        {
            int key = arr[i]; int j = i - 1;
            while (j >= from && arr[j] > key) { arr[j + 1] = arr[j]; j--; }
            arr[j + 1] = key;
        }
    }

    /// <summary>
    /// True if every intermediate cell on the straight hex line is empty (endpoints may be occupied).
    /// Uses cube-lerp rounding (Red Blob). Requires geo.coordById & geo.idByAxial.
    /// </summary>
    public bool LineOfSightClear(int fromCell, int toCell)
    {
        if (!IsValidCellId(fromCell) || !IsValidCellId(toCell)) return false;
        int steps = DistanceCells(fromCell, toCell);
        if (steps <= 1) return true; // adjacent or same

        var a = geo.coordById[fromCell];
        var b = geo.coordById[toCell];

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

            if (!geo.idByAxial.TryGetValue((rq, rr), out int midId))
                return false; // Off-board—treat as blocked (topology mismatch)
            if (occupantPieceId[midId] != _invalidId)
                return false; // blocked by any piece
        }
        return true;
    }


    /// <summary>
    /// Among the 6 neighbors of <paramref name="centerCell"/>, returns the empty cell that is
    /// closest (by hex distance) to <paramref name="originCell"/>. Ties break by smaller cellId.
    /// Returns -1 if none are empty.
    /// </summary>
    public int FindNearestEmptyAdjacent(int originCell, int centerCell)
    {
        if (!IsValidCellId(centerCell)) return _invalidId;

        var neigh = geo.neighborsById[centerCell];
        int best = _invalidId;
        int bestDist = int.MaxValue;

        for (int d = 0; d < 6; d++)
        {
            int n = neigh[d];
            if (n < 0) continue;         // off board
            if (!IsEmpty(n)) continue;    // occupied

            int dist = DistanceCells(originCell, n);
            if (dist < bestDist || (dist == bestDist && n < best))
            {
                best = n;
                bestDist = dist;
            }
        }

        return best;
    }





    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static void CubeRound(double x, double y, double z, out int rx, out int ry, out int rz)
    {
        rx = (int)Math.Round(x); ry = (int)Math.Round(y); rz = (int)Math.Round(z);
        double dx = Math.Abs(rx - x), dy = Math.Abs(ry - y), dz = Math.Abs(rz - z);
        if (dx > dy && dx > dz) rx = -ry - rz;
        else if (dy > dz) ry = -rx - rz;
        else rz = -rx - ry;
    }

    // =====================================================================
    // Convenience queries (optional)
    // =====================================================================

    /// <summary>Owner of a core cell (0..3) or 255 if not a core cell.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte OwnerOfCoreCell(int cellId)
    {
        for (byte p = 0; p < _coreCellIdByPlayer.Length; p++)
            if (_coreCellIdByPlayer[p] == cellId) return p;
        return 255;
    }

    /// <summary>Collect owned pieceIds (ascending by id). Returns total count.</summary>
    public int GetOwnedPieceIds(int owner, Span<int> outPieceIds)
    {
        int total = 0, write = 0;
        for (int pid = 0; pid < pieceCount; pid++)
        {
            if (pieceOwner[pid] != owner) continue;
            total++;
            if (write < outPieceIds.Length) outPieceIds[write++] = pid;
        }
        return total;
    }

    /// <summary>Collect cells of owned Buildings using IsBuildingType predicate. Returns total count.</summary>
    public int GetOwnedBuildingCells(int owner, Span<int> outCells)
    {
        if (IsBuildingType == null) return 0;
        int total = 0, write = 0;
        for (int pid = 0; pid < pieceCount; pid++)
        {
            if (pieceOwner[pid] != owner) continue;
            if (!IsBuildingType(pieceType[pid])) continue;
            int cell = pieceCellId[pid];
            total++;
            if (write < outCells.Length) outCells[write++] = cell;
        }
        return total;
    }

    // 1) Pieces expects an int[] out param version of GetNeighbors
    public int GetNeighbors(int cellId, int[] outNeighborCells)
    {
        if (outNeighborCells == null || outNeighborCells.Length == 0) return 0;
        // Reuse the Span<int> implementation if you have it:
        int n = GetNeighbors(cellId, outNeighborCells.AsSpan());
        return n;
    }


    // 2) Pieces calls Distance(...), so provide a wrapper
    public int Distance(int cellA, int cellB) => DistanceCells(cellA, cellB);


    // 3) Pieces passes an int[] scratch to EnumerateReachableEmpty
    public int EnumerateReachableEmpty(int originCell, int maxSteps, int[] outCells)
    {
        if (outCells == null) return 0;
        return EnumerateReachableEmpty(originCell, maxSteps, outCells.AsSpan());
    }



}
public static class GameStateUtilities
{
    /// <summary>
    /// Removes all Soldiers (i.e., pieces where Pieces.IsBuilding(type) == false).
    /// Returns the number of rows removed. Uses swap-back to stay O(n).
    /// </summary>
    public static int RemoveAllSoldiers(int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        Span<int> protectedpieces = stackalloc int[240];
        int numberOfProtectedPieces = Game.Core.GameActions.ProtectedBySanctuary(protectedpieces, gameIndex);

        int removed = 0;
        for (int pid = bm.pieceCount - 1; pid >= 0; pid--)
        {
            byte t = bm.pieceType[pid];
            bool isBuilding = Pieces.IsBuilding(t); // assumes Pieces exposes this
            if (isBuilding || Game.Core.GameActions.IsPieceApartOfSpan(pid, protectedpieces, numberOfProtectedPieces)) continue;

            // Free row (handles occupancy + swap-back)
            bm.FreeRowSwapBack(pid);
            removed++;
        }
        return removed;
    }
}
