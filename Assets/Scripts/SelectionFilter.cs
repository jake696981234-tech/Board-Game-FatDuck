using UnityEngine;

public static class SelectionFilter
{
    public static bool WaitingForConnectors = false;
    public static bool WaitingForSacrificeCost = false;
    public static bool WaitingForBuildCellSelection = false;

    public static void topLevelFilter()
    {
        if (cellClicked)
        {
            if (WaitingForConnectors) return;
            if (WaitingForSacrificeCost && SacrificeCostTargetIsLegal(cell))
            {
                cachedSacrificeCost[cachedSacrificeCost.Length] = cell;
                if (cachedSacrificeCost.Length == PieceDefinition.SacrificeCost_howManyNeeded[ActionBeingFiltered.pieceType])
                {
                    TryToPeformAction();
                    ResetWait();
                    return;
                }
                ShowLegalOptionsForSacrficeCost();
            }
        }


        if (buildItemClicked)
        {
            WaitingForBuildCellSelection = true;
            if (PieceDefinition.HasConnectors[BuildItem]) WaitingForConnectors = true;
            if (PieceDefinition.SacrificeCost[BuildItem]) WaitingForSacrificeCost = true;
        }

    }

    #region BuildFilter

    public static Game.Core.Action ActionBeingFiltered;
    public static int[] cachedSacrificeCost;

    public static void BuilditemFilter()
    {

    }
    #endregion
}
