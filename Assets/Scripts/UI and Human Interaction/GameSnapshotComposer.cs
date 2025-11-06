using UnityEngine;

public sealed class GameSnapshotComposer
{
    private readonly BoardGeometry geometry;
    private readonly BoardModel board;
    private readonly Game.Core.GameState state;
    private readonly Pieces pieces;

    private readonly GameSnapshot snapshot = new();

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
        snapshot.cellCount = board.GetCellCount();
        snapshot.worldPosById = new Vector3[snapshot.cellCount];

        // axial -> world (pointy-top)
        for (int id = 0; id < snapshot.cellCount; id++)
        {
            var (q, r) = geometry.coordById[id];
            snapshot.worldPosById[id] = AxialToWorld(q, r, board.Radius);
        }

        snapshot.victoryPointCellId = board.GetVictoryPointCellId();

        snapshot.coreCellIdByPlayer = new int[4];
        for (byte p = 0; p < 4; p++)
            snapshot.coreCellIdByPlayer[p] = board.GetPlayerCoreCellId(p);
    }

    public GameSnapshot GetSnapshot()
    {
        // pieces
        snapshot.pieceCount = board.pieceCount;
        snapshot.pieceCellId = board.pieceCellId;
        snapshot.pieceOwner  = board.pieceOwner;
        snapshot.pieceType   = board.pieceType;
        snapshot.pieceHP     = board.pieceHP;

        // live counters
        snapshot.centerVP = state.GetCenterVP();

        snapshot.coreHPByPlayer = new int[4];
        snapshot.vpByPlayer     = new int[4];
        for (byte p = 0; p < 4; p++)
        {
            snapshot.coreHPByPlayer[p] = state.GetCoreHealth(p);
            snapshot.vpByPlayer[p]     = state.GetVP(p);
        }

        // per-type UI metadata
        snapshot.spritePathByType  = pieces.spritePathByType;
        snapshot.displayNameByType = pieces.displayNameByType;

        // default owner palette (can replace later)
        snapshot.ownerTintByPlayer = new Color[4] { Color.red, Color.blue, Color.green, Color.yellow };

        snapshot.version++;
        return snapshot;
    }

    private static Vector3 AxialToWorld(short q, short r, float radius)
    {
        float x = radius * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r);
        float z = radius * (1.5f * r);
        return new Vector3(x, 0, z);
    }
}
