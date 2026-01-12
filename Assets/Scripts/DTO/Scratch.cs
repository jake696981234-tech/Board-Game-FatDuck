using UnityEngine;

public static class Scratch
{
    private static int[] _scratchCells;     // len == CellCount
    public static int[] GetScratchCellBuffer(int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (_scratchCells == null || _scratchCells.Length != bm._cellCount)
            _scratchCells = new int[bm._cellCount];
        return _scratchCells;
    }

    private static int[] _scratchCells2;     // len == CellCount
    public static int[] GetScratchCellBuffer2(int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (_scratchCells2 == null || _scratchCells2.Length != bm._cellCount)
            _scratchCells2 = new int[bm._cellCount];
        return _scratchCells2;
    }

    private static int[] _scratchNeighbors; // len >= 6
    public static int[] GetScratchNeighborBuffer(int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (_scratchNeighbors == null || _scratchNeighbors.Length < 6)
            _scratchNeighbors = new int[6];
        return _scratchNeighbors;
    }
}
