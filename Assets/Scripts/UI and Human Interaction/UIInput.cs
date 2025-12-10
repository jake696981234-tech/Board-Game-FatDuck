using UnityEngine;
using System;
using UnityEngine.InputSystem;

public static class UIInput
{
    #region input
    public static void HookPresenters()
    {
        // CellClicked += OnCellClicked;   // from your BoardViewController
        if (UI.hic.endTurnButton) UI.hic.endTurnButton.onClick.AddListener(OnEndTurnClicked);
        if (UI.hic.buildMenu) UI.hic.buildMenu.OnItemClicked += OnBuildItemClicked;
        if (UI.hic.nonPieceActionList) UI.hic.nonPieceActionList.OnItemClicked += OnActionItemClicked;
        if (UI.hic.pieceActionListFull) UI.hic.pieceActionListFull.OnItemClicked += OnActionItemClicked;
        if (UI.hic.SeePerPieceTypeTotalsButton) UI.hic.SeePerPieceTypeTotalsButton.onClick.AddListener(() => ShowFactoryBonusByPieceTypePrefabs());
    }

    public static void UnhookPresenters()
    {
        if (UI.hic.buildMenu) UI.hic.buildMenu.OnItemClicked -= OnBuildItemClicked;
        if (UI.hic.nonPieceActionList) UI.hic.nonPieceActionList.OnItemClicked -= OnActionItemClicked;
        if (UI.hic.pieceActionListFull) UI.hic.pieceActionListFull.OnItemClicked -= OnActionItemClicked;
    }

    //Cancel Button
    public static void UpdateMe()
    {
        UIBridge.SnapShotUpdate();
        // Global cancel (New Input System)
        // Esc key OR Right Mouse Button pressed this frame
        bool cancel =
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
           (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);

        if (cancel)
        {
            switch (PanelToggles._mode)
            {
                case PanelToggles.Mode.WallOptionsSecoundPanel: UIModes.enterWallOptionsSecondPanelMode(); break; //this is dumb lazy code i added
                case PanelToggles.Mode.Create: UIModes.EnterBuildMode(); break;
                case PanelToggles.Mode.PieceAction: UIModes.EnterBuildMode(); break;
                case PanelToggles.Mode.ActionExecute: UIModes.EnterPieceActionMode(); break;
                case PanelToggles.Mode.MultiInputAction: UIModes.EnterPieceActionMode(); break;
            }
        }
    }
    #endregion
    // ===================== Input (from board) =====================
    #region OnCellClicked

