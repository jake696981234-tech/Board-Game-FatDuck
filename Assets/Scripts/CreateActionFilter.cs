using static Game.Core.ActionKind;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// to do, need to figure out if the strut UIinfo still needs to be around, its not added to this new system yet

public static class CreateActionFilter 
{
    const byte kind = Create;
    public static bool isPieceType;
    public static int pieceType;

    public const ushort ActorCellId = 0xFFFF;

    public static bool isTargetCellId;
    public static ushort TargetCellId;
    public static int[] cachedLegalTargetCellId;

    public static bool isNumberOfWalls;
    public static int WallNumber;


    public static bool isAux;
    public static bool ActionRequiresAux;
    public static ushort aux;

    public static bool isAddCost;
    public static bool ActionCostRequiresAddCost;
    public static int[] addCost; 
    public static int[] cachedLegalAddCost;


    public static void Filter()
    {
        if (!isPieceType)
        {
            SetFilter();
            return;
        }
        if (!isNumberOfWalls)
        {
            setNumberOfWalls();
            return;
        }
        if (!isAux)
        {
            setWallConfig();
            return;
        }
        if (!isAddCost)
        {
            setSacCost();
            return;
        }
        if (!isTargetCellId)
        {
            setTargetCell();
            return;
        }
        
    }

    public static void SetFilter()
    {
        UIFilter.state = UIFilter.State.Create;

        pieceType = UIFilter.clickedBuildPieceType;
        isPieceType = true;
        
        if (!PieceDefinition.connectors_enabled[UIFilter.clickedBuildPieceType]) isAux = true;
        if (!PieceDefinition.sacrificeCost_enabled[pieceType]) isAddCost = true;

        cleanUpSet();
    }

    public static void setNumberOfWalls()
    {
        if (UIFilter.uIType != UIFilter.UIType.NumberOfWalls)
        {
            UIFilter.ResetClickedData();
            return;
        }
        WallNumber = UIFilter.clickedWallNumber;
        isNumberOfWalls = true;
        cleanUpSet();
    }

    public static void setWallConfig()
    {
        if (UIFilter.uIType != UIFilter.UIType.WallConfig)
        {
            UIFilter.ResetClickedData();
            return;
        }
        aux = UIFilter.clickedWallConfig;
        ActionRequiresAux = true;
        
        
        isAux = true;
        cleanUpSet();
    }

    public static void setSacCost()
    {
        if (UIFilter.uIType != UIFilter.UIType.Cell || cachedLegalAddCost.Contains(UIFilter.clickedCellId))
        {
            UIFilter.ResetClickedData();
            return;
        } 
        addCost[addCost.Length] = UIBridge.bm.occupantPieceId[UIFilter.clickedCellId];
        ActionCostRequiresAddCost = true;
        if (addCost.Length == PieceDefinition.sacrificeCost_howManyItNeeds[pieceType]) isAddCost = true;
        cleanUpSet();
    }
    
    public static void setTargetCell()
    {
        if (UIFilter.uIType != UIFilter.UIType.Cell || cachedLegalTargetCellId.Contains(UIFilter.clickedCellId))
        {
            UIFilter.ResetClickedData();
            return;
        }
        TargetCellId = UIFilter.clickedCellId;
        cleanUpSet();
    }

    public static void cleanUpSet()
    {
        UIFilter.ResetClickedData();
        showNextOption();
    }
    
    


    private static void showNextOption()
    {
        if (!isNumberOfWalls)
        {
            NumberOfWallOptions();
            UIFilter.ResetClickedData();
            return;
        }
        if (!isAux)
        {
            WallConfigOptions();
            UIFilter.ResetClickedData();
            return;
        }

        if (!isAddCost)
        {
            SacrificeCostOptions();
            UIFilter.ResetClickedData();
            return;
        }

        if (!isTargetCellId)
        {
            CreateCellOptions();
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
        Debug.Log($"showNextOption failed this is very unexpected");
    }

     public static void CreateCellOptions()
    {
        showBoard.ClearHighlights();
        showBoard.ApplyDefaultCellColor(UI.hic.config.defaultCellColor);
        
        showBoard.HighlightCells(computeCreateCellOptions(), UI.hic.config.createModeCellHighlight);
        PanelToggles.TogglePanels(build: false, create: true, action: false, pieceFull: false, execute: false, walls: false, secondWalls: false); 

        UIHelpers.SetBackdropColor(UI.hic.config.createModeBackground);
        UIHelpers.SetPanelBackdropColor(UI.hic.config.createModePanelBackground);
        if (UI.hic.createTitleText) UI.hic.createTitleText.text = $"Create: {PieceDefinition.name[pieceType]}";
        if (UI.hic.createCostText) UI.hic.createCostText.text = $"Cost: {PieceDefinition.BuildCost[pieceType]}"; //to do, this does not show full cost
        if (UI.hic.createSprite)
        {
            var s = !string.IsNullOrEmpty(PieceDefinition.spritePath[pieceType]) ? Resources.Load<Sprite>(PieceDefinition.spritePath[pieceType]) : null;
            UI.hic.createSprite.sprite = s;
            UI.hic.createSprite.enabled = (s != null);
        }

        ShowLeftPanel.HudRefresh();
    }

    // need to add go back to defualt Option
    public static void NumberOfWallOptions()
    {
        UI.hic.wallOptionPanel.showNumberOfWallS(ComputeWallOptions());
        UIHelpers.SetBackdropColor(UI.hic.config.ConnectorModeBackground);
        PanelToggles.TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: false, walls: true, secondWalls: false); // populate this to pther areas
    }

