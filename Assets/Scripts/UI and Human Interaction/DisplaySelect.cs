using UnityEngine;
using static AFilter.UICState;
using static Game.Core.ActionKind;



public static class DisplaySelect
{
    public static bool EndRoundTotalsVisible = false;
    public static bool DisplaySelectVisible = false;
    public static void ToggleLeftPanels(bool ordinals, bool payout, bool actionSelect)
    {
        UI.hic.EndRoundTotalsRoot.SetActive(payout);
        UI.hic.SubscribedUIRoot.SetActive(ordinals);
        UI.hic.ActionSelectionDisplayRoot.SetActive(actionSelect);

        EndRoundTotalsVisible = payout;
        DisplaySelectVisible = actionSelect;
        if (payout) EndRoundTotals.updateEndRoundTotals();
        if (actionSelect) updateDisplaySelect();
    }

    public static bool isCache = false;

    public static void SwapCache(bool swap)
    {
        isCache = swap;
        updateDisplaySelect();
    }
    public static void updateDisplaySelect()
    {
        if (isCache && isCacheInit)
        {
            showCacheDisplaySelect();
            return;
        }
        if (!DisplaySelectVisible) return;
        if (AFilter.Chosen[(int)ChoosingKind] == -1) 
        {
            UI.hic.displaykind.text = "Kind: -1";
        }
        else
        {
            // Piece.AbilityKind kind = (Piece.AbilityKind)AFilter.Chosen[(int)ChoosingKind];
            // string textKind = kind.ToString();
            UI.hic.displaykind.text = $"Kind: {(Piece.AbilityKind)AFilter.Chosen[(int)ChoosingKind]}";
            cachedLastAction[0] = $"Kind: {(Piece.AbilityKind)AFilter.Chosen[(int)ChoosingKind]}";
        }
                
        UI.hic.displayTargetCell.text = $"Target Cell: {AFilter.Chosen[(int)ChoosingTargetCell]}";
        UI.hic.displayActorsCell.text = $"Actors Cell: {AFilter.Chosen[(int)ChoosingActorsCell]}";
        if (AFilter.Chosen[(int)ChoosingTargetType] == -1) 
        {
            UI.hic.displayTargetType.text = "TargetType: -1";
        }
        else
        {
            UI.hic.displayTargetType.text = $"Target Type: {Piece.name[AFilter.Chosen[(int)ChoosingTargetType]]}";
        }
        UI.hic.displayWallConfig.text = $"Wall Config: {AFilter.Chosen[(int)ChoosingWallConfig]}";
        UI.hic.displayIntakeCell.text = $"Intake Cell: {AFilter.Chosen[(int)ChoosingIntakeCell]}";
    }
    public static bool isCacheInit = false;
    public static void showCacheDisplaySelect()
    {
        UI.hic.displaykind.text = cachedLastAction[0];
        UI.hic.displayTargetCell.text = cachedLastAction[1];
        UI.hic.displayActorsCell.text = cachedLastAction[2];
        UI.hic.displayTargetType.text = cachedLastAction[3];
        UI.hic.displayWallConfig.text = cachedLastAction[4];
        UI.hic.displayIntakeCell.text = cachedLastAction[5];
        
    }

    public static void CacheDisplaySelect()
    {
        if (!DisplaySelectVisible) return;
        if (AFilter.Chosen[(int)ChoosingKind] == -1) 
        {
            cachedLastAction[0] = "Kind: -1";
        }
        else
        {
            cachedLastAction[0] = $"Kind: {(Piece.AbilityKind)AFilter.Chosen[(int)ChoosingKind]}";
        }
                
        cachedLastAction[1] = $"Target Cell: {AFilter.Chosen[(int)ChoosingTargetCell]}";
        cachedLastAction[2] = $"Actors Cell: {AFilter.Chosen[(int)ChoosingActorsCell]}";
        if (AFilter.Chosen[(int)ChoosingTargetType] == -1) 
        {
            cachedLastAction[3] = "TargetType: -1";
        }
        else
        {
            cachedLastAction[3] = $"Target Type: {Piece.name[AFilter.Chosen[(int)ChoosingTargetType]]}";
        }
        cachedLastAction[4] = $"Wall Config: {AFilter.Chosen[(int)ChoosingWallConfig]}";
        cachedLastAction[5] = $"Intake Cell: {AFilter.Chosen[(int)ChoosingIntakeCell]}";
        isCacheInit = true;
    }

    public static string[] cachedLastAction = new string[6];


    // private static Piece.AbilityKind GiveMeActionsNames(byte theActionKind)
    // {
    //     switch (theActionKind)
    //     {
    //         case 
    //     }
    // }
}
