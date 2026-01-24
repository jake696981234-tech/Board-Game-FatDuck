using static Game.Core.ActionKind;
using static UIFilter.UIType;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;
using static PieceActionFilter.UICState;

public static class AFilter
{
    public static void Filter()
    {
        if (!IsCorrectInput()) { ShowNextAction(); return; }
        if (!advaPathAndTryPerformAction()) ShowNextAction();
    }
    
    public static int[] ChosenAction = new int[6];
    public static int[] UInput = new int[7];
    public static PieceActionFilter.UICState State = ChoosingActorsCell;

    private static void ShowNextAction()
    {
        UIFilter.ResetClickedData();
        switch (State)
        {
            case ChoosingKind:
                Show.ShowActionsForAPiece();
                break;
            case ChoosingTargetCell:
                Show.ShowTargetCellOptions();
                break;
            case ChoosingTargetType:
                Show.ShowUpgradeOptions();
                break;
            case ChoosingNumberOfWalls:
                Show.NumberOfWallOptions();
                break;
            // case ChoosingWallConfig:
            //     WallConfigOptions();
            //     break;
        }
    }

    

    public static bool advaPathAndTryPerformAction()
    {
        if (State == ChoosingActorsCell) 
        {
            ChosenAction[(int)ChoosingActorsCell] = UInput[(int)Cell];
            State = ChoosingKind;
            return false;
        }
        if (State == ChoosingKind) 
        {
            ChosenAction[(int)ChoosingKind] = UInput[(int)PieceActionKind];
            State = ChoosingKind;
        }
        switch (ChosenAction[(int)ChoosingKind])
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
                        ChosenAction[(int)ChoosingTargetCell] = UInput[(int)Cell];
                        PerformAction(iNeedKind: true, iNeedActorsCell: true, iNeedTargetCell: true, TargetType: false, iNeedWallConfig: false, iNeedintakeCell: false);
                        return true;
                }
                break;
            case Create:
                //I need TargetCell, PieceType, maybe WallConfig.
                switch (State)
                {
                    case ChoosingKind:                   
                        State = ChoosingTargetCell;
                        return false;                  
                    case ChoosingTargetCell:                    
                        ChosenAction[(int)ChoosingTargetCell] = UInput[(int)Cell];
                        State = ChoosingTargetType;
                        return false;                  
                    case ChoosingTargetType:                    
                        ChosenAction[(int)ChoosingTargetType] = UInput[(int)BuildItem];
                        if (Piece.connectors_enabled[ChosenAction[(int)ChoosingTargetType]])
                        {
                            State = ChoosingWallConfig;
                            return false;
                        }
                        ChosenAction[(int)ChoosingTargetCell] = UInput[(int)Cell];
                        PerformAction(iNeedKind: true, iNeedActorsCell: false, iNeedTargetCell: true, TargetType: false, iNeedWallConfig: false, iNeedintakeCell: false);
                        return true;                   
                    case ChoosingWallConfig:                
                        ChosenAction[(int)ChoosingWallConfig] = UInput[(int)WallConfig];
                        State = ChoosingNumberOfWalls;
                        if (UI.hic.config.skipNumberWallSelect) goto case ChoosingNumberOfWalls; // to do- fix me
                        return false;
                    case ChoosingNumberOfWalls:                    
                        ChosenAction[(int)ChoosingWallConfig] = UInput[(int)WallConfig];
                        PerformAction(iNeedKind: true, iNeedActorsCell: false, iNeedTargetCell: true, TargetType: false, iNeedWallConfig: true, iNeedintakeCell: false);
                        return true;
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
                        ChosenAction[(int)ChoosingTargetType] = UInput[(int)Cell];
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
                        ChosenAction[(int)ChoosingTargetCell] = UInput[(int)Cell];
                        State = ChoosingTargetType;
                        return false;                        
                    case ChoosingTargetType:                        
                        ChosenAction[(int)ChoosingTargetType] = UInput[(int)BuildItem];
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
            if (UIBridge._offers[theAction].kind != ChosenAction[(int)ChoosingKind] && iNeedKind) continue;
            if (UIBridge._offers[theAction].ActorsCell != ChosenAction[(int)ChoosingActorsCell] && iNeedActorsCell) continue;
            if (UIBridge._offers[theAction].TargetCell != ChosenAction[(int)ChoosingTargetCell] && iNeedTargetCell) continue;
            if (UIBridge._offers[theAction].TargetType != ChosenAction[(int)ChoosingTargetType] && TargetType) continue;
            if (UIBridge._offers[theAction].WallConfig != ChosenAction[(int)ChoosingWallConfig] && iNeedWallConfig) continue;
            if (UIBridge._offers[theAction].intakeCell != ChosenAction[(int)ChoosingInstakeCellID] && iNeedintakeCell) continue;
            UIBridge.PerformActionIndex(UIBridge._offers[theAction]);
            State = ChoosingActorsCell;
        }
    }

    private static bool IsCorrectInput()
    {
        switch (State)
        {
            case ChoosingActorsCell:
                if (UIFilter.uIType != Cell) return false;
                return true;
            case ChoosingKind:
                if (UIFilter.uIType != PieceActionKind) return false;
                return true;
            case ChoosingTargetCell:
                if (UIFilter.uIType != Cell || !cachedLegalTargetCellId.Contains(UIFilter.clickedCellId)) return false;
                return true;
            case ChoosingTargetType:
                if (UIFilter.uIType != BuildItem) return false;
                return true;
            case ChoosingNumberOfWalls:
                if (UIFilter.uIType != NumberOfWalls) return false;
                return true;
            case ChoosingWallConfig:
                if (UIFilter.uIType != WallConfig) return false;
                return true;
        }
         Debug.LogWarning($"Unhandled ActionKind");
         return false;
    }



    #region show Helpers

    


    #endregion
    


}
