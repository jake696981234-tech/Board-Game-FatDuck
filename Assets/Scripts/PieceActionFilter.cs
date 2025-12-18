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
    public static ushort ActorCellId;

    public static bool isTargetCellId;
    public static ushort TargetCellId;
    public static int[] cachedLegalTargetCellId;

    public static bool isAux;
    public static bool ActionRequiresAux;
    public static ushort aux;
    public static int[] cachedLegalAux;
    

    public static bool isAddCost;
    public static bool ActionCostRequiresAddCost;
    public static int[] addCost; 
    public static int[] cachedLegalAddCost;



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
                break;
            case Shoot:
            case Push:
            case SacrificeFactory:
                SetShootKindFilter();
                break;
             case Upgrade:
                UpgradeFilter();
                break;
            case CaptureVP:
            case CoreDamage:
            case ConversionFactory:
                SetOneInputKindFilter();
                break;
            case Spawner:
                SetSpawnerFilter();
                break;
            case Launcher:
                launcherFilter();
                break;
        }
    }

    private static void setActorCellIdAndPieceType()
    {
        UIFilter.state = UIFilter.State.PieceAction;

        ActorCellId = UIFilter.clickedCellId;
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
        if (UIFilter.uIType != UIFilter.UIType.Cell || cachedLegalTargetCellId.Contains(UIFilter.clickedCellId))
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
         if (UIFilter.uIType != UIFilter.UIType.Cell  || cachedLegalAux.Contains(UIFilter.clickedCellId))
        {
            UIFilter.ResetClickedData();
            return;
        }
        aux = (ushort)UIBridge.bm.occupantPieceId[UIFilter.clickedCellId];
        ActionRequiresAux = true;
        isAux = true;
    }


    private static void SetMoveFilter()
    {
        if (!isAux) isAux = true;
        if (!isAddCost) isAddCost = true;

        if (!isTargetCellId)
        {
            if (UIFilter.uIType != UIFilter.UIType.Cell || cachedLegalTargetCellId.Contains(UIFilter.clickedCellId))
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
            if (UIFilter.uIType != UIFilter.UIType.Cell || cachedLegalTargetCellId.Contains(UIFilter.clickedCellId))
            {
                UIFilter.ResetClickedData();
                return;
            }
            TargetCellId = UIFilter.clickedCellId;
            aux = (ushort)PieceDefinition.spawn_targetType[pieceType];
            ActionRequiresAux = true;

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
            if (UIFilter.uIType != UIFilter.UIType.Cell || cachedLegalTargetCellId.Contains(UIFilter.clickedCellId))
            {
                UIFilter.ResetClickedData();
                return;
            }
            TargetCellId = UIFilter.clickedCellId;
            aux = (ushort)UIBridge.bm.occupantPieceId[TargetCellId];
            ActionRequiresAux = true;
            isTargetCellId = true;
        }
        UIFilter.ResetClickedData();
        showNextActionOption();
        return;
    }

    private static void UpgradeFilter()
    {
        if (!isAux) isAux = true;
        
        if (!isTargetCellId)
        {
            if (UIFilter.uIType != UIFilter.UIType.Cell || cachedLegalTargetCellId.Contains(UIFilter.clickedCellId))
            {
                UIFilter.ResetClickedData();
                return;
            }
            TargetCellId = (ushort)UIBridge.bm.GetPieceTypeFromCell(UIFilter.clickedCellId);
            isTargetCellId = true;

            if (!PieceDefinition.sacrificeCost_enabled[TargetCellId])
            {
                if (!isAddCost) isAddCost = true;
            }

            UIFilter.ResetClickedData();
            showNextActionOption();
            return;
        }

        if (!isAddCost)
        {
            if (UIFilter.uIType != UIFilter.UIType.Cell  || cachedLegalAddCost.Contains(UIFilter.clickedCellId))
            {
                UIFilter.ResetClickedData();
                return;
            } 
            addCost[addCost.Length] = UIBridge.bm.occupantPieceId[UIFilter.clickedCellId];
            ActionCostRequiresAddCost = true;
            if (addCost.Length == PieceDefinition.sacrificeCost_howManyItNeeds[pieceType]) isAddCost = true;
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


    

    private static void showNextActionOption() // to do
    {
        if (!isKind)
        {
            PieceKindOptions();
            UIFilter.ResetClickedData();
            return;
        }

        if (!isTargetCellId)
        {
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
            SacrificeCostOptions();
            UIFilter.ResetClickedData();
            return;
        }

        if (ActionCostRequiresAddCost && !ActionRequiresAux) // to do- probs need to resort the addcost array order.
        {
            Game.Core.Action theAction = new Game.Core.Action(kind, (byte)pieceType, ActorCellId, TargetCellId, 0, addCost);
            UIBridge.PerformActionIndex(theAction);
            return;
        }
        if (ActionCostRequiresAddCost && ActionRequiresAux)
        {
            Game.Core.Action theAction = new Game.Core.Action(kind, (byte)pieceType, ActorCellId, TargetCellId, aux, addCost);
            UIBridge.PerformActionIndex(theAction);
            return;
        }
        if (!ActionCostRequiresAddCost && ActionRequiresAux)
        {
            Game.Core.Action theAction = new Game.Core.Action(kind, (byte)pieceType, ActorCellId, TargetCellId, aux);
            UIBridge.PerformActionIndex(theAction);
            return;
        }
        if (!ActionCostRequiresAddCost && !ActionRequiresAux)
        {
            Game.Core.Action theAction = new Game.Core.Action(kind, (byte)pieceType, ActorCellId, TargetCellId);
            UIBridge.PerformActionIndex(theAction);
            return;
        }
        Debug.Log($"showNextActionOption failed this is very unexpected");
    }



    public static void SacrificeCostOptions()
    {
        showBoard.ClearHighlights();
        showBoard.ApplyDefaultCellColor(UI.hic.config.defaultCellColor);

        showBoard.HighlightCells(computeSacrficeTargets(), UI.hic.config.SacrificeCostCellHighlight);
        PanelToggles.TogglePanels(build: false, create: true, action: false, pieceFull: false, execute: false, walls: false, secondWalls: false); // could change this to sac specfic

        UIHelpers.SetBackdropColor(UI.hic.config.createModeBackground);
        UIHelpers.SetPanelBackdropColor(UI.hic.config.createModePanelBackground);
        if (UI.hic.createTitleText) UI.hic.createTitleText.text = $"Choose Sacrfices for the upgrade: {PieceDefinition.name[PieceDefinition.upgrade_target[TargetCellId]]}";
        if (UI.hic.createCostText) UI.hic.createCostText.text = $"Cost: {PieceDefinition.BuildCost[pieceType]}"; //to do, this does not show full cost
        if (UI.hic.createSprite)
        {
            var s = !string.IsNullOrEmpty(PieceDefinition.spritePath[PieceDefinition.upgrade_target[TargetCellId]]) ? Resources.Load<Sprite>(PieceDefinition.spritePath[PieceDefinition.upgrade_target[TargetCellId]]) : null;
            UI.hic.createSprite.sprite = s;
            UI.hic.createSprite.enabled = (s != null);
        }

        ShowLeftPanel.HudRefresh();
    }

     private static IEnumerable<int> computeSacrficeTargets()
    {
        List<int> SacrficeTargets = new List<int>(128);
        var seen = new HashSet<int>();
        for (int i = 0; i < UIBridge._count; i++)
        {
            var actions = UIBridge._offers[i];
            if (actions.kind != Upgrade) continue;
            if (actions.pieceType != pieceType) continue;
            if (UIBridge._mask[i] == 0) continue;

            if (PieceDefinition.sacrificeCost_isNeedsSpecificPiece[pieceType])
            {
                if (PieceDefinition.sacrificeCost_specificPiece[pieceType] != actions.TargetCellId) continue;
            }

            //to do - need to remove the prevoius selected option if, there was one
            
            for (int b = 0; b < PieceDefinition.sacrificeCost_howManyItNeeds[pieceType]; b++)
            {
                if (!seen.Add(actions.addCost[b])) continue; // skip duplicates
                SacrficeTargets.Add(UIBridge.bm.pieceCellId[actions.addCost[b]]); // to do, check if this is the right thing to add
            }    
        }
        cachedLegalAddCost = SacrficeTargets.ToArray();
        return SacrficeTargets;
    } 

    public static void secondTargetlauncherOptions() // this is for aux, probs could work for more than just the launcher
    {
        showBoard.ClearHighlights();

        UIHelpers.SetBackdropColor(UI.hic.config.actionExecuteBackground);
        UIHelpers.SetPanelBackdropColor(UI.hic.config.actionExecutePanelBackground);

        PanelToggles.TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: true, walls: false, secondWalls: false);

        showBoard.HighlightCells(ComputeAuxCellsForAction(), UI.hic.config.actionLegalTargetHighlight);

        ShowLeftPanel.HudRefresh();
    }

    public static void PieceKindOptions()
    {
        showBoard.ClearHighlights();
        showBoard.ApplyDefaultCellColor(UI.hic.config.defaultCellColor);
        UIHelpers.SetBackdropColor(UI.hic.config.pieceActionBackground);
        UIHelpers.SetPanelBackdropColor(UI.hic.config.pieceActionPanelBackground);
        PanelToggles.TogglePanels(build: false, create: false, action: false, pieceFull: true, execute: false, walls: false, secondWalls: false);

        PushPieceActionListForSelection();

        ShowLeftPanel.HudRefresh();
    }

    public static void TargetCellIdsOptions()
    {
        showBoard.ClearHighlights();

        UIHelpers.SetBackdropColor(UI.hic.config.actionExecuteBackground);
        UIHelpers.SetPanelBackdropColor(UI.hic.config.actionExecutePanelBackground);

        PanelToggles.TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: true, walls: false, secondWalls: false);

        showBoard.HighlightCells(ComputeTargetCellsForAction(), UI.hic.config.actionLegalTargetHighlight);

        if (UI.hic.actionTitleText) UI.hic.actionTitleText.text = $"Action: {kind}"; // to do, probs need fix it to enum to string
        if (UI.hic.actionPieceText) UI.hic.actionPieceText.text = $"Piece #{UIBridge.bm.occupantPieceId[ActorCellId]}";
        // if (UI.hic.actionCostText) UI.hic.actionCostText.text = $"Cost: {action.cost}"; to do, add cost

        ShowLeftPanel.HudRefresh();
    }


    public static IEnumerable<int> ComputeTargetCellsForAction()
    {
        List<int> targetCells = new List<int>(128);

        for (int i = 0; i < UIBridge._count; i++)
        {
            var action = UIBridge._offers[i];
            if (action.kind != kind) continue;
            if (action.ActorsCellId != ActorCellId) continue;
            if (action.pieceType != pieceType) continue;
            if (UIBridge._mask[i] == 0) continue; // masked out = illegal

            targetCells.Add(action.TargetCellId);
        }
        cachedLegalTargetCellId = targetCells.ToArray();
        return targetCells;
    }

    public static IEnumerable<int> ComputeAuxCellsForAction()
    {
        List<int> targetAuxCells = new List<int>(128);

        for (int i = 0; i < UIBridge._count; i++)
        {
            var action = UIBridge._offers[i];
            if (action.kind != kind) continue;
            if (action.ActorsCellId != ActorCellId) continue;
            if (action.pieceType != pieceType) continue;
            if (action.TargetCellId != TargetCellId) continue;
            if (UIBridge._mask[i] == 0) continue; // masked out = illegal

            targetAuxCells.Add(UIBridge.bm.pieceCellId[action.aux]);
        }
        cachedLegalAux = targetAuxCells.ToArray();
        return targetAuxCells;
    }


    public static void PushPieceActionListForSelection()
    {
        var items = new List<ActionItem>();
        bool moveAddedForCell = false;
        
        for (int i = 0; i < UIBridge._count; i++)
        {
            var action = UIBridge._offers[i];
            if (action.kind == Game.Core.ActionKind.EndTurn) continue; // exclude non-piece actions
            if (action.ActorsCellId != (ushort)ActorCellId) continue;               // only actions from this piece

            // Show only one Move per selected piece unless raw offers requested
            if (action.kind == Game.Core.ActionKind.Move && !UI.hic.config.GiveRawActionOffers)
            {
                if (moveAddedForCell) continue;
                moveAddedForCell = true;
            }

            int kind = action.kind;
            string label = UIHelpers.PrettyAction(action);
            int cost = Mathf.RoundToInt(UIBridge._quoted[i]);
            bool legal = UIBridge._mask[i] != 0;
            items.Add(new ActionItem(i.ToString(), label, cost, legal, Array.Empty<int>(), kind));
        }
        
        UI.hic.pieceActionListFull.Show(items);
    }

}
