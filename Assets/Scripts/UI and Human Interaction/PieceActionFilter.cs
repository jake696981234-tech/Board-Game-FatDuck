using static Game.Core.ActionKind;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;

public static class PieceActionFilter
{
    public static bool isKind;
    public  static byte kind;

    public static bool isPieceType;
    public static int pieceType;

    public static bool isActorCellId;
    public static ushort ActorsCellId;

    public static bool isTargetCellId;
    public static ushort TargetCellId;
    public static List<int> cachedLegalTargetCellId = new List<int>(256);


    public static bool isAux;
    public static bool isActionRequiresAux = false;
    public static ushort aux;
    public static List<int> cachedLegalAux = new List<int>(256);

    public static bool isAddCost;
    public static bool ActionCostRequiresAddCost = false;
    public static List<int> addCost = new List<int>(256);
    public static List<int> cachedLegalAddCost = new List<int>(256);


    public static void Filter()
    {
        if (!isPieceType)
        {
            setActorCellIdAndPieceType();
            return;
        }
        if (!isKind)
        {
            setActionKind();
            return;
        }
        pieceKindSwitchBoard();
    }

    private static void pieceKindSwitchBoard()
    {
        switch (kind)
        {
            case Move:
                SetMoveFilter();
                return;
            case Shoot:
            case Push:
            case SacrificeFactory:
                SetShootKindFilter();
                return;
             case Upgrade:
                UpgradeFilter();
                return;
            case Spawner:
                SetSpawnerFilter();
                return;
            case Launcher:
                launcherFilter();
                return;
            case CaptureVP:
            case CoreDamage:
            case ConversionFactory:
                SetOneInputKindFilter();
                return;
            case GroupBuild:
                GroupBuildFilter();
                return;
        }
        Debug.Log($"Missing Ability Kind Filter {kind}");
        UIFilter.reset();
    }

    private static void setActorCellIdAndPieceType()
    {
        UIFilter.state = UIFilter.State.PieceAction;

        ActorsCellId = UIFilter.clickedCellId;
        isActorCellId = true;

        pieceType = UIBridge.bm.GetPieceTypeFromCell(UIFilter.clickedCellId);
        isPieceType = true;
        
        UIFilter.ResetClickedData();
        showNextActionOption();
    }

    private static void setActionKind()
    {
        if (UIFilter.uIType != UIFilter.UIType.PieceActionKind)
            {
                UIFilter.ResetClickedData();
                return;
            }
            kind = UIFilter.clickedActionKind;
            isKind = true;

            UIFilter.ResetClickedData();
            showNextActionOption();
    }

    

    private static void launcherFilter()
    {
        if (!isAddCost) isAddCost = true;

        if (!isTargetCellId)
        {
            SetTargetCellIdToClickedCell();
            return;
        }

        if (!isAux)
        {
           SetAuxForLauncher();
           return;
        }
        UIFilter.ResetClickedData();
        showNextActionOption();
    }



    private static void SetTargetCellIdToClickedCell()
    {
        if (UIFilter.uIType != UIFilter.UIType.Cell || !cachedLegalTargetCellId.Contains(UIFilter.clickedCellId))
        {
            UIFilter.ResetClickedData();
            return;
        }
        TargetCellId = UIFilter.clickedCellId;
        isTargetCellId = true;

        UIFilter.ResetClickedData();
        showNextActionOption();
    }

    private static void SetAuxForLauncher()
    {
         if (UIFilter.uIType != UIFilter.UIType.Cell  || !cachedLegalAux.Contains(UIFilter.clickedCellId))
        {
            UIFilter.ResetClickedData();
            return;
        }
        aux = (ushort)UIBridge.bm.occupantPieceId[UIFilter.clickedCellId];
        isActionRequiresAux = true;
        isAux = true;
        UIFilter.ResetClickedData();
        showNextActionOption();
    }


