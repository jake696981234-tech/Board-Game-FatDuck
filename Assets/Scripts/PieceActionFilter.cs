using static Game.Core.ActionKind;

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

    public static bool isAux;
    public static ushort aux;

    public static bool isAddCost;
    public static int[] addCost; 



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
        if (UIFilter.uIType != UIFilter.UIType.ActionKind)
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
        if (UIFilter.uIType != UIFilter.UIType.TargetCellId)
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
         if (UIFilter.uIType != UIFilter.UIType.TargetCellId)
        {
            UIFilter.ResetClickedData();
            return;
        }
        aux = (ushort)UIBridge.bm.occupantPieceId[UIFilter.clickedCellId];
        isAux = true;
    }


    private static void SetMoveFilter()
    {
        if (!isAux) isAux = true;
        if (!isAddCost) isAddCost = true;

        if (!isTargetCellId)
        {
            if (UIFilter.uIType != UIFilter.UIType.TargetCellId)
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
            if (UIFilter.uIType != UIFilter.UIType.TargetCellId)
            {
                UIFilter.ResetClickedData();
                return;
            }
            TargetCellId = UIFilter.clickedCellId;
            aux = (ushort)PieceDefinition.spawn_targetType[pieceType];;
            isTargetCellId = true;

            UIFilter.ResetClickedData();
            showNextActionOption();
            return;
        }
    }
    private static void SetShootKindFilter()
    {
        if (!isAux) isAux = true;
        if (!isAddCost) isAddCost = true;

        if (!isTargetCellId)
        {
            if (UIFilter.uIType != UIFilter.UIType.TargetCellId)
            {
                UIFilter.ResetClickedData();
                return;
            }
            TargetCellId = UIFilter.clickedCellId;
            aux = (ushort)UIBridge.bm.occupantPieceId[TargetCellId];
            isTargetCellId = true;

            UIFilter.ResetClickedData();
            showNextActionOption();
            return;
        }
    }

    private static void UpgradeFilter()
    {
        if (!isAux) isAux = true;
        
        if (!isTargetCellId)
        {
            if (UIFilter.uIType != UIFilter.UIType.TargetCellId)
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
            if (UIFilter.uIType != UIFilter.UIType.addCost)
            {
                UIFilter.ResetClickedData();
                return;
            } 
            addCost[addCost.Length] = UIFilter.clickedAddCost[UIFilter.clickedAddCost.Length];
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
            // showPieceActionOptions();
            UIFilter.ResetClickedData();
            return;
        }

        if (!isTargetCellId)
        {
            // showTargetCellIds();
            UIFilter.ResetClickedData();
            return;
        }

        if (!isAux)
        {
             // showAuxOptions();
            UIFilter.ResetClickedData();
            return;
        }

        if (!isAddCost)
        {
            // showSacrificeCostOptions();
            UIFilter.ResetClickedData();
            return;
        }

        //performAction(); to do
    }
}
