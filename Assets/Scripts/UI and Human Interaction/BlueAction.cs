using static Game.Core.ActionKind;
using static AFilter.UIType;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;
using static AFilter.UICState;

public static class AFilter
{
    public static void topFilter()
    {
        // if (uIType == UIType.EndTurnButton)
        // {
        //     UIBridge.PerformActionIndex(UIHelpers.FindEndTurnIndex());
        //     reset();
        // }
        if (uIType == Cell) UI.hic.BackgroundExit.gameObject.SetActive(true);
        
        bool correctInput = IsCorrectInput();
        if (!correctInput) 
        { 
            Debug.Log($"Correct Input ={correctInput}"); 
            ResetClickedData();
            return; 
        }
        Show.displayPieceInfo();
        if (!advaPathAndTryPerformAction()) ShowNextAction();
    }

    public static bool advaPathAndTryPerformAction()
    {
        switch (State)
        {
            case ChoosingActorsCell:
                if (uIType == BuildItem)
                {
                    Chosen[(int)ChoosingTargetType] = UInput[(int)BuildItem];
                    Chosen[(int)ChoosingKind] = (int)Create;
                    State = ChoosingTargetCell;
                    return false;
                }
                Chosen[(int)ChoosingActorsCell] = UInput[(int)Cell];
                State = ChoosingKind;
                return false;
            case ChoosingKind:
                Chosen[(int)ChoosingKind] = UInput[(int)PieceActionKind];
                break;
        }
        switch (Chosen[(int)ChoosingKind])
        {
            case CaptureVP:
                PerformAction(iNeedKind: true, iNeedActorsCell: false, iNeedTargetCell: false, TargetType: false, iNeedWallConfig: false, iNeedintakeCell: false);
                return true;
            case CoreDamage:
            case Explosive:
            case ConversionFactory:
                PerformAction(iNeedKind: true, iNeedActorsCell: true, iNeedTargetCell: false, TargetType: false, iNeedWallConfig: false, iNeedintakeCell: false);
                return true;
            case Move:
            case Shoot:
            case Push:
            case Sniper:
            case SacrificeFactory:
            case NecroSpawn:
                switch (State)
                {
                    //I Need ActorsCellID and TargetCell
                    case ChoosingKind:                    
                        State = ChoosingTargetCell;
                        return false;                 
                    case ChoosingTargetCell:
                        Chosen[(int)ChoosingTargetCell] = UInput[(int)Cell];
                        PerformAction(iNeedKind: true, iNeedActorsCell: true, iNeedTargetCell: true, TargetType: false, iNeedWallConfig: false, iNeedintakeCell: false);
                        return true;
                }
                break;
            case Create:
                //I need TargetCell, PieceType, maybe WallConfig.
                switch (State)
                {                 
                    case ChoosingTargetCell:                    
                        Chosen[(int)ChoosingTargetCell] = UInput[(int)Cell];
                        if (Piece.connectors_enabled[Chosen[(int)ChoosingTargetType]])
                        {
                            State = ChoosingWallConfig;
                            return false;
                        }
                        PerformAction(iNeedKind: true, iNeedActorsCell: false, iNeedTargetCell: true, TargetType: false, iNeedWallConfig: false, iNeedintakeCell: false);
                        return true;                   
                    case ChoosingWallConfig:                
                        Chosen[(int)ChoosingWallConfig] = UInput[(int)WallConfig];
                        PerformAction(iNeedKind: true, iNeedActorsCell: false, iNeedTargetCell: true, TargetType: false, iNeedWallConfig: true, iNeedintakeCell: false);
                        return false;
                }
                break;
            case Upgrade:
                //I need ActorsCellID, PieceType, maybe WallConfig.
                switch (State)
                {
                    case ChoosingKind:                    
                        State = ChoosingTargetType;
                        return false;                                       
                    case ChoosingTargetType:                   
                        Chosen[(int)ChoosingTargetType] = UInput[(int)Cell];
                        PerformAction(iNeedKind: true, iNeedActorsCell: true, iNeedTargetCell: false, TargetType: true, iNeedWallConfig: false, iNeedintakeCell: false);
                        return true;                 
                }
                break;
            case Spawner:
             //I need ActorsCellID, TargetCell, PieceType, maybe WallConfig.
                switch (State)
                {
                    case ChoosingKind:                       
                        State = ChoosingTargetCell;
                        return false;                                           
                    case ChoosingTargetCell:                        
                        Chosen[(int)ChoosingTargetCell] = UInput[(int)Cell];
                        State = ChoosingTargetType;
                        return false;                        
                    case ChoosingTargetType:                        
                        Chosen[(int)ChoosingTargetType] = UInput[(int)BuildItem];
                        PerformAction(iNeedKind: true, iNeedActorsCell: true, iNeedTargetCell: true, TargetType: true, iNeedWallConfig: false, iNeedintakeCell: false);
                        return true;                                      
                    default:                        
                        Debug.LogWarning($"Unhandled ActionKind");
                        return false;                      
                }
        }
        Debug.LogWarning($"Fix ME!");
        return false;  
    }