    private static void SetMoveFilter()
    {
        if (!isAux) isAux = true;
        if (!isAddCost) isAddCost = true;

        if (!isTargetCellId)
        {
            if (UIFilter.uIType != UIFilter.UIType.Cell || !cachedLegalTargetCellId.Contains(UIFilter.clickedCellId))
            {
                UIFilter.ResetClickedData();
                return;
            }
            TargetCellId = UIFilter.clickedCellId;
            isTargetCellId = true;

            UIFilter.ResetClickedData();
            showNextActionOption();
            return;
        }
    }

    private static void SetSpawnerFilter()
    {
        if (!isAux) isAux = true;
        if (!isAddCost) isAddCost = true;

        if (!isTargetCellId)
        {
            if (UIFilter.uIType != UIFilter.UIType.Cell || !cachedLegalTargetCellId.Contains(UIFilter.clickedCellId))
            {
                UIFilter.ResetClickedData();
                return;
            }
            TargetCellId = UIFilter.clickedCellId;
            aux = (ushort)Piece.spawn_targetType[pieceType];
            isActionRequiresAux = true;

            isTargetCellId = true;
        }
        UIFilter.ResetClickedData();
        showNextActionOption();
    }
    private static void SetShootKindFilter()
    {
        if (!isAux) isAux = true;
        if (!isAddCost) isAddCost = true;

        if (!isTargetCellId)
        {
            if (UIFilter.uIType != UIFilter.UIType.Cell || !cachedLegalTargetCellId.Contains(UIFilter.clickedCellId))
            {
                UIFilter.ResetClickedData();
                return;
            }
            TargetCellId = UIFilter.clickedCellId;
            aux = (ushort)UIBridge.bm.occupantPieceId[TargetCellId];
            isActionRequiresAux = true;
            isTargetCellId = true;
        }
        UIFilter.ResetClickedData();
        showNextActionOption();
        return;
    }

    private static void UpgradeFilter()
    {
        // TargetCellId = (ushort)PieceDefinition.upgrade_target[UIFilter.clickedCellId];
        // isTargetCellId = true;
        isAux = true;
        

        if (!isTargetCellId)
        {
            if (UIFilter.uIType != UIFilter.UIType.BuildItem)
            {
                UIFilter.ResetClickedData();
                return;
            }
            TargetCellId = UIFilter.clickedBuildPieceType;
            isTargetCellId = true;

            UIFilter.ResetClickedData();
            showNextActionOption();
            return;
        }

        if (!Piece.sacrificeCost_enabled[TargetCellId]) isAddCost = true;
        if (!isAddCost)
        {
            if (UIFilter.uIType != UIFilter.UIType.Cell  || !cachedLegalAddCost.Contains(UIFilter.clickedCellId))
            {
                UIFilter.ResetClickedData();
                return;
            } 
            addCost.Add(UIBridge.bm.occupantPieceId[UIFilter.clickedCellId]);
            ActionCostRequiresAddCost = true;
            if (addCost.Count == Piece.sacrificeCost_howManyItNeeds[TargetCellId])
            {
                addCost.Sort();
                addCost.Reverse();
                isAddCost = true;
            } 
        }
        UIFilter.ResetClickedData();
        showNextActionOption();
    }

    private static void GroupBuildFilter()
    {
        isAux = true;
        isTargetCellId = true;

        if (!isAddCost)
        {
            if (UIFilter.uIType != UIFilter.UIType.Cell  || !cachedLegalAddCost.Contains(UIFilter.clickedCellId))
            {
                Debug.Log($"Does it contain {!cachedLegalAddCost.Contains(UIFilter.clickedCellId)}");
                Debug.Log($"Right UI type {UIFilter.uIType != UIFilter.UIType.Cell}");
                UIFilter.ResetClickedData();
                return;
            } 
            addCost.Add(UIFilter.clickedCellId);
            ActionCostRequiresAddCost = true;
            if (addCost.Count == Piece.groupBuild_requireNumber[pieceType])
            {
                addCost.Sort();
                addCost.Reverse();
                isAddCost = true;
            } 
        }
        UIFilter.ResetClickedData();
        showNextActionOption();
    }

