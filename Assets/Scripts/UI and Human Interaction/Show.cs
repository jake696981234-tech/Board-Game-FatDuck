using static Game.Core.ActionKind;
using static AFilter.UIType;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;
using static AFilter.UICState;


public static class Show
{
    public static void NextAction()
    {
        AFilter.ResetClickedData();
        switch (AFilter.State)
        {
            case ChoosingKind:
                ShowActionsForAPiece();
                return;
            case ChoosingTargetCell:
                if (AFilter.Chosen[(int)ChoosingKind] == Create) { ShowCreateCellOptions(); return; }
                ShowTargetCellOptions();
                return;
            case ChoosingTargetType:
                if (AFilter.Chosen[(int)ChoosingKind] == Spawner) { ShowSpawnerOptions(); return; }
                ShowUpgradeOptions();
                return;
            case ChoosingWallConfig:
                WallConfigOptions();
                return;
            case ChoosingIntakeCell:
                launcherFilter();
                return;
                
        }
    }
    public static void ShowActionsForAPiece()
    {
        SetGeneralUI(backDropColor: UI.hic.config.pieceActionBackground, panelColor: UI.hic.config.pieceActionPanelBackground, cellColor: UI.hic.config.defaultCellColor, build: false, create: true, action: false, pieceFull: true, execute: false, walls: false, secondWalls: false);
        PieceInfo.SetPieceInfo(UIBridge.bm.GetPieceTypeFromCell(AFilter.Chosen[(int)ChoosingActorsCell]));
        PushPieceActionListForSelection();
    }
    public static void ShowCreateCellOptions()
    {
        SetGeneralUI(backDropColor: UI.hic.config.createModeBackground, panelColor: UI.hic.config.createModePanelBackground, cellColor: UI.hic.config.defaultCellColor, build: false, create: true, action: false, pieceFull: false, execute: false, walls: false, secondWalls: false);
        showBoard.HighlightCells(GiveMeOffersContaining(GiveMe: (int)ChoosingTargetCell, Legal: true, Kind: true, ActorsCell: false, TargetCell: false, TargetType: true, WallConfig: false, intakeCell: false), UI.hic.config.createModeCellHighlight);
        PieceInfo.SetPieceInfo(AFilter.Chosen[(int)ChoosingTargetType]);
    }
    public static void ShowTargetCellOptions()
    {
        if (UI.hic.actionTitleText) UI.hic.actionTitleText.text = $"Action: {AFilter.Chosen[(int)ChoosingKind]}"; // to do, probs need fix it to enum to string
        if (UI.hic.actionPieceText) UI.hic.actionPieceText.text = $"Piece #{UIBridge.bm.occupantPieceId[AFilter.Chosen[(int)ChoosingActorsCell]]}";
        SetGeneralUI(backDropColor: UI.hic.config.actionExecuteBackground, panelColor: UI.hic.config.pieceActionPanelBackground, cellColor: UI.hic.config.defaultCellColor, build: false, create: false, action: false, pieceFull: false, execute: true, walls: false, secondWalls: false);
        // if (UI.hic.actionCostText) UI.hic.actionCostText.text = $"Cost: {action.cost}"; to do, add cost
        showBoard.HighlightCells(GiveMeOffersContaining(GiveMe: (int)ChoosingTargetCell, Legal: true, Kind: true, ActorsCell: true, TargetCell: false, TargetType: false, WallConfig: false, intakeCell: false), UI.hic.config.actionLegalTargetHighlight);
        PieceInfo.SetPieceInfo(UIBridge.bm.GetPieceTypeFromCell(AFilter.Chosen[(int)ChoosingActorsCell]));
    }
    public static void WallConfigOptions()
    {
        // UI.hic.wallOptionPanel.showNumberOfWallS(GiveMeOffersContaining(Legal: true, Kind: true, ActorsCell: false, TargetCell: true, Type: true, WallConfig: false, intakeCell: false));
        // UI.hic.wallOptionPanel.FirstWallOptionPanel.SetActive(false);
        SetGeneralUI(backDropColor: UI.hic.config.ConnectorModeBackground, panelColor: UI.hic.config.buildModePanelBackground, cellColor: UI.hic.config.defaultCellColor, build: false, create: true, action: false, pieceFull: false, execute: false, walls: false, secondWalls: true);
        UI.hic.wallOptionPanel.showWallConfigOptions(GiveMeOffersContaining(Legal: true, Kind: true, ActorsCell: false, TargetCell: true, Type: true, WallConfig: false, intakeCell: false));
        PieceInfo.SetPieceInfo(AFilter.Chosen[(int)ChoosingTargetType]);
    }
    public static void ShowSpawnerOptions()
    {
        SetGeneralUI(backDropColor: UI.hic.config.buildModeBackground, panelColor: UI.hic.config.buildModePanelBackground, cellColor: UI.hic.config.defaultCellColor, build: true, create: false, action: true, pieceFull: false, execute: false, walls: false, secondWalls: false);
        ShowBuildItemsContaining(Legal: false, Kind: true, ActorsCell: true, TargetCell: true, TargetType: false, WallConfig: false, intakeCell: false);
    }
    public static void ShowUpgradeOptions()
    {
        SetGeneralUI(backDropColor: UI.hic.config.buildModeBackground, panelColor: UI.hic.config.buildModePanelBackground, cellColor: UI.hic.config.defaultCellColor, build: true, create: false, action: true, pieceFull: false, execute: false, walls: false, secondWalls: false);
        ShowBuildItemsContaining(Legal: false, Kind: true, ActorsCell: true, TargetCell: false, TargetType: false, WallConfig: false, intakeCell: false);
    }
    private static void launcherFilter()
    {
        if (UI.hic.actionTitleText) UI.hic.actionTitleText.text = $"Action: {AFilter.Chosen[(int)ChoosingKind]}"; // to do, probs need fix it to enum to string
        if (UI.hic.actionPieceText) UI.hic.actionPieceText.text = $"Piece #{UIBridge.bm.occupantPieceId[AFilter.Chosen[(int)ChoosingActorsCell]]}";
        SetGeneralUI(backDropColor: UI.hic.config.actionExecuteBackground, panelColor: UI.hic.config.pieceActionPanelBackground, cellColor: UI.hic.config.defaultCellColor, build: false, create: false, action: false, pieceFull: false, execute: true, walls: false, secondWalls: false);
        // if (UI.hic.actionCostText) UI.hic.actionCostText.text = $"Cost: {action.cost}"; to do, add cost
        showBoard.HighlightCells(GiveMeOffersContaining(GiveMe: (int)ChoosingIntakeCell, Legal: true, Kind: true, ActorsCell: true, TargetCell: false, TargetType: false, WallConfig: false, intakeCell: false), UI.hic.config.actionLegalTargetHighlight);
        PieceInfo.SetPieceInfo(UIBridge.bm.GetPieceTypeFromCell(AFilter.Chosen[(int)ChoosingActorsCell]));
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
        var seen = new HashSet<int>();
        for (int i = 0; i < UIBridge._count; i++)
        {
            var action = UIBridge._offers[i];
            {
                if (!seen.Add(action.TargetCell)) continue;
            }
            switch (action.kind)
            {
                case EndTurn:
                case Create:
                    continue;
                case Move:
                case Shoot:
                case Launcher:
                    if (UI.hic.config.GiveRawActionOffers) break;
                    if (!seen.Add(action.kind)) continue;
                    break;
            }
            bool legal = UIBridge._mask[i] != 0;
            int kind = action.kind;
            string label = UIHelpers.PrettyAction(action);
            int cost = Mathf.RoundToInt(UIBridge._quoted[i]);
            
            items.Add(new ActionItem(i.ToString(), label, cost, legal, Array.Empty<int>(), kind));
        }
        
        UI.hic.pieceActionListFull.Show(items);
    }

