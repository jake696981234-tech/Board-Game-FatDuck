using UnityEngine;

public static class UIModes
{
    // ===================== Mode transitions =====================
    #region Mode Transitions
    public static void EnterBuildMode()
    {
        showBoard.ClearHighlights();
        showBoard.ApplyDefaultCellColor(UI.hic.config.defaultCellColor);
        PanelToggles._mode = PanelToggles.Mode.Build;
        UIHelpers._selectedPieceId = null;
        UIHelpers._selectedAction = null;

        UIHelpers.SetBackdropColor(UI.hic.config ? UI.hic.config.buildModeBackground : new Color(0, 0, 0, 0.8f));
        UIHelpers.SetPanelBackdropColor(UI.hic.config ? UI.hic.config.buildModePanelBackground : new Color(0, 0, 0, 0.8f));

        PanelToggles.TogglePanels(build: true, create: false, action: true, pieceFull: false, execute: false, walls: false);
        ShowRightPanel.PushBuildMenu();
        ShowRightPanel.PushNonPieceActionList();   // NEW: default action list = non-piece actions (e.g., End Turn)

        ShowLeftPanel.HudRefresh();
    }

    public static void EnterCreateMode(BuildItem build, ushort? ChosenWall = null)
    {
        showBoard.ClearHighlights();
        showBoard.ApplyDefaultCellColor(UI.hic.config.defaultCellColor);
        // Seed the create family (piece type) and highlight all legal cells for that type.
        UIHelpers._selectedCreateType = build.pieceType;
        UIHelpers._selectedActionIndex = UIHelpers.FindFirstCreateIndexForType(UIHelpers._selectedCreateType); // seed (can be -1 if none)
        UIHelpers._createArmed = (UIHelpers._selectedActionIndex >= 0);

        if (ChosenWall == null)
        {
            showBoard.HighlightCells(
                UIHelpers.ComputeCreateTargetsForPieceType(UIHelpers._selectedCreateType),
                UI.hic.config ? UI.hic.config.createModeCellHighlight : new Color(0.25f, 0.75f, 0.25f, 0.6f)
            );

            UIHelpers.cachedChosenWall = null;
        }
        else
        {
            showBoard.HighlightCells(
                UIHelpers.ComputeCreateTargetsForPieceType(UIHelpers._selectedCreateType, ChosenWall),
                UI.hic.config ? UI.hic.config.createModeCellHighlight : new Color(0.25f, 0.75f, 0.25f, 0.6f)
            );

            UIHelpers.cachedChosenWall = ChosenWall;
        }


        PanelToggles._mode = PanelToggles.Mode.Create;
        UIHelpers._selectedPieceId = null;
        UIHelpers._selectedCellId = null;
        UIHelpers._selectedAction = null;

        UIHelpers.SetBackdropColor(UI.hic.config ? UI.hic.config.createModeBackground : new Color(0, 0, 0, 0.8f));
        UIHelpers.SetPanelBackdropColor(UI.hic.config ? UI.hic.config.createModePanelBackground : new Color(0, 0, 0, 0.8f));
        PanelToggles.TogglePanels(build: false, create: true, action: false, pieceFull: false, execute: false, walls: false);

        if (UI.hic.createTitleText) UI.hic.createTitleText.text = $"Create: {build.name}";
        if (UI.hic.createCostText) UI.hic.createCostText.text = $"Cost: {build.cost}";
        if (UI.hic.createSprite)
        {
            var s = !string.IsNullOrEmpty(build.spritePath) ? Resources.Load<Sprite>(build.spritePath) : null;
            UI.hic.createSprite.sprite = s;
            UI.hic.createSprite.enabled = (s != null);
        }

        // NOTE: We haven't added highlight APIs to BoardViewController yet, so no highlight calls here.
        ShowLeftPanel.HudRefresh();
    }

    public static void EnterConnectingMode(BuildItem item)
    {
        // Ensure we query offers for the piece type the user just picked

        PanelToggles._mode = PanelToggles.Mode.Create;
        UIHelpers._selectedCreateType = item.pieceType;
        UIHelpers._selectedActionIndex = UIHelpers.FindFirstCreateIndexForType(UIHelpers._selectedCreateType);
        var wallOptions = UIHelpers.WallOptionsForPieceType(UIHelpers._selectedCreateType);

        PanelToggles.TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: false, walls: true);

        UI.hic.wallOptionPanel.ShowSideOptions(wallOptions);
    }

    public static void EnterPieceActionMode(int? pieceId = null)
    {
        showBoard.ClearHighlights();
        showBoard.ApplyDefaultCellColor(UI.hic.config.defaultCellColor);
        PanelToggles._mode = PanelToggles.Mode.PieceAction;
        UIHelpers._selectedAction = null;
        if (pieceId.HasValue) UIHelpers._selectedPieceId = pieceId;

        UIHelpers.SetBackdropColor(UI.hic.config ? UI.hic.config.pieceActionBackground : new Color(0, 0, 0, 0.8f));
        UIHelpers.SetPanelBackdropColor(UI.hic.config ? UI.hic.config.pieceActionPanelBackground : new Color(0, 0, 0, 0.8f));

        PanelToggles.TogglePanels(build: false, create: false, action: false, pieceFull: true, execute: false, walls: false);
        // Immediately fill the full panel so it shows on the first click:
        ShowRightPanel.PushPieceActionListForSelection();

        ShowLeftPanel.HudRefresh();
    }

    public static void EnterActionExecuteMode(ActionItem action)
    {
        showBoard.ClearHighlights();
        if (action.kind == Game.Core.ActionKind.Launcher && PanelToggles._mode != PanelToggles.Mode.MultiInputAction)
        {
            PanelToggles._mode = PanelToggles.Mode.MultiInputAction;
        }
        else
        {
            PanelToggles._mode = PanelToggles.Mode.ActionExecute;
        }

        UIHelpers._selectedAction = action;

        UIHelpers.SetBackdropColor(UI.hic.config ? UI.hic.config.actionExecuteBackground : new Color(0, 0, 0, 0.8f));
        UIHelpers.SetPanelBackdropColor(UI.hic.config ? UI.hic.config.actionExecutePanelBackground : new Color(0, 0, 0, 0.8f));

        PanelToggles.TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: true, walls: false);
        // (Re)apply legal-target highlights for clarity while in execute mode
        if (UIHelpers._selectedActionIndex >= 0)
        {
            var col = UI.hic.config ? UI.hic.config.actionLegalTargetHighlight : new Color(0.6f, 0.35f, 0.9f, 0.65f);
            showBoard.ClearHighlights();
            showBoard.HighlightCells(UIHelpers.ComputeTargetsForActionIndex(UIHelpers._selectedActionIndex), col);
        }

        if (UI.hic.actionTitleText) UI.hic.actionTitleText.text = action.name;
        if (UI.hic.actionPieceText) UI.hic.actionPieceText.text = UIHelpers._selectedPieceId.HasValue ? $"Piece #{UIHelpers._selectedPieceId.Value}" : "Piece (none)";
        if (UI.hic.actionCostText) UI.hic.actionCostText.text = $"Cost: {action.cost}";

        ShowLeftPanel.HudRefresh();
    }

    public static void enterWallOptionsSecondPanelMode()
    {
        PanelToggles.TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: false, walls: true);
        showBoard.ClearHighlights();
        PanelToggles._mode = PanelToggles.Mode.Create;
    }

    public static void EnterCreateModeWithWallChosen(ushort chosenWall)
    {
        EnterCreateMode(UIHelpers.CachedBuildItemForCreateConnector, chosenWall);
    }

    #endregion
}
