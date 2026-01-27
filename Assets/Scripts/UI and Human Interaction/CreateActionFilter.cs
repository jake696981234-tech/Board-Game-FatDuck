// using static Game.Core.ActionKind;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// // to do, need to figure out if the strut UIinfo still needs to be around, its not added to this new system yet

// public static class CreateActionFilter 
// {
//     const byte kind = Create;
//     public static bool isPieceType;
//     public static int pieceType;

//     public const ushort ActorCellId = 0xFFFF;

//     public static bool isTargetCellId;
//     public static ushort TargetCell;
//     public static List<int> cachedLegalTargetCellId = new List<int>(256);

    

//     public static bool isNumberOfWalls;
//     public static int WallNumber;


//     public static bool isAux;
//     public static bool ActionRequiresAux;
//     public static ushort aux;

//     public static bool isAddCost;
//     public static bool ActionCostRequiresAddCost;
//     public static List<int> addCost = new List<int>(256);
//     public static List<int> cachedLegalAddCost = new List<int>(256);

//     public static void Filter()
//     {
//         if (!isPieceType)
//         {
//             SetFilter();
//             return;
//         }
//         if (UI.hic.config.altWallSelect)
//         {
//             enterAltWallSelect();
//             return;
//         }
//         if (!isNumberOfWalls)
//         {
//             setNumberOfWalls();
//             return;
//         }
//         if (!isAux)
//         {
//             setWallConfig();
//             return;
//         }
//         if (!isAddCost)
//         {
//             setSacCost();
//             return;
//         }
//         if (!isTargetCellId)
//         {
//             setTargetCell();
//             return;
//         } 
//     }

//     public static void enterAltWallSelect()
//     {
//         if (!isTargetCellId)
//         {
//             setTargetCell();
//             return;
//         } 
//         if (!isNumberOfWalls)
//         {
//             setNumberOfWalls();
//             return;
//         }
//         if (!isAux)
//         {
//             setWallConfig();
//             return;
//         }
//         if (!isAddCost)
//         {
//             setSacCost();
//             return;
//         }
//     }

//     private static void showNextOption()
//     {
//         if (UI.hic.config.altWallSelect )
//         {
//             if (!isTargetCellId)
//             {
//                 CreateCellOptions();
//                 UIInput.ResetClickedData();
//                 return;
//             }
//             if (!isNumberOfWalls)
//             {
//                 NumberOfWallOptions();
//                 UIInput.ResetClickedData();
//                 return;
//             }
//             if (!isAux)
//             {
//                 WallConfigOptions();
//                 UIInput.ResetClickedData();
//                 return;
//             }
//             if (!isAddCost)
//             {
//                 SacrificeCostOptions(computeSacrficeTargets(kind, pieceType, ref cachedLegalAddCost), pieceType);
//                 UIInput.ResetClickedData();
//                 return;
//             }   
//         }
//         else
//         {
//             if (!isNumberOfWalls)
//             {
//                 NumberOfWallOptions();
//                 UIInput.ResetClickedData();
//                 return;
//             }
//             if (!isAux)
//             {
//                 WallConfigOptions();
//                 UIInput.ResetClickedData();
//                 return;
//             }
//             if (!isAddCost)
//             {
//                 SacrificeCostOptions(computeSacrficeTargets(kind, pieceType, ref cachedLegalAddCost), pieceType);
//                 UIInput.ResetClickedData();
//                 return;
//             }
//             if (!isTargetCellId)
//             {
//                 CreateCellOptions();
//                 UIInput.ResetClickedData();
//                 return;
//             }
//         }
//         //Perform Actions
//         if (ActionCostRequiresAddCost && !ActionRequiresAux) // to do- probs need to resort the addcost array order.
//         {
//             Game.Core.Action theAction = new Game.Core.Action(kind, (byte)pieceType, ActorCellId, TargetCell, 0, addCost.ToArray());
//             UIBridge.PerformActionIndex(theAction);
//             UIInput.reset();
//             return;
//         }
//         if (ActionCostRequiresAddCost && ActionRequiresAux)
//         {
//             Game.Core.Action theAction = new Game.Core.Action(kind, (byte)pieceType, ActorCellId, TargetCell, aux, addCost.ToArray());
//             UIBridge.PerformActionIndex(theAction);
//             UIInput.reset();
//             return;
//         }
//         if (!ActionCostRequiresAddCost && ActionRequiresAux)
//         {
//             Game.Core.Action theAction = new Game.Core.Action(kind, (byte)pieceType, ActorCellId, TargetCell, aux);
//             UIBridge.PerformActionIndex(theAction);
//             UIInput.reset();
//             return;
//         }
//         if (!ActionCostRequiresAddCost && !ActionRequiresAux)
//         {
//             Game.Core.Action theAction = new Game.Core.Action(kind, (byte)pieceType, ActorCellId, TargetCell);
//             UIBridge.PerformActionIndex(theAction);
//             UIInput.reset();
//             return;
//         }
//         Debug.Log($"showNextOption failed this is very unexpected");
//     }