    private static void SetOneInputKindFilter()
    {
        isAux = true;
        isAddCost = true;
        isTargetCellId = true;

        UIFilter.ResetClickedData();
        showNextActionOption();
    }


    

    private static void showNextActionOption() 
    {
        if (!isKind)
        {
            PieceKindOptions();
            UIFilter.ResetClickedData();
            return;
        }

        if (!isTargetCellId)
        {
            if (kind == GroupBuild)
            {
                isTargetCellId = true;
                CreateActionFilter.SacrificeCostOptions(computeGroupBuildDeletionTargets(), pieceType);
                UIFilter.ResetClickedData();
                return;
            }
            if (kind == Upgrade)
            {
                ShowUpgradeOptions();
                UIFilter.ResetClickedData();
                return;
            }
            TargetCellIdsOptions();
            UIFilter.ResetClickedData();
            return;
        }

        if (!isAux)
        {
            secondTargetlauncherOptions();
            UIFilter.ResetClickedData();
            return;
        }

        if (!isAddCost)
        {
            if (kind == GroupBuild)
            {
                CreateActionFilter.SacrificeCostOptions(computeGroupBuildDeletionTargets(), pieceType);
                UIFilter.ResetClickedData();
                return;
            } 
            CreateActionFilter.SacrificeCostOptions(CreateActionFilter.computeSacrficeTargets(kind, pieceType, ref cachedLegalAddCost), pieceType);
            UIFilter.ResetClickedData();
            return;
        }

        if (kind == Upgrade) (pieceType, TargetCellId) = (TargetCellId, (ushort)pieceType);

        if (kind == GroupBuild)
        {
            var store = pieceType;
            pieceType = Piece.groupBuild_target[store];
            TargetCellId = (ushort)store;
        }



        if (ActionCostRequiresAddCost && !isActionRequiresAux) // to do- probs need to resort the addcost array order. Look at -case PanelToggles.Mode.SacrificeSelect:- Inside old UIInput, Could be use full code that does this, and few ther essetentials.   
        {
            Game.Core.Action theAction = new Game.Core.Action(kind, (byte)pieceType, ActorsCellId, TargetCellId, 0, addCost.ToArray());
            UIBridge.PerformActionIndex(theAction);
            UIFilter.reset();
            return;
        }
        if (ActionCostRequiresAddCost && isActionRequiresAux)
        {
            Game.Core.Action theAction = new Game.Core.Action(kind, (byte)pieceType, ActorsCellId, TargetCellId, aux, addCost.ToArray());
            UIBridge.PerformActionIndex(theAction);
            UIFilter.reset();
            return;
        }
        if (!ActionCostRequiresAddCost && isActionRequiresAux)
        {
            Game.Core.Action theAction = new Game.Core.Action(kind, (byte)pieceType, ActorsCellId, TargetCellId, aux);
            UIBridge.PerformActionIndex(theAction);
            UIFilter.reset();
            return;
        }
        if (!ActionCostRequiresAddCost && !isActionRequiresAux)
        {
            Game.Core.Action theAction = new Game.Core.Action(kind, (byte)pieceType, ActorsCellId, TargetCellId);
            UIBridge.PerformActionIndex(theAction);
            UIFilter.reset();
            return;
        }
        Debug.Log($"showNextActionOption failed this is very unexpected");
    }

