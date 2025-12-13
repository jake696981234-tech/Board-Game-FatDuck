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
                case PanelToggles.Mode.SacrificeSelect: UIModes.EnterBuildMode(); break;
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

            case PanelToggles.Mode.SacrificeSelect:
                {
                    int pid = UIBridge.bm.GetCellOccupant(cellId);
                    if (pid < 0) break;
                    if (UIHelpers.SelectedSacrificeIds.Contains(pid)) break;
                    if (UIBridge.bm.GetPieceOwner(pid) != UIBridge.gameState.CurrentPlayerId) break;

                    bool allowed = false;
                    for (int i = 0; i < UIHelpers.SacrificeCombos.Count; i++)
                    {
                        var combo = UIHelpers.SacrificeCombos[i];
                        if (combo == null) continue;
                        bool subset = true;
                        for (int s = 0; s < UIHelpers.SelectedSacrificeIds.Count; s++)
                        {
                            if (Array.IndexOf(combo, UIHelpers.SelectedSacrificeIds[s]) < 0) { subset = false; break; }
                        }
                        if (!subset) continue;
                        if (Array.IndexOf(combo, pid) >= 0) { allowed = true; break; }
                    }
                    if (!allowed) break;

                    UIHelpers.SelectedSacrificeIds.Add(pid);
                    UIHelpers.SortSelectedSacrifices();

                    // Determine need based on current mode (create or upgrade)
                    int need = 0;
                    if (UIHelpers._selectedActionIndex >= 0 && UIBridge._offers[UIHelpers._selectedActionIndex].kind == Game.Core.ActionKind.Upgrade)
                    {
                        need = PieceDefinition.sacrificeCost_howManyItNeeds[UIBridge._offers[UIHelpers._selectedActionIndex].pieceType];
                    }
                    else
                    {
                        need = PieceDefinition.sacrificeCost_howManyItNeeds[UIHelpers._selectedCreateType];
                    }

                    if (UIHelpers.SelectedSacrificeIds.Count >= need)
                    {
                        if (UIHelpers._selectedActionIndex >= 0 && UIBridge._offers[UIHelpers._selectedActionIndex].kind == Game.Core.ActionKind.Upgrade)
                        {
                            // auto-perform upgrade with matching sacrifice set
                            int srcCell = UIHelpers.SelectedUpgradeSourceCell ?? UIBridge._offers[UIHelpers._selectedActionIndex].ActorsCellId;
                            int idx = UIHelpers.FindConcreteUpgradeAction(UIBridge._offers[UIHelpers._selectedActionIndex].pieceType, (ushort)srcCell);
                            if (idx >= 0) { UIBridge.PerformActionIndex(idx); }
                            UIHelpers.SelectedSacrificeIds.Clear();
                            UIHelpers.SacrificeCombos.Clear();
                            UIHelpers.SelectedUpgradeSourceCell = null;
                            showBoard.ClearHighlights();
                            UIModes.EnterBuildMode();
                        }
                        else
                        {
                            UIModes.EnterCreateModeForSacrifice(UIHelpers.CachedActionForSacrificeCostMode, UIHelpers.CachedUIInfoForSacrificeCostMode);
                        }
                    }
                    else
                    {
                        showBoard.ClearHighlights();
                        showBoard.HighlightCells(
                            UIHelpers.GetNextSelectableSacrificeCells(),
                            UI.hic.config ? UI.hic.config.createModeCellHighlight : new Color(0.25f, 0.75f, 0.25f, 0.6f)
                        );
                    }
                    break;
                }

            case PanelToggles.Mode.Create:
            case PanelToggles.Mode.MultiCreateAction:
                if (UIHelpers._createArmed)
                {
                    int idx = UIHelpers.FindConcreteCreateAction(UIHelpers._selectedCreateType, (ushort)cellId);
                    if (idx >= 0) { UIBridge.PerformActionIndex(idx); }
                    showBoard.ClearHighlights();
                    UIModes.EnterBuildMode();
                }
                break;
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
    private static void OnBuildItemClicked(Game.Core.Action item, UIInfo uiInfo)
    {
        if (PieceDefinition.sacrificeCost_enabled[item.pieceType])
        {
            UIModes.EnterSacrificeSelectMode(item, uiInfo);
            return;
        }
        if (PieceDefinition.connectors_enabled[item.pieceType])
        {
            UIHelpers.CachedBuildItemForCreateConnector = item;
            UIHelpers.CachedUIInfoForCreateConnector = uiInfo;
            UIModes.EnterConnectingMode(item);
        }
        else
        {
            UIModes.EnterCreateMode(item, uiInfo);
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
        // If upgrade with sacrifice cost, branch into sacrifice select flow
        if (UIBridge._offers[idx].kind == Game.Core.ActionKind.Upgrade && PieceDefinition.sacrificeCost_enabled[UIBridge._offers[idx].pieceType])
        {
            UIModes.EnterUpgradeSacrificeSelectMode(item, idx);
        }
        else
        {
            UIModes.EnterActionExecuteMode(item);
        }
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
