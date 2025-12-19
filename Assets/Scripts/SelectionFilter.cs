using System;
using UnityEngine;

public static class UIFilter
{
    public static void topFilter()
    {
        if (uIType == UIType.EndTurnButton)
        {
            UIBridge.PerformActionIndex(UIHelpers.FindEndTurnIndex());
            reset();
        }


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
                if (uIType == UIType.Cell && UIHelpers.HasPieceActionsForCell(clickedCellId))
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
    public static byte clickedBuildPieceType;
    public static byte clickedActionKind;
    public static ushort clickedCellId;
    public static ushort clickedWallConfig;
    public static int clickedWallNumber;

    #region Subscription

    public static void HookPresenters()
    {
        UI.hic.endTurnButton.onClick.AddListener(onEndTurnButton);
        UI.hic.buildMenu.OnItemClicked += OnBuildItemClicked;
        UI.hic.pieceActionListFull.OnItemClicked += OnPieceActionClicked;
        // UI.hic.SeePerPieceTypeTotalsButton.onClick.AddListener(() => ShowFactoryBonusByPieceTypePrefabs()); //to do
    }



    public static void OnBuildItemClicked(Game.Core.Action item, UIInfo uiInfo)
    {
        uIType = UIType.BuildItem;
        clickedBuildPieceType = item.pieceType;
        topFilter();
    }

    public static void OnWallNumberClicked(int wallNumber)
    {
        uIType = UIType.NumberOfWalls;
        clickedWallNumber = wallNumber;
        topFilter();
    }

    public static void OnWallConfigClicked(ushort wallConfig)
    {
        uIType = UIType.WallConfig;
        clickedWallConfig = wallConfig;
        topFilter();
    }

    public static void OnCellClicked(ushort Cell)
    {
        uIType = UIType.Cell;
        clickedCellId = Cell;
        topFilter();
    }

    public static void OnPieceActionClicked(ActionItem item)
    {
        uIType = UIType.PieceActionKind;
        clickedActionKind = (byte)item.kind;
        topFilter();
    }

    public static void onCancel()
    {
        uIType = UIType.Cancel;
         topFilter();
    }

    public static void onEndTurnButton()
    {
        uIType = UIType.EndTurnButton;
         topFilter();
    }


    #endregion

    #region DTOS
    public enum State
    {
        Idle = 1,
        Create = 2,
        PieceAction = 3,
        
    }
    public static State state = State.Idle;
   
    public enum UIType
    {
        BuildItem = 2,
        NumberOfWalls = 3,
        WallConfig = 4,
        Cell = 6,
        PieceActionKind = 8,
        Cancel = 9,
        EndTurnButton = 10,
    }
    public static UIType uIType;
    #endregion

    #region Util
    
    public static void reset()
    {
        UIBridge.RebuildOffersForCurrentPlayer();
        // Add Reset UI in Here to do
        ShowRightPanel.PushNonPieceActionList();
        ShowRightPanel.PushCreateActionMenu();

        //UI 
        showBoard.ClearHighlights();
        showBoard.ApplyDefaultCellColor(UI.hic.config.defaultCellColor);
        UIHelpers.SetBackdropColor(UI.hic.config ? UI.hic.config.buildModeBackground : new Color(0, 0, 0, 0.8f));
        UIHelpers.SetPanelBackdropColor(UI.hic.config ? UI.hic.config.buildModePanelBackground : new Color(0, 0, 0, 0.8f));
        PanelToggles.TogglePanels(build: true, create: false, action: true, pieceFull: false, execute: false, walls: false, secondWalls: false);
        ShowLeftPanel.HudRefresh();

        ResetClickedData();

        state = State.Idle;

        PieceActionFilter.isKind = false;
        PieceActionFilter.isPieceType = false;
        PieceActionFilter.isActorCellId = false;
        PieceActionFilter.isTargetCellId = false;
        PieceActionFilter.isAux = false;
        PieceActionFilter.ActionRequiresAux = false;
        PieceActionFilter.isAddCost = false;
        PieceActionFilter.ActionCostRequiresAddCost = false;

        PieceActionFilter.kind = 15;
        PieceActionFilter.pieceType = -1;
        PieceActionFilter.TargetCellId = 300;
        PieceActionFilter.aux = 300;
        Array.Clear(PieceActionFilter.addCost, 0, PieceActionFilter.addCost.Length);

        CreateActionFilter.isPieceType = false;
        CreateActionFilter.isTargetCellId = false;
        CreateActionFilter.isAux = false;
        CreateActionFilter.ActionRequiresAux = false;
        CreateActionFilter.isAddCost = false;
        CreateActionFilter.ActionCostRequiresAddCost = false;

        CreateActionFilter.pieceType = -1;
        CreateActionFilter.TargetCellId = 300;
        CreateActionFilter.aux = 300;
        Array.Clear(CreateActionFilter.addCost, 0, CreateActionFilter.addCost.Length);
    }

    public static void ResetClickedData()
    {
        clickedBuildPieceType = 30;
        clickedCellId = 30;
    }
    #endregion
}