    private static void PerformAction(bool iNeedKind, bool iNeedActorsCell, bool iNeedTargetCell, bool TargetType, bool iNeedWallConfig, bool iNeedintakeCell)
    {
        for (int theAction = 0; theAction < UIBridge._count; theAction++)
        {
            if (UIBridge._offers[theAction].kind != Chosen[(int)ChoosingKind] && iNeedKind) continue;
            if (UIBridge._offers[theAction].ActorsCell != Chosen[(int)ChoosingActorsCell] && iNeedActorsCell) continue;
            if (UIBridge._offers[theAction].TargetCell != Chosen[(int)ChoosingTargetCell] && iNeedTargetCell) continue;
            if (UIBridge._offers[theAction].TargetType != Chosen[(int)ChoosingTargetType] && TargetType) continue;
            if (UIBridge._offers[theAction].WallConfig != Chosen[(int)ChoosingWallConfig] && iNeedWallConfig) continue;
            if (UIBridge._offers[theAction].intakeCell != Chosen[(int)ChoosingInstakeCellID] && iNeedintakeCell) continue;
            UIBridge.PerformActionIndex(UIBridge._offers[theAction]);
            State = ChoosingActorsCell;
            reset();
            return;
        }
    }

    private static bool IsCorrectInput()
    {
        if (uIType == Cancel) { Debug.Log($"Clicked Cancel"); reset();  return false; }
        switch (State)
        {
            case ChoosingActorsCell:
                if ((uIType == BuildItem && Chosen[(int)ChoosingKind] == -1) || uIType == Cell) return true;
                return true;
            case ChoosingKind:
                if (uIType == PieceActionKind) return true;
                return false;
            case ChoosingTargetCell:
                if (uIType == Cell && cachedLegalTargetCellId.Contains(UInput[(int)Cell])) return true;
                return false;
            case ChoosingTargetType:
                if (uIType == BuildItem) return true;
                return false;
            case ChoosingWallConfig:
                if (uIType == WallConfig) return true;
                return false;
        }
         Debug.LogWarning($"Unhandled ActionKind");
         return false;
    }

    private static void ShowNextAction()
    {
        ResetClickedData();
        switch (State)
        {
            case ChoosingKind:
                Show.ShowActionsForAPiece();
                return;
            case ChoosingTargetCell:
                if (Chosen[(int)ChoosingKind] == Create) { Show.ShowCreateCellOptions(); return; }
                Show.ShowTargetCellOptions();
                return;
            case ChoosingTargetType:
                Show.ShowUpgradeOptions();
                return;
            case ChoosingWallConfig:
                Show.WallConfigOptions();
                return;
        }
    }



    public static void reset()
    {
        ResetClickedData();
        for (int i = 0; i < Chosen.Length; i++) Chosen[i] = -1;

        UIBridge.RebuildOffersForCurrentPlayer();
        ShowRightPanel.ShowBuildMenu();
        ShowRightPanel.PushCreateActionMenu();

        showBoard.ClearHighlights();
        showBoard.ApplyDefaultCellColor(UI.hic.config.defaultCellColor);
        UIHelpers.SetBackdropColor(UI.hic.config ? UI.hic.config.buildModeBackground : new Color(0, 0, 0, 0.8f));
        UIHelpers.SetPanelBackdropColor(UI.hic.config ? UI.hic.config.buildModePanelBackground : new Color(0, 0, 0, 0.8f));
        PanelToggles.TogglePanels(build: true, create: false, action: true, pieceFull: false, execute: false, walls: false, secondWalls: false);
        ShowLeftPanel.HudRefresh();
        EndRoundTotals.updateEndRoundTotals();
    }

    private static void ResetClickedData()
    {
        for (int i = 0; i < UInput.Length; i++) UInput[i] = -1;
    }
    

    public enum UIType { BuildItem = 0, NumberOfWalls = 1, WallConfig = 2, Cell = 3, PieceActionKind = 4, Cancel = 5, EndTurnButton = 6, }

    #region fields
    public static UIType uIType;
    public static List<int> cachedLegalTargetCellId = new List<int>(UIBridge.bm._cellCount);
    public static int[] Chosen = new int[6];
    public static int[] UInput = new int[6];
    public static UICState State = ChoosingActorsCell;

    #endregion

    public enum UICState
    {
        ChoosingKind = 0,
        ChoosingActorsCell = 1,
        ChoosingTargetCell = 2,
        ChoosingTargetType = 3,
        ChoosingWallConfig = 4,
        ChoosingInstakeCellID = 5,
    }
}
