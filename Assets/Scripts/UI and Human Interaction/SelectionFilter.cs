// using System;
// using UnityEngine;
// using static AFilter.UIType;

// public static class UIInput
// {
//     // public static int clickedBuildPieceType;
//     // public static int clickedActionKind;
//     // public static ushort clickedCellId;
//     // public static ushort clickedWallConfig;
//     // public static int clickedWallNumber;

//     // public static void ResetClickedData()
//     // {
//     //     clickedBuildPieceType = 30;
//     //     clickedActionKind = 30;
//     //     clickedCellId = 30;
//     //     clickedWallConfig = 30;
//     //     clickedWallNumber = -1;
//     // }
//     // private static void topFilter()
//     // {
//     //     // if (uIType == UIType.EndTurnButton)
//     //     // {
//     //     //     UIBridge.PerformActionIndex(UIHelpers.FindEndTurnIndex());
//     //     //     reset();
//     //     // }
//     //     if (uIType == UIType.Cell) UI.hic.BackgroundExit.gameObject.SetActive(true);


//     //     if (uIType == UIType.Cancel)
//     //     {
//     //         reset();
//     //         return;
//     //     }

//     //     displayPieceInfo();

        

//     //     switch (state)
//     //     {
//     //         case State.Idle:
//     //         {
//     //             if (uIType == UIType.BuildItem)
//     //             {
//     //                 CreateActionFilter.Filter();
//     //                 return;
//     //             }
//     //             if (uIType == UIType.Cell && UIHelpers.HasPieceActionsForCell(clickedCellId))
//     //             {
//     //                 PieceActionFilter.Filter();
//     //                 return;
//     //             }
                
//     //             ResetClickedData();
//     //             return;
//     //         }
//     //         case State.Create:
//     //         {
//     //             CreateActionFilter.Filter();
//     //             return;
//     //         }
//     //         case State.PieceAction:
//     //         {
//     //             PieceActionFilter.Filter();
//     //             return;
//     //         }
//     //     }
//     // }

//     // public static void displayPieceInfo()
//     // {
//     //     var pieceId = UIBridge.bm.GetCellOccupant(clickedCellId);
//     //     if (AFilter.uIType == Cell && (pieceId != UIBridge.bm._invalidId))
//     //     {
//     //         if (UIBridge.bm.GetPieceOwnerFromCell(clickedCellId) == UIBridge._humanPlayer)
//     //         {
//     //             PieceInfo.SetPieceInfo(UIBridge.bm.pieceType[pieceId]);
//     //             PanelToggles.TogglePanels(build: false, create: true, action: false, pieceFull: false, execute: false, walls: false, secondWalls: false);
//     //         }
//     //     }
//     // }
   

    



//     #region DTOS
//     // public enum State
//     // {
//     //     Idle = 1,
//     //     Create = 2,
//     //     PieceAction = 3,
        
//     // }
//     // public static State state = State.Idle;
   
//     // public enum UIType
//     // {
//     //     BuildItem = 0,
//     //     NumberOfWalls = 1,
//     //     WallConfig = 2,
//     //     Cell = 3,
//     //     PieceActionKind = 4,
//     //     Cancel = 5,
//     //     EndTurnButton = 6,
//     // }
//     // public static UIType uIType;
//     #endregion

//     #region Util
    
//     // public static void reset()
//     // {
//     //     UIBridge.RebuildOffersForCurrentPlayer();
//     //     // Add Reset UI in Here to do
//     //     ShowRightPanel.PushNonPieceActionList();
//     //     ShowRightPanel.PushCreateActionMenu();

//     //     //UI 
//     //     showBoard.ClearHighlights();
//     //     showBoard.ApplyDefaultCellColor(UI.hic.config.defaultCellColor);
//     //     UIHelpers.SetBackdropColor(UI.hic.config ? UI.hic.config.buildModeBackground : new Color(0, 0, 0, 0.8f));
//     //     UIHelpers.SetPanelBackdropColor(UI.hic.config ? UI.hic.config.buildModePanelBackground : new Color(0, 0, 0, 0.8f));
//     //     PanelToggles.TogglePanels(build: true, create: false, action: true, pieceFull: false, execute: false, walls: false, secondWalls: false);
//     //     ShowLeftPanel.HudRefresh();

//     //     ResetClickedData();

//     //     EndRoundTotals.updateEndRoundTotals();

//     //     state = State.Idle;

//     //     PieceActionFilter.isKind = false;
//     //     PieceActionFilter.isPieceType = false;
//     //     PieceActionFilter.isActorCellId = false;
//     //     PieceActionFilter.isTargetCellId = false;
//     //     PieceActionFilter.isAux = false;
//     //     PieceActionFilter.isActionRequiresAux = false;
//     //     PieceActionFilter.isAddCost = false;
//     //     PieceActionFilter.ActionCostRequiresAddCost = false;

//     //     PieceActionFilter.kind = 15;
//     //     PieceActionFilter.pieceType = -1;
//     //     PieceActionFilter.TargetCell = 300;
//     //     PieceActionFilter.aux = 300;
//     //     PieceActionFilter.addCost.Clear();
//     //     PieceActionFilter.cachedLegalTargetCellId.Clear();
//     //     PieceActionFilter.cachedLegalAddCost.Clear();
//     //     PieceActionFilter.cachedLegalAux.Clear();


//     //     CreateActionFilter.isPieceType = false;
//     //     CreateActionFilter.isTargetCellId = false;
//     //     CreateActionFilter.isNumberOfWalls = false;
//     //     CreateActionFilter.isAux = false;
//     //     CreateActionFilter.ActionRequiresAux = false;
//     //     CreateActionFilter.isAddCost = false;
//     //     CreateActionFilter.ActionCostRequiresAddCost = false;

//     //     CreateActionFilter.pieceType = -1;
//     //     CreateActionFilter.TargetCell = 300;
//     //     CreateActionFilter.cachedLegalTargetCellId.Clear();
//     //     CreateActionFilter.cachedLegalAddCost.Clear();
//     //     CreateActionFilter.aux = 300;
//     //     CreateActionFilter.addCost.Clear();
//     // }

   
//     #endregion
// }
