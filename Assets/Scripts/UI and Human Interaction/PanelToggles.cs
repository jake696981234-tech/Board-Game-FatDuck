using UnityEngine;

public static class PanelToggles
{
    // public enum Mode { Build, Create, PieceAction, ActionExecute, WallOptionsSecoundPanel, MultiInputAction, MultiCreateAction, SacrificeSelect }
    // public static Mode _mode = Mode.Build;
    public static void TogglePanels(bool build, bool create, bool action, bool pieceFull, bool execute, bool walls, bool secondWalls)
    {
        if (UI.hic.buildMenu) UI.hic.buildMenu.gameObject.SetActive(build);
        if (UI.hic.createPanel) UI.hic.createPanel.gameObject.SetActive(create);
        if (UI.hic.actionPanel) UI.hic.actionPanel.gameObject.SetActive(action);
        if (UI.hic.pieceActionPanelFull) UI.hic.pieceActionPanelFull.gameObject.SetActive(pieceFull);
        if (UI.hic.actionExecutePanel) UI.hic.actionExecutePanel.gameObject.SetActive(execute);
        UI.hic.WallCreatePanel1.SetActive(walls);
        UI.hic.WallCreatePanel2.SetActive(secondWalls);
    }



    public enum LeftPanelsModes { DefaultPanel, EndRoundTotalPanel, EndRoundTotalPanel2 }
    public static LeftPanelsModes leftPanelMode = LeftPanelsModes.DefaultPanel;
    public static void ToggleLeftPanels(bool EndRoundTotalPanel1, bool EndRoundTotalPanel2)
    {
        UI.hic.Payout_Panel1.SetActive(EndRoundTotalPanel1);
        UI.hic.Payout_Panel2.SetActive(EndRoundTotalPanel2);
    }

    // public static void SetWallOptionsSecondPanelMode()
    // {

    //     _mode = Mode.WallOptionsSecoundPanel;
    // }


}
