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

    private static int[] _scratchNeighbors; // len >= 6
    public static int[] GetScratchNeighborBuffer(int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        if (_scratchNeighbors == null || _scratchNeighbors.Length < 6)
            _scratchNeighbors = new int[6];
        return _scratchNeighbors;
    }
}
