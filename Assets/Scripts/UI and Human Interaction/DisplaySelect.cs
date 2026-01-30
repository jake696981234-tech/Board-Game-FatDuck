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
    public static void updateDisplaySelect()
    {
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
        UI.hic.displayIntakeCell.text = $"Intake Cell: {AFilter.Chosen[(int)ChoosingInstakeCellID]}";
    }


    // private static Piece.AbilityKind GiveMeActionsNames(byte theActionKind)
    // {
    //     switch (theActionKind)
    //     {
    //         case 
    //     }
    // }
}
