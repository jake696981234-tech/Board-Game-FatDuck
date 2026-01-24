using static Game.Core.ActionKind;
using static UIFilter.UIType;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;
using static PieceActionFilter.UICState;


public static class Show
{
   public static void ShowActionsForAPiece()
    {
        SetGeneralUI(backDropColor: UI.hic.config.pieceActionBackground, panelColor: UI.hic.config.pieceActionPanelBackground, cellColor: UI.hic.config.defaultCellColor, build: false, create: true, action: false, pieceFull: true, execute: false, walls: false, secondWalls: false);
        PieceInfo.SetPieceInfo(AFilter.ChosenAction[(int)ChoosingTargetType]);
        PushPieceActionListForSelection();
    }

    public static void ShowTargetCellOptions()
    {
        SetGeneralUI(backDropColor: UI.hic.config.actionExecuteBackground, panelColor: UI.hic.config.actionExecutePanelBackground, cellColor: UI.hic.config.defaultCellColor, build: false, create: false, action: false, pieceFull: false, execute: true, walls: false, secondWalls: false);
        if (UI.hic.actionTitleText) UI.hic.actionTitleText.text = $"Action: {AFilter.ChosenAction[(int)ChoosingKind]}"; // to do, probs need fix it to enum to string
        if (UI.hic.actionPieceText) UI.hic.actionPieceText.text = $"Piece #{UIBridge.bm.occupantPieceId[AFilter.ChosenAction[(int)ChoosingActorsCell]]}";
        // if (UI.hic.actionCostText) UI.hic.actionCostText.text = $"Cost: {action.cost}"; to do, add cost
        showBoard.HighlightCells(GiveMeOffersContaining(Legal: true, Kind: true, ActorsCell: true, TargetCell: false, Type: true, WallConfig: true, intakeCell: false), UI.hic.config.actionLegalTargetHighlight);
    }

    public static void ShowUpgradeOptions()
    {
        SetGeneralUI(backDropColor: UI.hic.config.buildModeBackground, panelColor: UI.hic.config.buildModePanelBackground, cellColor: UI.hic.config.defaultCellColor, build: true, create: false, action: true, pieceFull: false, execute: false, walls: false, secondWalls: false);
        var items = new List<Game.Core.Action>(UIBridge._count);
        var uiInfo = new List<UIInfo>(UIBridge._count);
        for (int i = 0; i < UIBridge._count; i++)
        {
            var theAction = UIBridge._offers[i];
            if (theAction.kind != Upgrade) continue;
            if (theAction.ActorsCell != AFilter.ChosenAction[(int)ChoosingActorsCell]) continue;
            int fullCost = Mathf.RoundToInt(UIBridge._quoted[i]);
            bool legal = UIBridge._mask[i] != 0;           // 1 = affordable+legal; 0 = masked out by cost, etc. :contentReference[oaicite:8]{index=8}
            items.Add(theAction);
            uiInfo.Add(new UIInfo(legal, fullCost));
        }
        UI.hic.buildMenu.Show(items, UI.hic.config);
    }

    public static void WallConfigOptions()
    {
        SetGeneralUI(backDropColor: UI.hic.config.ConnectorModeBackground, panelColor: UI.hic.config.buildModePanelBackground, cellColor: UI.hic.config.defaultCellColor, build: false, create: true, action: false, pieceFull: false, execute: false, walls: false, secondWalls: true);
        UI.hic.wallOptionPanel.showWallConfigOptions();
        PieceInfo.SetPieceInfo(AFilter.ChosenAction[(int)ChoosingTargetType]);
    }

    public static void SetGeneralUI(Color backDropColor, Color panelColor, Color cellColor, bool build, bool create, bool action, bool pieceFull, bool execute, bool walls, bool secondWalls)
    {
        showBoard.ClearHighlights();
        UIHelpers.SetBackdropColor(backDropColor);
        showBoard.ApplyDefaultCellColor(cellColor);
        UIHelpers.SetPanelBackdropColor(panelColor);
        PanelToggles.TogglePanels(build: build, create: create, action: action, pieceFull: pieceFull, execute: execute, walls: walls, secondWalls: secondWalls);
        ShowLeftPanel.HudRefresh();
    }



    private static void PushPieceActionListForSelection()
    {
        var items = new List<ActionItem>();
        bool moveAddedForCell = false;
        
        for (int i = 0; i < UIBridge._count; i++)
        {
            var action = UIBridge._offers[i];
            if (action.kind == EndTurn) continue; // exclude non-piece actions
            // if (action.kind == GroupBuild) to do- re add this path
            // {
            //     if (!IsGroupBuildForSelection(action, UIBridge._mask[i])) continue;
            // }
            // else
            {
                if (action.ActorsCell != AFilter.ChosenAction[(int)ChoosingActorsCell]) continue;               // only actions from this piece
                if (action.kind != Upgrade && action.TargetType != UIBridge.bm.GetPieceTypeFromCell(AFilter.ChosenAction[(int)ChoosingTargetType])) continue;
            }

            // Show only one Move per selected piece unless raw offers requested
            if (action.kind == Move && !UI.hic.config.GiveRawActionOffers)
            {
                if (moveAddedForCell) continue;
                moveAddedForCell = true;
            }

            bool legal = UIBridge._mask[i] != 0;

            int kind = action.kind;
            string label = UIHelpers.PrettyAction(action);
            int cost = Mathf.RoundToInt(UIBridge._quoted[i]);
            
            items.Add(new ActionItem(i.ToString(), label, cost, legal, Array.Empty<int>(), kind));
        }
        
        UI.hic.pieceActionListFull.Show(items);
    }

    private static IEnumerable<int> GiveMeOffersContaining(bool Legal, bool Kind, bool ActorsCell, bool TargetCell, bool Type, bool WallConfig, bool intakeCell)
    {
        List<int> targetCells = new List<int>(UIBridge.bm._cellCount);
        for (int i = 0; i < UIBridge._count; i++)
        {
            if (UIBridge._offers[i].kind != AFilter.ChosenAction[(int)ChoosingKind] && Kind) continue;
            if (UIBridge._offers[i].ActorsCell != AFilter.ChosenAction[(int)ChoosingActorsCell] && ActorsCell) continue;
            if (UIBridge._offers[i].TargetCell != AFilter.ChosenAction[(int)ChoosingTargetCell] && TargetCell) continue;
            if (UIBridge._offers[i].TargetType != AFilter.ChosenAction[(int)ChoosingTargetType] && Type) continue;
            if (UIBridge._offers[i].WallConfig != AFilter.ChosenAction[(int)ChoosingWallConfig] && WallConfig) continue;
            if (UIBridge._offers[i].intakeCell != AFilter.ChosenAction[(int)ChoosingInstakeCellID] && intakeCell) continue;
            if (UIBridge._mask[i] == 0 && Legal) continue;
            
            // if (action.addCost == null || action.addCost.Length == 0) continue;
            // if (!action.addCost.Contains(actorCell)) continue;
            // if (!targetCells.Contains(action.ActorsCellId))
            targetCells.Add(UIBridge._offers[i].TargetCell);
            AFilter.cachedLegalTargetCellId[i] = UIBridge._offers[i].ActorsCell;
        }
        AFilter.cachedLegalTargetCellId = targetCells;
        return targetCells;
    }
}
