using UnityEngine;
using System.Collections.Generic;

public sealed class GameSnapshotComposer
{
    private readonly BoardGeometry geometry;
    private readonly BoardModel board;
    private readonly Game.Core.GameState state;
    private readonly Pieces pieces;

    private readonly GameSnapshot staticSnapshot = new();
    private uint versionCounter = 0;

    public GameSnapshotComposer(BoardGeometry g, BoardModel b, Game.Core.GameState s, Pieces p)
    {
        geometry = g;
        board = b;
        state = s;
        pieces = p;
        BuildStaticGeometry();
    }

    private void BuildStaticGeometry()
    {
        staticSnapshot.cellCount = board.GetCellCount();
        staticSnapshot.worldPosById = new Vector3[staticSnapshot.cellCount];

        // axial -> world (pointy-top)
        for (int id = 0; id < staticSnapshot.cellCount; id++)
        {
            var (q, r) = geometry.coordById[id];
            staticSnapshot.worldPosById[id] = AxialToWorld(q, r, board.Radius);
        }

        staticSnapshot.victoryPointCellId = board.GetVictoryPointCellId();

        staticSnapshot.coreCellIdByPlayer = new int[4];
        for (byte p = 0; p < 4; p++)
            staticSnapshot.coreCellIdByPlayer[p] = board.GetPlayerCoreCellId(p);
    }

    private List<byte?> PiecesWithConnectors()
    {
        List<byte?> FilteredPieces = new List<byte?>();
        int i = 0;
        foreach (byte Piece in board.pieceType)
        {
            if (pieces.HasConnectors(Piece))
            {
                FilteredPieces.Add(board.pieceConnectorConfig[i]);
            }
            else
            {
                FilteredPieces.Add(null);
            }
            i++;
        }
        return FilteredPieces;
    }



    public GameSnapshot GetSnapshot()
    {
        var snapshot = new GameSnapshot();

        // Static geometry (share references; immutable)
        snapshot.cellCount = staticSnapshot.cellCount;
        snapshot.worldPosById = staticSnapshot.worldPosById;
        snapshot.victoryPointCellId = staticSnapshot.victoryPointCellId;
        snapshot.coreCellIdByPlayer = staticSnapshot.coreCellIdByPlayer;

        // Pieces (deep copy mutable arrays so history is immutable)
        snapshot.pieceCount = board.pieceCount;
        if (snapshot.pieceCount > 0)
        {
            snapshot.pieceCellId = new int[snapshot.pieceCount];
            snapshot.pieceOwner = new int[snapshot.pieceCount];
            snapshot.pieceType = new byte[snapshot.pieceCount];
            snapshot.pieceHP = new short[snapshot.pieceCount];
            System.Array.Copy(board.pieceCellId, snapshot.pieceCellId, snapshot.pieceCount);
            System.Array.Copy(board.pieceOwner, snapshot.pieceOwner, snapshot.pieceCount);
            System.Array.Copy(board.pieceType, snapshot.pieceType, snapshot.pieceCount);
            System.Array.Copy(board.pieceHP, snapshot.pieceHP, snapshot.pieceCount);
        }
        snapshot.connector = PiecesWithConnectors().ToArray(); // already allocates a new array

        // live counters
        snapshot.centerVP = state.GetCenterVP();

        snapshot.coreHPByPlayer = new int[4];
        snapshot.vpByPlayer = new int[4];
        for (byte p = 0; p < 4; p++)
        {
            snapshot.coreHPByPlayer[p] = state.GetCoreHealth(p);
            snapshot.vpByPlayer[p] = state.GetVP(p);
        }

        // per-type UI metadata (safe to share)
        snapshot.spritePathByType = pieces.spritePathByType;
        snapshot.displayNameByType = pieces.displayNameByType;

        // default owner palette (can replace later)
        snapshot.ownerTintByPlayer = new Color[4] { Color.red, Color.blue, Color.green, Color.silver };

        snapshot.version = ++versionCounter;
        return snapshot;
    }

    /// <summary>
    /// Create a frozen (deep-copied) snapshot of just the mutable state we need for analytics/logging.
    /// Does NOT allocate/duplicate static geometry.
    /// </summary>
    public GameSnapshot GetFrozenMinimal()
    {
        var s = new GameSnapshot();
        // --- Pieces (deep copy) ---
        s.pieceCount = board.pieceCount;
        s.pieceCellId = (int[])board.pieceCellId.Clone();
        s.pieceOwner = (int[])board.pieceOwner.Clone();
        s.pieceType = (byte[])board.pieceType.Clone();
        s.pieceHP = (short[])board.pieceHP.Clone();

        // --- Match counters (copy values) ---
        s.centerVP = state.GetCenterVP();
        s.coreHPByPlayer = new int[4];
        s.vpByPlayer = new int[4];
        for (byte p = 0; p < 4; p++)
        {
            s.coreHPByPlayer[p] = state.GetCoreHealth(p);
            s.vpByPlayer[p] = state.GetVP(p);
        }
        // Not needed for logging:
        // s.worldPosById, s.spritePathByType, etc.
        return s;
    }

    private static Vector3 AxialToWorld(short q, short r, float radius)
    {
        float x = radius * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r);
        float z = radius * (1.5f * r);
        return new Vector3(x, 0, z);
    }
}