    #region Helpers

    public static void ShowBuildItemsContaining(bool Legal, bool Kind, bool ActorsCell, bool TargetCell, bool TargetType, bool WallConfig, bool intakeCell)
    {
        var items = new List<Game.Core.Action>(UIBridge._count);
        var uiInfo = new List<UIInfo>(UIBridge._count);
        for (int i = 0; i < UIBridge._count; i++)
        {
            if (UIBridge._offers[i].kind != AFilter.Chosen[(int)ChoosingKind] && Kind) continue;
            if (UIBridge._offers[i].ActorsCell != AFilter.Chosen[(int)ChoosingActorsCell] && ActorsCell) continue;
            if (UIBridge._offers[i].TargetCell != AFilter.Chosen[(int)ChoosingTargetCell] && TargetCell) continue;
            if (UIBridge._offers[i].TargetType != AFilter.Chosen[(int)ChoosingTargetType] && TargetType) continue;
            if (UIBridge._offers[i].WallConfig != AFilter.Chosen[(int)ChoosingWallConfig] && WallConfig) continue;
            if (UIBridge._offers[i].IntakeCell != AFilter.Chosen[(int)ChoosingIntakeCell] && intakeCell) continue;
            bool isLegal = UIBridge._mask[i] != 0;
            if (isLegal && Legal) continue;

            items.Add(UIBridge._offers[i]);
            int fullCost = Mathf.RoundToInt(UIBridge._quoted[i]);
            uiInfo.Add(new UIInfo(isLegal, fullCost));
        }
        UI.hic.buildMenu.Show(items, UI.hic.config);
    }

    // DoesThisHave(Have: (int)ChoosingActorsCell, Legal: true, Kind: true, ActorsCell: false, TargetCell: false, Type: true, WallConfig: false, intakeCell: false)
    public static bool DoesThisPieceHaveActions(int cell)
    {
        for (int i = 0; i < UIBridge._count; i++)
        {
            if (UIBridge._offers[i].ActorsCell == cell) return true;
        }
        return false;
    }