    public static void WallConfigOptions()
    {
        UI.hic.wallOptionPanel.showWallConfigOptions(UIFilter.clickedWallNumber);
        PanelToggles.TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: false, walls: false, secondWalls: true);
    }

    public static void SacrificeCostOptions()
    {
        showBoard.ClearHighlights();
        showBoard.ApplyDefaultCellColor(UI.hic.config.defaultCellColor);

        showBoard.HighlightCells(computeSacrficeTargets(), UI.hic.config.SacrificeCostCellHighlight);
        PanelToggles.TogglePanels(build: false, create: true, action: false, pieceFull: false, execute: false, walls: false, secondWalls: false); // could change this to sac specfic

        UIHelpers.SetBackdropColor(UI.hic.config.createModeBackground);
        UIHelpers.SetPanelBackdropColor(UI.hic.config.createModePanelBackground);
        if (UI.hic.createTitleText) UI.hic.createTitleText.text = $"Choose Sacrfice/s for: {PieceDefinition.name[pieceType]}";
        if (UI.hic.createCostText) UI.hic.createCostText.text = $"Cost: {PieceDefinition.BuildCost[pieceType]}"; //to do, this does not show full cost
        if (UI.hic.createSprite)
        {
            var s = !string.IsNullOrEmpty(PieceDefinition.spritePath[pieceType]) ? Resources.Load<Sprite>(PieceDefinition.spritePath[pieceType]) : null;
            UI.hic.createSprite.sprite = s;
            UI.hic.createSprite.enabled = (s != null);
        }

        ShowLeftPanel.HudRefresh();
    }

   

    
    public static IEnumerable<ushort> ComputeWallOptions()
    {
        List<ushort> wallConfigs = new List<ushort>(64);
        for (int i = 0; i < UIBridge._count; i++)
        {
            var actions = UIBridge._offers[i];
            if (actions.kind != Create) continue;   // byte code
            if (actions.pieceType != pieceType) continue;
            if (UIBridge._mask[i] == 0) continue; // masked out = illegal/unaffordable
            wallConfigs.Add(actions.aux);
        }
        return wallConfigs;
    }

    private static IEnumerable<int> computeSacrficeTargets()
    {
        List<int> SacrficeTargets = new List<int>(128);
        var seen = new HashSet<int>();
        for (int i = 0; i < UIBridge._count; i++)
        {
            var actions = UIBridge._offers[i];
            if (actions.kind != Create) continue;
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
                SacrficeTargets.Add(UIBridge.bm.pieceCellId[actions.addCost[b]]); 
            }
        }
        cachedLegalAddCost = SacrficeTargets.ToArray();
        return SacrficeTargets;
    } 

    public static IEnumerable<int> computeCreateCellOptions()
    {
        List<int> CreateCellOptions = new List<int>(128);
        for (int i = 0; i < UIBridge._count; i++)
        {
            var theAction = UIBridge._offers[i];
            if (theAction.kind != Create) continue;   // byte code

            if (PieceDefinition.connectors_enabled[pieceType])
            {
                if (theAction.aux != aux) continue;
            }

            if (theAction.pieceType != pieceType) continue;
            if (UIBridge._mask[i] == 0) continue; // masked out = illegal/unaffordable
            CreateCellOptions.Add(theAction.TargetCellId);
        }
        cachedLegalTargetCellId = CreateCellOptions.ToArray();
        return CreateCellOptions;
    }
}