    public static IEnumerable<int> computeGroupBuildDeletionTargets()
    {
        List<int> deletionTargets = new List<int>(128);
        var seen = new HashSet<int>();
        for (int i = 0; i < UIBridge._count; i++)
        {
            var actions = UIBridge._offers[i];
            if (actions.kind != kind) continue;
            if (pieceType != actions.TargetCellId) continue;
            if (ActorsCellId != actions.ActorsCellId) continue;
            if (addCost.Count > 0)
            {
                for (int c = 0; c < addCost.Count; c++)
                {
                    if (!actions.addCost.Contains(addCost[c])) continue;
                }
            } 
            
            for (int b = 0; b < actions.addCost.Length; b++)
            {
                if (!seen.Add(actions.addCost[b])) continue; // skip duplicates
                deletionTargets.Add(actions.addCost[b]); 
            }
        }
        cachedLegalAddCost = deletionTargets;
        return deletionTargets;
    } 



    private static void secondTargetlauncherOptions() // this is for aux, probs could work for more than just the launcher
    {
        showBoard.ClearHighlights();

        UIHelpers.SetBackdropColor(UI.hic.config.actionExecuteBackground);
        UIHelpers.SetPanelBackdropColor(UI.hic.config.actionExecutePanelBackground);

        PanelToggles.TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: true, walls: false, secondWalls: false);

        showBoard.HighlightCells(ComputeAuxCellsForAction(), UI.hic.config.actionLegalTargetHighlight);

