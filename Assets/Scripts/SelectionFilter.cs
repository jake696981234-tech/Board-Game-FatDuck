using System;
using UnityEngine;

public static class UIFilter
{
    public enum State
    {
        Idle = 1,
        Create = 2,
        PieceAction = 3,
        
    }
    public static State state = State.Idle;
   
   public enum UIType
    {
        cell = 1,
        BuildItem = 2,
        NumberOfWalls = 3,
        WallConfig = 4,
        addCost = 5,
        TargetCellId = 6,
        ActionKind = 7,
        PieceAction = 8,
        Cancel = 9,
    }
    public static UIType uIType;

    public static byte clickedBuildItem;
    public static byte clickedActionKind;
    public static ushort clickedCellId;
    public static ushort clickedAux;
    public static int[] clickedAddCost;

    public static void reset()
    {
        // Add Reset UI in Here to do

        state = State.Idle;

        PieceActionFilter.isKind = false;
        PieceActionFilter.isPieceType = false;
        PieceActionFilter.isActorCellId = false;
        PieceActionFilter.isTargetCellId = false;
        PieceActionFilter.isAux = false;
        PieceActionFilter.isAddCost = false;

        PieceActionFilter.kind = 15;
        PieceActionFilter.pieceType = -1;
        PieceActionFilter.TargetCellId = 300;
        PieceActionFilter.aux = 300;
        Array.Clear(PieceActionFilter.addCost, 0, PieceActionFilter.addCost.Length);

        CreateActionFilter.isPieceType = false;
        CreateActionFilter.isTargetCellId = false;
        CreateActionFilter.isAux = false;
        CreateActionFilter.isAddCost = false;

        CreateActionFilter.pieceType = -1;
        CreateActionFilter.TargetCellId = 300;
        CreateActionFilter.aux = 300;
        Array.Clear(CreateActionFilter.addCost, 0, CreateActionFilter.addCost.Length);
    }

    public static void ResetClickedData()
    {
        clickedBuildItem = 30;
        clickedCellId = 30;
    }

    public static void topFilter()
    {
        if (uIType == UIType.Cancel)
        {
            reset();
            return;
        }

        switch (state)
        {
            case State.Idle:
            {
                if (uIType == UIType.BuildItem)
                {
                    CreateActionFilter.Filter();
                    return;
                }
                if (uIType == UIType.PieceAction)
                {
                    PieceActionFilter.Filter();
                    return;
                }
                
                ResetClickedData();
                return;
            }
            case State.Create:
            {
                CreateActionFilter.Filter();
                return;
            }
            case State.PieceAction:
            {
                PieceActionFilter.Filter();
                return;
            }
        }
    }  
}
