using UnityEngine;
using System;
using UnityEngine.InputSystem;
using static AFilter.UIType;

public static class UIInput
{
    public static void UpdateMe()
    {
        UIBridge.SnapShotUpdate();

        bool cancel =
        (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
        (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
        if (cancel) onCancel();
    }

    public static void HookPresenters()
    {
        // UI.hic.endTurnButton.onClick.AddListener(onEndTurnButton);
        UI.hic.buildMenu.OnItemClicked += OnBuildItemClicked;
        UI.hic.pieceActionListFull.OnItemClicked += OnPieceActionClicked;
        UI.hic.nonPieceActionList.OnItemClicked += OnNonPieceActionClicked;
        // UI.hic.SeePerPieceTypeTotalsButton.onClick.AddListener(() => ShowFactoryBonusByPieceTypePrefabs()); //to do
         UI.hic.BackgroundExitButton.onClick.AddListener(() => BackgroundExitButton());
    }

    private static void BackgroundExitButton()
    {
        AFilter.uIType = Cancel;
        AFilter.topFilter();
    }
    private static void OnBuildItemClicked(Game.Core.Action item, UIInfo uiInfo)
    {
        AFilter.uIType = BuildItem;
        AFilter.UInput[(int)BuildItem] = item.TargetType;
        AFilter.topFilter();
    }
    public static void OnWallNumberClicked(int wallNumber)
    {
        AFilter.uIType = NumberOfWalls;
        // clickedWallNumber = wallNumber;
        AFilter.topFilter();
    }

    public static void OnWallConfigClicked(ushort wallConfig)
    {
        AFilter.uIType = WallConfig;
        // clickedWallConfig = wallConfig;
        AFilter.UInput[(int)WallConfig] = wallConfig;
        AFilter.topFilter();
    }

    public static void OnCellClicked(ushort cell)
    {
        AFilter.uIType = Cell;
        AFilter.UInput[(int)Cell] = cell;
        AFilter.topFilter();
    }

    private static void OnPieceActionClicked(ActionItem item)
    {
        AFilter.uIType = PieceActionKind;
        AFilter.UInput[(int)PieceActionKind] = item.kind;
        AFilter.topFilter();
    }

    private static void OnNonPieceActionClicked(ActionItem item)
    {
        // Currently only EndTurn lives here; execute immediately.
        if (!int.TryParse(item.id, out var index)) return;
        if (index < 0 || index >= UIBridge._count) return;
        if (UIBridge._mask[index] == 0) return; // should always be legal for EndTurn

        var action = UIBridge._offers[index];
        UIBridge.PerformActionIndex(action);
        AFilter.reset();
    }

    public static void onCancel()
    {
        AFilter.uIType = Cancel;
        AFilter.topFilter();
    }

    private static void onEndTurnButton()
    {
        AFilter.uIType = EndTurnButton;
         AFilter.topFilter();
    }
   
}
