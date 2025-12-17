using static Game.Core.ActionKind;
using System.Collections.Generic;

public static class CreateActionFilter 
{
    const byte kind = Create;
    public static bool isPieceType;
    public static int pieceType;

    public const ushort ActerCellId = 0xFFFF;

    public static bool isTargetCellId;
    public static ushort TargetCellId;

    public static bool isNumberOfWalls;
    public static bool isAux;
    public static ushort aux;

    public static bool isAddCost;
    public static int[] addCost; 


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
        isAux = true;
        cleanUpSet();
    }

    public static void setSacCost()
    {
        if (UIFilter.uIType != UIFilter.UIType.Cell)
        {
            UIFilter.ResetClickedData();
            return;
        } 
        addCost[addCost.Length] = UIFilter.clickedAddCost[UIFilter.clickedAddCost.Length];
        if (addCost.Length == PieceDefinition.sacrificeCost_howManyItNeeds[pieceType]) isAddCost = true;
        cleanUpSet();
    }
    
    public static void setTargetCell()
    {
        if (UIFilter.uIType != UIFilter.UIType.Cell)
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
        //performAction(); to do
    }

    // need to add go back to defualt Option
    public static void NumberOfWallOptions()
    {
        var wallOptions = ComputeWallOptions();
        UI.hic.wallOptionPanel.ShowSideOptions(wallOptions);
        PanelToggles.TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: false, walls: true); // populate this to pther areas
    }

    public static void WallConfigOptions()
    {
        // to do- currently the system is Semi-Internal
    }

    public static void SacrificeCostOptions()
    {
        showBoard.HighlightCells(computeSacrficeTargets(), UI.hic.config.createModeCellHighlight);
    }

    public static void CreateCellOptions()
    {
        showBoard.HighlightCells(computeCreateCellOptions(), UI.hic.config.createModeCellHighlight);
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

            SacrficeTargets.Add(actions.TargetCellId); // to do, check if this is the right thing to add
        }
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
        return CreateCellOptions;
    }
}