//     private static void SetFilter()
//     {
//         UIInput.state = UIInput.State.Create;

//         pieceType = UIInput.clickedBuildPieceType;
//         isPieceType = true;
        
//         if (!Piece.connectors_enabled[UIInput.clickedBuildPieceType])
//         {
//             isAux = true;
//             isNumberOfWalls = true;
//         }
//         if (!Piece.sacrificeCost_enabled[pieceType]) isAddCost = true;

//         cleanUpSet();
//     }

//     private static void setNumberOfWalls()
//     {
//         if (UIInput.uIType != UIInput.UIType.NumberOfWalls)
//         {
//             UIInput.ResetClickedData();
//             return;
//         }
//         WallNumber = UIInput.clickedWallNumber;
//         isNumberOfWalls = true;
//         cleanUpSet();
//     }

//     private static void setWallConfig()
//     {
//         if (UIInput.uIType != UIInput.UIType.WallConfig)
//         {
//             UIInput.ResetClickedData();
//             return;
//         }
//         aux = UIInput.clickedWallConfig;
//         ActionRequiresAux = true;
        
//         isAux = true;
//         cleanUpSet();
//     }

//     private static void setSacCost()
//     {
//         if (UIInput.uIType != UIInput.UIType.Cell || !cachedLegalAddCost.Contains(UIInput.clickedCellId))
//         {
//             UIInput.ResetClickedData();
//             return;
//         } 
//         addCost.Add(UIBridge.bm.occupantPieceId[UIInput.clickedCellId]);
//         ActionCostRequiresAddCost = true;
//         if (addCost.Count == Piece.sacrificeCost_howManyItNeeds[pieceType])
//         {
//             addCost.Sort();
//             addCost.Reverse();
//             isAddCost = true;
//         } 
//         cleanUpSet();
//     }
    
//     private static void setTargetCell()
//     {
//         if (UIInput.uIType != UIInput.UIType.Cell || !cachedLegalTargetCellId.Contains(UIInput.clickedCellId))
//         {
//             UIInput.ResetClickedData();
//             return;
//         }
//         TargetCell = UIInput.clickedCellId;
//         isTargetCellId = true;
//         cleanUpSet();
//     }

//     private static void cleanUpSet()
//     {
//         UIInput.ResetClickedData();
//         showNextOption();
//     }
    
    
//      private static void CreateCellOptions()
//     {
//         showBoard.ClearHighlights();
//         showBoard.ApplyDefaultCellColor(UI.hic.config.defaultCellColor);
        
//         showBoard.HighlightCells(computeCreateCellOptions(), UI.hic.config.createModeCellHighlight);
//         PieceInfo.SetPieceInfo(pieceType);
//         PanelToggles.TogglePanels(build: false, create: true, action: false, pieceFull: false, execute: false, walls: false, secondWalls: false); 

//         UIHelpers.SetBackdropColor(UI.hic.config.createModeBackground);
//         UIHelpers.SetPanelBackdropColor(UI.hic.config.createModePanelBackground);
//         ShowLeftPanel.HudRefresh();
//     }
//     // need to add go back to defualt Option
//     private static void NumberOfWallOptions()
//     {
//         showBoard.ClearHighlights();
//         UI.hic.wallOptionPanel.showNumberOfWallS(ComputeWallOptions());
//         UIHelpers.SetBackdropColor(UI.hic.config.ConnectorModeBackground);
//         if (UI.hic.config.skipNumberWallSelect)
//         {
//             isNumberOfWalls = true;
//             if (UI.hic.wallOptionPanel.FirstWallOptionPanel)
//             {
//                 UI.hic.wallOptionPanel.FirstWallOptionPanel.SetActive(false);
//             }
//             WallConfigOptions();
//             return;
//         }
//         PieceInfo.SetPieceInfo(pieceType);
//         PanelToggles.TogglePanels(build: false, create: true, action: false, pieceFull: false, execute: false, walls: true, secondWalls: false);
//     }

//     private static void WallConfigOptions()
//     {
//         showBoard.ClearHighlights();
//         if (UI.hic.config.skipNumberWallSelect) { UI.hic.wallOptionPanel.showWallConfigOptions(); } else { UI.hic.wallOptionPanel.showWallConfigOptions(WallNumber); }
//         PieceInfo.SetPieceInfo(pieceType);
//         PanelToggles.TogglePanels(build: false, create: true, action: false, pieceFull: false, execute: false, walls: false, secondWalls: true);
//     }

