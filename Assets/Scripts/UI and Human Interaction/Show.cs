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
        showBoard.HighlightCells(ComputeTargetCellsForAction(), UI.hic.config.actionLegalTargetHighlight);
        if (UI.hic.actionTitleText) UI.hic.actionTitleText.text = $"Action: {AFilter.ChosenAction[(int)ChoosingKind]}"; // to do, probs need fix it to enum to string
        if (UI.hic.actionPieceText) UI.hic.actionPieceText.text = $"Piece #{UIBridge.bm.occupantPieceId[AFilter.ChosenAction[(int)ChoosingActorsCell]]}";
        // if (UI.hic.actionCostText) UI.hic.actionCostText.text = $"Cost: {action.cost}"; to do, add cost
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

    private static void NumberOfWallOptions()
    {
        SetGeneralUI(backDropColor: UI.hic.config.ConnectorModeBackground, panelColor: UI.hic.config.buildModePanelBackground, cellColor: UI.hic.config.defaultCellColor, build: false, create: true, action: false, pieceFull: false, execute: false, walls: true, secondWalls: false);
        UI.hic.wallOptionPanel.showNumberOfWallS(ComputeWallOptions());
        if (UI.hic.config.skipNumberWallSelect)
        {
            if (UI.hic.wallOptionPanel.FirstWallOptionPanel)
            {
                UI.hic.wallOptionPanel.FirstWallOptionPanel.SetActive(false);
            }
            WallConfigOptions();
            return;
        }
        PieceInfo.SetPieceInfo(AFilter.ChosenAction[(int)ChoosingTargetType]);
    }

    private static void WallConfigOptions()
    {
        SetGeneralUI(backDropColor: UI.hic.config.ConnectorModeBackground, panelColor: UI.hic.config.buildModePanelBackground, cellColor: UI.hic.config.defaultCellColor, build: false, create: true, action: false, pieceFull: false, execute: false, walls: false, secondWalls: true);
        if (UI.hic.config.skipNumberWallSelect) { UI.hic.wallOptionPanel.showWallConfigOptions(); } else { UI.hic.wallOptionPanel.showWallConfigOptions(WallNumber); }
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
}
