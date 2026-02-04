using UnityEngine;

public class HCost : MonoBehaviour
{
    public static bool UpdateBuild = false;
    public static bool UpdatePieceActions = false;
    public static bool UpdateCreate = false;
    float secondAccumulator;

    void Update()
    {
        if (!UIBridge.IsCurrentPlayerTheHuman) return;
        if (UIBridge.gameState.ps[UIBridge._humanPlayer].budget <= 0)
        {
            UIBridge.PerformActionIndex(UIHelpers.FindEndTurnIndex());
            return;
        }

        secondAccumulator += Time.deltaTime;
        if (secondAccumulator < 1f) return;
        secondAccumulator -= 1f;

        UIBridge.gameState.ps[UIBridge._humanPlayer].budget -= 1;

        UI.hic.Personal_BudgetText.text = "Budget: " + $"{UIBridge.gameState.ps[UIBridge._humanPlayer].budget}";
        UI.hic.Personal_BudgetAfterActionFee.text = "Budget - Action Fee: " + $"{UIBridge.gameState.ps[UIBridge._humanPlayer].budget - ShowLeftPanel.curActionFee}";
        if (UpdateBuild) UpdateBuildActionsLegality();
        if (UpdateCreate) PieceInfo.UpdateCreateCost();
        if (UpdatePieceActions) UpdatePieceActionsLegality();
    }


    public static void UpdateBuildActionsLegality()
    {
        for (int i = 0; i < UI.hic.buildMenu.BuildActionPrefabs.Length; i++)
        {
            if (UI.hic.buildMenu.BuildActionPrefabs[i] == null) continue;
            if (UIBridge.gameState.ps[UIBridge._humanPlayer].budget < UI.hic.buildMenu.BuildActionPrefabs[i].cachedUIInfo.fullCost) UI.hic.buildMenu.BuildActionPrefabs[i].setLegality(false);
        }
    }

    public static void UpdatePieceActionsLegality()
    {
        for (int i = 0; i < UI.hic.pieceActionListFull.theItems.Length; i++) if (UIBridge.gameState.ps[UIBridge._humanPlayer].budget < UI.hic.pieceActionListFull.theItems[i].cost) UI.hic.pieceActionListFull.PieceActionPrefabs[i].setLegality(false);
    }
}


