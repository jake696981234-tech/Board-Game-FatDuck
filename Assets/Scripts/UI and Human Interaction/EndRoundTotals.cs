using System;
using System.Collections.Generic;
using System.Text;

public static class EndRoundTotals
{
    

    public static void SubscribeEndRoundTotals()
    {
        UI.hic.ShowMeEndRoundTotals.onClick.AddListener(() => DisplaySelect.ToggleLeftPanels(ordinals: false, payout: true, actionSelect: false));
        UI.hic.ShowOrdinals.onClick.AddListener(() => DisplaySelect.ToggleLeftPanels(ordinals: true, payout: false, actionSelect: false));
        UI.hic.ShowActionSelectionButton.onClick.AddListener(() => DisplaySelect.ToggleLeftPanels(ordinals: false, payout: false, actionSelect: true));
    }

    // public static void flipBool()
    // {
    //     if (!EndRoundTotalsVisible) { EndRoundTotalsVisible = true; } else {EndRoundTotalsVisible = false; }
    //     UI.hic.SubscribedUIRoot.SetActive(!EndRoundTotalsVisible);
    //     UI.hic.EndRoundTotalsRoot.SetActive(EndRoundTotalsVisible);
    // }
    public static void updateEndRoundTotals()
    {
        if (!DisplaySelect.EndRoundTotalsVisible) return;

        PerPiecePayout perPiecePayout = PassiveActions.ComputeDetailedPlayerFactoryIncome(UIBridge._humanPlayer, UIBridge.gameIndex);
        float totalFactory = PassiveActions.ComputeFactoryIncome(UIBridge._humanPlayer, UIBridge.gameIndex);
        float vpBonus = UIBridge.gameState.ComputePlayerVPReward(UIBridge._humanPlayer);
        float coreBonus = UIBridge.gameState.ComputePlayeroreDamageReward(UIBridge._humanPlayer);
        float totalEndRound = totalFactory + vpBonus + coreBonus;
        float penalties = UIBridge.gameState.playerPieceDrivenPenalties(UIBridge._humanPlayer);

        UI.hic.newTotalEndRoundPayOutText.text = $"Total End Round Payout = {totalEndRound}";
        UI.hic.newVpBonusText.text = $"VP Bonus = {vpBonus}";
        UI.hic.newCoreBonusText.text = $"Core Damage Bonus = {coreBonus}";
        UI.hic.newTotalPerPiecePayOutText.text = $"Total Piece End Round Income = {totalFactory}";

        if (penalties > 0)
        {
            UI.hic.PieceDrivenPenalties.text = $"Total Piece Driven Penalties = {penalties}";
            UI.hic.newTotalEndRoundPayOutWithPenaltiesText.text = $"Total - Penalties = {totalEndRound - penalties}";
        }
        else
        {
            UI.hic.PieceDrivenPenalties.text = string.Empty;
            UI.hic.newTotalEndRoundPayOutWithPenaltiesText.text = string.Empty;
        }
        UI.hic.newPerTypeCurrentPayOut.text = BuildPerTypePayoutText(perPiecePayout);
    }

    private static string BuildPerTypePayoutText(PerPiecePayout perPiecePayout)
    {
        if (perPiecePayout.pieceType == null || perPiecePayout.payout == null)
        {
            return string.Empty;
        }

        int length = Math.Min(perPiecePayout.pieceType.Length, perPiecePayout.payout.Length);
        if (length == 0)
        {
            return string.Empty;
        }

        var totals = new Dictionary<int, float>();
        var order = new List<int>();

        for (int i = 0; i < length; i++)
        {
            int type = perPiecePayout.pieceType[i];
            float payout = perPiecePayout.payout[i];

            if (!totals.TryGetValue(type, out var current))
            {
                totals[type] = payout;
                order.Add(type);
                continue;
            }

            totals[type] = current + payout;
        }

        var sb = new StringBuilder(128);
        for (int i = 0; i < order.Count; i++)
        {
            int type = order[i];
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(GetPieceTypeName(type));
            sb.Append(" = ");
            sb.Append(totals[type].ToString("0.##"));
        }

        return sb.ToString();
    }

    private static string GetPieceTypeName(int type)
    {
        if (Piece.name != null && type >= 0 && type < Piece.name.Length && !string.IsNullOrEmpty(Piece.name[type]))
        {
            return Piece.name[type];
        }
        return $"Type {type}";
    }
}