    public static void OnCellClicked(int cellId)
    {
        if (UI.hic.config && UI.hic.config.blockInputWhenNotYourTurn && UIBridge.gameState != null)
        {
            // gate input while not human's turn (wire up your player id if needed)
            if (UIBridge.gameState.CurrentPlayerId != UIBridge._humanPlayer) return;
        }

        switch (PanelToggles._mode)
        {
            case PanelToggles.Mode.Build:
                UIHelpers._firstCellSelected = null;
                UIHelpers._selectedCellId = cellId;
                // Selection/hover feedback: show selected cell using config colour
                if (UI.hic.config)
                {
                    showBoard.ClearHighlights();
                    showBoard.HighlightSelection(cellId, UI.hic.config.selectionHighlight);
                }
                // Only enter piece mode if the cell has any piece-driven actions.
                if (UIHelpers.HasPieceActionsForCell(cellId))
                {
                    UIModes.EnterPieceActionMode();  // switches right column to full panel
                    // NOTE: EnterPieceActionMode() calls PushPieceActionListForSelection(),
                    // so the list is visible on the very first click.
                }
                // else: stay on default Build + Non-piece Action layout
                break;

            case PanelToggles.Mode.Create:
            case PanelToggles.Mode.WallOptionsSecoundPanel:
                UIHelpers._firstCellSelected = null;
                // Click on a highlighted target → perform the concrete Create action
                if (UIHelpers._createArmed)
                {
                    if (UIHelpers.cachedChosenWall == null)
                    {
                        int idx = UIHelpers.FindConcreteCreateAction(UIHelpers._selectedCreateType, (ushort)cellId);
                        if (idx >= 0) UIBridge.PerformActionIndex(idx);
                    }
                    else
                    {
                        int idx = UIHelpers.FindConcreteCreateAction(UIHelpers._selectedCreateType, (ushort)cellId, UIHelpers.cachedChosenWall);
                        if (idx >= 0) UIBridge.PerformActionIndex(idx);
                    }
                }
                // Clear either way and return to Build
                showBoard.ClearHighlights();
                UIModes.EnterBuildMode();
                break;

            case PanelToggles.Mode.MultiInputAction:
                if (UIHelpers.FindIfLegalForMultiAction(cellId))
                {
                    UIHelpers._firstCellSelected = cellId;
                    ActionItem item = UIHelpers._selectedAction ?? throw new Exception("Null!");
                    UIModes.EnterActionExecuteMode(item);
                }
                break;

            case PanelToggles.Mode.PieceAction:
                UIHelpers._firstCellSelected = null;
                UIHelpers._selectedCellId = cellId;
                ShowRightPanel.PushPieceActionListForSelection();
                break;

            case PanelToggles.Mode.ActionExecute:
                // We’re in targeting: the user clicked a highlighted cell → find the concrete action
                if (UIHelpers._selectedActionIndex >= 0)
                {
                    var seed = UIBridge._offers[UIHelpers._selectedActionIndex];
                    int idx = UIHelpers.FindConcreteAction(seed.kind, seed.ActorsCellId, seed.pieceType, (ushort)cellId);
                    if (idx >= 0) { UIBridge.PerformActionIndex(idx); }
                    // Clear highlights regardless (perform may change board)
                    showBoard.ClearHighlights();
                    UIModes.EnterBuildMode();
                }
                break;
        }
    }
    #endregion

    #region Buttons Clicked
    private static void OnEndTurnClicked()
    {
        // find EndTurn in the current offers and perform it
        int idx = UIHelpers.FindEndTurnIndex();
        if (idx >= 0) UIBridge.PerformActionIndex(idx);
    }
    #endregion

    #region Prefab Clicked
    private static void OnBuildItemClicked(BuildItem item)
    {
        if (PieceDefinition.connectors_enabled[item.pieceType])
        {
            UIHelpers.CachedBuildItemForCreateConnector = item;
            UIModes.EnterConnectingMode(item);
        }
        else
        {
            UIModes.EnterCreateMode(item);
        }
    }

    private static void OnActionItemClicked(ActionItem item)
    {
        if (!int.TryParse(item.id, out var idx)) return;
        if (idx < 0 || idx >= UIBridge._count) return;

        // If this is a NON-piece action (e.g., End Turn), perform immediately.
        // NOTE: If your constant lives at Action.Kind.EndTurn, swap the symbol accordingly.

        if (UIBridge._offers[idx].kind == Game.Core.ActionKind.EndTurn)
        {
            UIBridge.PerformActionIndex(idx);
            showBoard.ClearHighlights();
            UIModes.EnterBuildMode(); // stay on default layout after executing a non-piece action
            return;
        }

        // Piece-derived action: highlight all legal target cells right away
        var col = (UI.hic.config ? UI.hic.config.actionLegalTargetHighlight : new Color(0.6f, 0.35f, 0.9f, 0.65f));
        UIHelpers._selectedAction = item;
        UIHelpers._selectedActionIndex = idx; // seed used for target resolution
        showBoard.ClearHighlights();
        showBoard.HighlightCells(UIHelpers.ComputeTargetsForActionIndex(UIHelpers._selectedActionIndex), col);
        UIModes.EnterActionExecuteMode(item);
    }
    #endregion

    #region non Crucial
    public static void ShowFactoryBonusByPieceTypePrefabs()
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
