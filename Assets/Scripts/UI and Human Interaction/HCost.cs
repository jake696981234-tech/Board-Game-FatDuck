using UnityEngine;

public class HCost : MonoBehaviour
{
    public static bool UpdateBuild = false;
    public static bool UpdatePieceActions = false;
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
        if (UpdateBuild) UpdateBuildActionsLegality();
        if (UpdatePieceActions) UpdatePieceActionsLegality();
    }


    public static void UpdateBuildActionsLegality()
    {
        for (int i = 0; i < UI.hic.buildMenu.theUIInfo.Count; i++) if (UIBridge.gameState.ps[UIBridge._humanPlayer].budget < UI.hic.buildMenu.theUIInfo[i].fullCost) UI.hic.buildMenu._pool[i].setLegality(false);
    }

    public static void UpdatePieceActionsLegality()
    {
        for (int i = 0; i < UI.hic.pieceActionListFull.theItems.Length; i++) if (UIBridge.gameState.ps[UIBridge._humanPlayer].budget < UI.hic.pieceActionListFull.theItems[i].cost) UI.hic.pieceActionListFull._pool[i].setLegality(false);
    }
}