    public static IEnumerable<int> GiveMeOffersContaining(byte GiveMe, bool Legal, bool Kind, bool ActorsCell, bool TargetCell, bool TargetType, bool WallConfig, bool intakeCell)
    {
        List<int> ReturningList = new List<int>(UIBridge.bm._cellCount);
        for (int i = 0; i < UIBridge._count; i++)
        {
            if (UIBridge._offers[i].kind != AFilter.Chosen[(int)ChoosingKind] && Kind) continue;
            if (UIBridge._offers[i].ActorsCell != AFilter.Chosen[(int)ChoosingActorsCell] && ActorsCell) continue;
            if (UIBridge._offers[i].TargetCell != AFilter.Chosen[(int)ChoosingTargetCell] && TargetCell) continue;
            if (UIBridge._offers[i].TargetType != AFilter.Chosen[(int)ChoosingTargetType] && TargetType) continue;
            if (UIBridge._offers[i].WallConfig != AFilter.Chosen[(int)ChoosingWallConfig] && WallConfig) continue;
            if (UIBridge._offers[i].IntakeCell != AFilter.Chosen[(int)ChoosingIntakeCell] && intakeCell) continue;
            if (UIBridge._mask[i] == 0 && Legal) continue;
            
            // if (action.addCost == null || action.addCost.Length == 0) continue;
            // if (!action.addCost.Contains(actorCell)) continue;
            // if (!targetCells.Contains(action.ActorsCell))
            switch (GiveMe)
            {
                case (int)ChoosingTargetCell:
                    ReturningList.Add(UIBridge._offers[i].TargetCell);
                    break;
                case (int)ChoosingWallConfig:
                    ReturningList.Add(UIBridge._offers[i].WallConfig);
                    break;
                case (int)ChoosingIntakeCell:
                    ReturningList.Add(UIBridge._offers[i].IntakeCell);
                    break;
            }
        }
        if (GiveMe == (int)ChoosingWallConfig) return ReturningList;
        AFilter.cachedLegalTargetCellId = ReturningList;
        return ReturningList;
    }

        public static IEnumerable<int> GiveMeOffersContaining(byte GiveMe, bool Legal, byte Kind, bool ActorsCell, bool TargetCell, bool Type, bool WallConfig, bool intakeCell)
        {
            int cachedKind = AFilter.Chosen[(int)ChoosingKind];
            AFilter.Chosen[(int)ChoosingKind] = Kind;
            IEnumerable<int> returningList = GiveMeOffersContaining(GiveMe: GiveMe, Legal: Legal, Kind: true, ActorsCell: ActorsCell, TargetCell: TargetCell, TargetType: Type, WallConfig: WallConfig, intakeCell: intakeCell);
            AFilter.Chosen[(int)ChoosingKind] = cachedKind;
            return returningList;
        }


        public static IEnumerable<ushort> GiveMeOffersContaining(bool Legal, bool Kind, bool ActorsCell, bool TargetCell, bool Type, bool WallConfig, bool intakeCell)
        {
            return GiveMeOffersContaining(GiveMe: (int)ChoosingWallConfig, Legal: Legal, Kind: Kind, ActorsCell: ActorsCell, TargetCell: TargetCell, TargetType: Type, WallConfig: WallConfig, intakeCell: intakeCell).Select(i => unchecked((ushort)i));
        }

        #endregion

    


    // public static void displayPieceInfo()
    // {
    //     var pieceId = UIBridge.bm.GetCellOccupant(AFilter.UInput[(int)Cell]);
    //     if (AFilter.uIType != Cell || (pieceId == UIBridge.bm._invalidId)) return;
    //     if (UIBridge.bm.GetPieceOwnerFromCell(AFilter.UInput[(int)Cell]) != UIBridge._humanPlayer) return;
    //     PieceInfo.SetPieceInfo(UIBridge.bm.pieceType[pieceId]);
    //     PanelToggles.TogglePanels(build: false, create: true, action: false, pieceFull: false, execute: false, walls: false, secondWalls: false);
    // }

    // ublic static void ShowUpgradeOptions()
    // {
    //     SetGeneralUI(backDropColor: UI.hic.config.buildModeBackground, panelColor: UI.hic.config.buildModePanelBackground, cellColor: UI.hic.config.defaultCellColor, build: true, create: false, action: true, pieceFull: false, execute: false, walls: false, secondWalls: false);
    //     var items = new List<Game.Core.Action>(UIBridge._count);
    //     var uiInfo = new List<UIInfo>(UIBridge._count);
    //     for (int i = 0; i < UIBridge._count; i++)
    //     {
    //         var theAction = UIBridge._offers[i];
    //         if (theAction.kind != Upgrade) continue;
    //         if (theAction.ActorsCell != AFilter.Chosen[(int)ChoosingActorsCell]) continue;
    //         int fullCost = Mathf.RoundToInt(UIBridge._quoted[i]);
    //         bool legal = UIBridge._mask[i] != 0;           // 1 = affordable+legal; 0 = masked out by cost, etc. :contentReference[oaicite:8]{index=8}
    //         items.Add(theAction);
    //         uiInfo.Add(new UIInfo(legal, fullCost));
    //     }
    //     UI.hic.buildMenu.Show(items, UI.hic.config);
    // }
    
}
