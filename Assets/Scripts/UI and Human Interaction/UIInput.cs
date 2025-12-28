using UnityEngine;
using System;
using UnityEngine.InputSystem;

public static class UIInput
{
    #region input
    public static void UpdateMe()
    {
        UIBridge.SnapShotUpdate();

        bool cancel =
        (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
        (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
        if (cancel) UIFilter.onCancel();
    }
    #endregion
    // ===================== Input (from board) =====================
    

    #region non Crucial
    public static void ShowFactoryBonusByPieceTypePrefabs() // to do, need add this back to the left panel. there is commented out action inside UIFilter that links to this.
    {
        PanelToggles.ToggleLeftPanels(true, PanelToggles.leftPanelMode == PanelToggles.LeftPanelsModes.EndRoundTotalPanel);

        if (PanelToggles.leftPanelMode == PanelToggles.LeftPanelsModes.EndRoundTotalPanel)
        {
            PanelToggles.leftPanelMode = PanelToggles.LeftPanelsModes.EndRoundTotalPanel2;
        }
        else
        {
            PanelToggles.leftPanelMode = PanelToggles.LeftPanelsModes.EndRoundTotalPanel;
        }


        ShowLeftPanel.updatePerTypeEndRoundTotals();
    }
    #endregion
}
