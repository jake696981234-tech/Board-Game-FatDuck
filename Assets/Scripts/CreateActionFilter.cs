using static Game.Core.ActionKind;

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

        pieceType = UIFilter.clickedBuildItem;
        isPieceType = true;
        
        if (!PieceDefinition.connectors_enabled[UIFilter.clickedBuildItem]) isAux = true;
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
        aux = UIFilter.clickedAux;
        isAux = true;
        cleanUpSet();
    }

    public static void setSacCost()
    {
        if (UIFilter.uIType != UIFilter.UIType.addCost)
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
        if (UIFilter.uIType != UIFilter.UIType.TargetCellId)
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
            // showNumberOfWallOptions();
            UIFilter.ResetClickedData();
            return;
        }
        if (!isAux)
        {
            // showWallConfigOptions();
            UIFilter.ResetClickedData();
            return;
        }

        if (!isAddCost)
        {
            // showSacrificeCostOptions();
            UIFilter.ResetClickedData();
            return;
        }

        if (!isTargetCellId)
        {
            // showSacrificeCostOptions();
            UIFilter.ResetClickedData();
            return;
        }
        //performAction(); to do
    }


}