        ShowLeftPanel.HudRefresh();
    }

    private static void PieceKindOptions()
    {
        showBoard.ClearHighlights();
        showBoard.ApplyDefaultCellColor(UI.hic.config.defaultCellColor);
        UIHelpers.SetBackdropColor(UI.hic.config.pieceActionBackground);
        UIHelpers.SetPanelBackdropColor(UI.hic.config.pieceActionPanelBackground);
        PanelToggles.TogglePanels(build: false, create: false, action: false, pieceFull: true, execute: false, walls: false, secondWalls: false);

        PushPieceActionListForSelection();

        ShowLeftPanel.HudRefresh();
    }

    private static void ShowUpgradeOptions()
    {
        showBoard.ClearHighlights();
        showBoard.ApplyDefaultCellColor(UI.hic.config.defaultCellColor);
        UIHelpers.SetBackdropColor(UI.hic.config ? UI.hic.config.buildModeBackground : new Color(0, 0, 0, 0.8f));
        UIHelpers.SetPanelBackdropColor(UI.hic.config ? UI.hic.config.buildModePanelBackground : new Color(0, 0, 0, 0.8f));
        PanelToggles.TogglePanels(build: true, create: false, action: true, pieceFull: false, execute: false, walls: false, secondWalls: false);
        ShowLeftPanel.HudRefresh();
        var items = new List<Game.Core.Action>(UIBridge._count);
        var uiInfo = new List<UIInfo>(UIBridge._count);
        for (int i = 0; i < UIBridge._count; i++)
        {
            var theAction = UIBridge._offers[i];
            if (theAction.kind != Upgrade) continue;
            if (theAction.TargetCellId != pieceType) continue;
            if (theAction.ActorsCellId != ActorsCellId) continue;

            int fullCost = Mathf.RoundToInt(UIBridge._quoted[i]);
            bool legal = UIBridge._mask[i] != 0;           // 1 = affordable+legal; 0 = masked out by cost, etc. :contentReference[oaicite:8]{index=8}

            items.Add(theAction);
            uiInfo.Add(new UIInfo(legal, fullCost));
        }
        UI.hic.buildMenu.Show(items, uiInfo, UI.hic.config);
    }

    

    private static void TargetCellIdsOptions()
    {
        showBoard.ClearHighlights();

        UIHelpers.SetBackdropColor(UI.hic.config.actionExecuteBackground);
        UIHelpers.SetPanelBackdropColor(UI.hic.config.actionExecutePanelBackground);

        PanelToggles.TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: true, walls: false, secondWalls: false);

        showBoard.HighlightCells(ComputeTargetCellsForAction(), UI.hic.config.actionLegalTargetHighlight);

        if (UI.hic.actionTitleText) UI.hic.actionTitleText.text = $"Action: {kind}"; // to do, probs need fix it to enum to string
        if (UI.hic.actionPieceText) UI.hic.actionPieceText.text = $"Piece #{UIBridge.bm.occupantPieceId[ActorsCellId]}";
        // if (UI.hic.actionCostText) UI.hic.actionCostText.text = $"Cost: {action.cost}"; to do, add cost

        ShowLeftPanel.HudRefresh();
    }



    private static IEnumerable<int> ComputeTargetCellsForAction()
    {
        if (kind == GroupBuild) return ComputeGroupBuildTargetCells();

        List<int> targetCells = new List<int>(128);

        for (int i = 0; i < UIBridge._count; i++)
        {
            var action = UIBridge._offers[i];
            if (action.kind != kind) continue;
            if (action.ActorsCellId != ActorsCellId) continue;
            if (action.pieceType != pieceType) continue;
            if (UIBridge._mask[i] == 0) continue; // masked out = illegal

            targetCells.Add(action.TargetCellId);
        }
        cachedLegalTargetCellId = targetCells;
        return targetCells;
    }

    private static IEnumerable<int> ComputeGroupBuildTargetCells()
    {
        List<int> targetCells = new List<int>(128);
        int actorCell = ActorsCellId;
        byte actorType = (byte)pieceType;

        for (int i = 0; i < UIBridge._count; i++)
        {
            var action = UIBridge._offers[i];
            if (action.kind != GroupBuild) continue;
            if (UIBridge._mask[i] == 0) continue;
            if (action.TargetCellId != actorType) continue;
            if (action.addCost == null || action.addCost.Length == 0) continue;
            if (!action.addCost.Contains(actorCell)) continue;
            if (!targetCells.Contains(action.ActorsCellId))
                targetCells.Add(action.ActorsCellId);
        }
        cachedLegalTargetCellId = targetCells;
        return targetCells;
    }

   

    private static IEnumerable<int> ComputeAuxCellsForAction()
    {
        List<int> targetAuxCells = new List<int>(128);

        for (int i = 0; i < UIBridge._count; i++)
        {
            var action = UIBridge._offers[i];
            if (action.kind != kind) continue;
            if (action.ActorsCellId != ActorsCellId) continue;
            if (action.pieceType != pieceType) continue;
            if (action.TargetCellId != TargetCellId) continue;
            if (UIBridge._mask[i] == 0) continue; // masked out = illegal

            targetAuxCells.Add(UIBridge.bm.pieceCellId[action.aux]);
        }
        cachedLegalAux = targetAuxCells;
        return targetAuxCells;
    }

    
    
    
    

    private static void PushPieceActionListForSelection()
    {
        var items = new List<ActionItem>();
        bool moveAddedForCell = false;
        
        for (int i = 0; i < UIBridge._count; i++)
        {
            var action = UIBridge._offers[i];
            if (action.kind == Game.Core.ActionKind.EndTurn) continue; // exclude non-piece actions
            if (action.kind == GroupBuild)
            {
                if (!IsGroupBuildForSelection(action, UIBridge._mask[i])) continue;
            }
            else
            {
                if (action.ActorsCellId != (ushort)ActorsCellId) continue;               // only actions from this piece
                // Upgrade actions carry destination type in pieceType; bypass strict type check for upgrades
                if (action.kind != Upgrade && action.pieceType != pieceType) continue;
            }

            // Show only one Move per selected piece unless raw offers requested
            if (action.kind == Game.Core.ActionKind.Move && !UI.hic.config.GiveRawActionOffers)
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

    //Buildings
    //Factions
    //Legal

    private static bool IsGroupBuildForSelection(Game.Core.Action action, byte mask)
    {
        if (mask == 0) return false;
        int actorCell = ActorsCellId;
        byte actorType = (byte)pieceType;
        if (action.TargetCellId != actorType) return false;
        if (action.addCost == null || action.addCost.Length == 0) return false;
        return action.addCost.Contains(actorCell);
    }

}