//     public static void SacrificeCostOptions(IEnumerable<int> Targets, int inPieceType)
//     {
//         showBoard.ClearHighlights();
//         showBoard.ApplyDefaultCellColor(UI.hic.config.defaultCellColor);

//         showBoard.HighlightCells(Targets, UI.hic.config.SacrificeCostCellHighlight);
//         PieceInfo.SetPieceInfo(pieceType);
//         PanelToggles.TogglePanels(build: false, create: true, action: false, pieceFull: false, execute: false, walls: false, secondWalls: false); // could change this to sac specfic

//         UIHelpers.SetBackdropColor(UI.hic.config.createModeBackground);
//         UIHelpers.SetPanelBackdropColor(UI.hic.config.createModePanelBackground);
//         if (UI.hic.createTitleText) UI.hic.createTitleText.text = $"Choose Sacrfice/s for: {Piece.name[inPieceType]}";
//         if (UI.hic.createCostText) UI.hic.createCostText.text = $"Cost: {Piece.BuildCost[inPieceType]}"; //to do, this does not show full cost
//         if (UI.hic.createSprite)
//         {
//             var s = !string.IsNullOrEmpty(Piece.spritePath[inPieceType]) ? Resources.Load<Sprite>(Piece.spritePath[inPieceType]) : null;
//             UI.hic.createSprite.sprite = s;
//             UI.hic.createSprite.enabled = (s != null);
//         }

//         ShowLeftPanel.HudRefresh();
//     }

   

    
//     private static IEnumerable<ushort> ComputeWallOptions()
//     {
//         List<ushort> wallConfigs = new List<ushort>(64);
//         for (int i = 0; i < UIBridge._count; i++)
//         {
//             var actions = UIBridge._offers[i];
//             if (actions.kind != Create) continue;   // byte code
//             if (actions.pieceType != pieceType) continue;
//             if (UIBridge._mask[i] == 0) continue; // masked out = illegal/unaffordable
//             if (UI.hic.config.altWallSelect)
//             {
//                 if (actions.TargetCell != TargetCell) continue;   // byte code
//             }
//             wallConfigs.Add(actions.aux);
//         }
//         return wallConfigs;
//     }

//     public static IEnumerable<int> computeSacrficeTargets(byte inKind, int inPieceType, ref List<int> cachedLegalTargets)
//     {
//         List<int> SacrficeTargets = new List<int>(128);
//         var seen = new HashSet<int>();
//         var bm = UIBridge.bm;
//         for (int i = 0; i < UIBridge._count; i++)
//         {
//             var actions = UIBridge._offers[i];
//             if (actions.kind != inKind) continue;
//             if (actions.pieceType != inPieceType) continue;
//             if (UIBridge._mask[i] == 0) continue;

//             if (actions.addCost == null || actions.addCost.Length == 0) continue;

//             if (Piece.sacrificeCost_isNeedsSpecificPiece[inPieceType])
//             {
//                 int requiredType = Piece.sacrificeCost_specificPiece[inPieceType];
//                 bool hasRequired = false;
//                 for (int b = 0; b < actions.addCost.Length; b++)
//                 {
//                     if (bm.pieceType[actions.addCost[b]] == requiredType)
//                     {
//                         hasRequired = true;
//                         break;
//                     }
//                 }
//                 if (!hasRequired) continue;
//             }
            
//             for (int b = 0; b < actions.addCost.Length; b++)
//             {
//                 if (!seen.Add(actions.addCost[b])) continue; // skip duplicates
//                 SacrficeTargets.Add(bm.pieceCellId[actions.addCost[b]]); 
//             }
//         }
//         cachedLegalTargets = SacrficeTargets;
//         return SacrficeTargets;
//     } 

//     private static IEnumerable<int> computeCreateCellOptions()
//     {
//         List<int> CreateCellOptions = new List<int>(128);
//         for (int i = 0; i < UIBridge._count; i++)
//         {
//             var theAction = UIBridge._offers[i];
//             if (theAction.kind != Create) continue;   // byte code

//             if (Piece.connectors_enabled[pieceType] && !UI.hic.config.altWallSelect)
//             {
//                 if (theAction.aux != aux) continue;
//             }

//             if (theAction.pieceType != pieceType) continue;
//             if (UIBridge._mask[i] == 0) continue; // masked out = illegal/unaffordable
//             CreateCellOptions.Add(theAction.TargetCell);
//         }
//         cachedLegalTargetCellId = CreateCellOptions;
//         return CreateCellOptions;
//     }
// }
