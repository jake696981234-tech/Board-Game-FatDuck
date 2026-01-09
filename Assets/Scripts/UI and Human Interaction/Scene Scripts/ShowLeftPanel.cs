using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using TMPro;

public static class ShowLeftPanel
{
    public enum EndRoundTotalsPlayer
    {
        playerOne = 0,
        PlayerTwo = 1,
        PlayerThree = 2,
        PlayerFour = 3
    }
    public static EndRoundTotalsPlayer endRoundTotalsPlayer;
    private static readonly List<GameObject> spawnedPerTypeFactoryPayOutPrefab = new List<GameObject>();
    // public static void updatePerTypeEndRoundTotals()
    // {
    //     for (int c = 0; c < spawnedPerTypeFactoryPayOutPrefab.Count; c++)
    //     {
    //         if (spawnedPerTypeFactoryPayOutPrefab[c] != null)
    //         {
    //             UnityEngine.Object.Destroy(spawnedPerTypeFactoryPayOutPrefab[c]);
    //         }
    //     }
    //     spawnedPerTypeFactoryPayOutPrefab.Clear();

    //     var payout = UIBridge._snapshot.PerEndRoundPayOut[(int)endRoundTotalsPlayer];
    //     if (payout.pieceType == null || payout.PieceTypePayOut == null) return;
    //     int count = Math.Min(payout.pieceType.Length, payout.PieceTypePayOut.Length);
    //     if (count <= 0) return;

    //     for (int i = 0; i < payout.pieceType.Length; i++)
    //     {
    //         int type = payout.pieceType[i];
    //         var prefab = UnityEngine.Object.Instantiate(UI.hic.PerTypeFactoryPayOutPrefab, UI.hic.PerTypeFactoryPayOutRoot);
    //         prefab.SetActive(true);

    //         var prefabScript = prefab.GetComponent<FactoryPerTypePayOut>();
    //         prefabScript.SetValues(
    //             Piece.name[type],
    //             payout.PieceTypePayOut[i]
    //         );
    //         spawnedPerTypeFactoryPayOutPrefab.Add(prefab);
    //     }
    // }

    // private static void showPlayerEndRoundTotals(int playerId)
    // {
    //     PanelToggles.ToggleLeftPanels(PanelToggles.leftPanelMode == PanelToggles.LeftPanelsModes.DefaultPanel, false);

    //     if (PanelToggles.leftPanelMode == PanelToggles.LeftPanelsModes.DefaultPanel)
    //     {
    //         PanelToggles.leftPanelMode = PanelToggles.LeftPanelsModes.EndRoundTotalPanel;

    //         endRoundTotalsPlayer = (EndRoundTotalsPlayer)playerId;
    //     }
    //     else
    //     {
    //         PanelToggles.leftPanelMode = PanelToggles.LeftPanelsModes.DefaultPanel;
    //     }

    //     updatePlayerEndRoundTotals(playerId);
    // }

    // public static void updatePlayerEndRoundTotals(int playerId)
    // {
    //     float TotalFactory = UIBridge._snapshot.PerEndRoundPayOut[playerId].PieceTypePayOut.Sum();

    //     UI.hic.TotalPayoutText.text = $"{TotalFactory + UIBridge._snapshot.PerEndRoundPayOut[playerId].BonusForVP + UIBridge._snapshot.PerEndRoundPayOut[playerId].BonusForCoreDamage}";
    //     UI.hic.VpBonusText.text = $"{UIBridge._snapshot.PerEndRoundPayOut[playerId].BonusForVP}";
    //     UI.hic.CoreBonusText.text = $"{UIBridge._snapshot.PerEndRoundPayOut[playerId].BonusForCoreDamage}";
    //     UI.hic.TotalFactoryTotalText.text = $"{TotalFactory}";

    //     if (PanelToggles.leftPanelMode == PanelToggles.LeftPanelsModes.EndRoundTotalPanel2) updatePerTypeEndRoundTotals();
    // }


    private static void BindPlayerRow(RectTransform row, int playerId)
    {
        if (!row) return;
        if (UI.hic.PlayerRow_Button == null || UI.hic.PlayerRow_Button.Length <= playerId)
        {
            UI.hic.PlayerRow_Button = new Button[4];
        }
        // Find children by the agreed names
        var nameText = row.Find("PlayerRow_NameText")?.GetComponent<TMP_Text>();
        var tintImg = row.Find("PlayerRow_TintSwatch")?.GetComponent<Image>();
        var budgetText = row.Find("PlayerRow_BudgetText")?.GetComponent<TMP_Text>();
        var vpText = row.Find("PlayerRow_VPText")?.GetComponent<TMP_Text>();
        var hpText = row.Find("PlayerRow_CoreHPText")?.GetComponent<TMP_Text>();
        var turn = row.Find("PlayerRow_TurnText")?.GetComponent<TMP_Text>();
        var action = row.Find("PlayerRow_ActionText")?.GetComponent<TMP_Text>();
        var passed = row.Find("PlayerRow_PassedText")?.GetComponent<TMP_Text>();
        UI.hic.PlayerRow_Button[playerId] = row.Find("PlayerRow_Button")?.GetComponent<Button>();

        if (nameText) nameText.text = (playerId == UIBridge.gameState.CurrentPlayerId) ? $">Player {playerId}" : $"Player {playerId}";
        if (budgetText) budgetText.text = $"{Mathf.RoundToInt(UIBridge.gameState.GetBudget((byte)playerId))}";
        if (vpText) vpText.text = $"{UIBridge.gameState.GetVP((byte)playerId)}";
        if (hpText) hpText.text = $"{UIBridge.gameState.GetCoreHealth((byte)playerId)}";
        if (turn) turn.text = $"{UI.playerTurn[playerId]}";
        if (action) action.text = $"{UI.playerAction[playerId]}";
        if (passed) passed.text = $"{UIBridge.gameState.PassedTurn(playerId)}";

        // UI.hic.PlayerRow_Button[playerId].onClick.AddListener(() => showPlayerEndRoundTotals(playerId));

        // Optional tint swatch: if you have a palette elsewhere, assign it here (left blank by default)
        if (tintImg) tintImg.enabled = false;
    }

    public static int curActionFee; 
    public static void HudRefresh()
    {
        if (UIBridge.gameState == null) return;

        // --- Match header ---
        if (UI.hic.Header_TurnOwnerText) UI.hic.Header_TurnOwnerText.text = $"Player {UIBridge.gameState.CurrentPlayerId}";
        // if (UI.hic.Header_ModeText) UI.hic.Header_ModeText.text = PanelToggles._mode.ToString();

        // --- Personal stats (your seat) ---
        if (UI.hic.Personal_BudgetText) UI.hic.Personal_BudgetText.text = "Budget: " + $"{Mathf.RoundToInt(UIBridge.gameState.GetBudget(UIBridge._humanPlayer))}";
        curActionFee = CostEngine.turnFee(in UIBridge.gameState.ps[UIBridge._humanPlayer]);
        if (UI.hic.Personal_ActionFee) UI.hic.Personal_ActionFee.text = "Action Fee: " + $"{curActionFee}";
        
        if (UI.hic.Personal_VPText) UI.hic.Personal_VPText.text = "VP: " + $"{UIBridge.gameState.GetVP(UIBridge._humanPlayer)}";
        if (UI.hic.Personal_CoreHPText) UI.hic.Personal_CoreHPText.text = "Core Hp: " + $"{UIBridge.gameState.GetCoreHealth(UIBridge._humanPlayer)}";
        // Tint swatch optional; if you have a palette somewhere you can assign it here.

        // --- All players list ---
        if (UI.hic.AllPlayers_ListRoot && UI.hic.PlayerRowPrefab)
        {
            // Clear old rows
            for (int i = UI.hic.AllPlayers_ListRoot.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(UI.hic.AllPlayers_ListRoot.GetChild(i).gameObject);

            // Show current player first, then others in seat order
            Span<int> order = stackalloc int[4] { UIBridge.gameState.CurrentPlayerId, (UIBridge.gameState.CurrentPlayerId + 1) & 3, (UIBridge.gameState.CurrentPlayerId + 2) & 3, (UIBridge.gameState.CurrentPlayerId + 3) & 3 };
            for (int k = 0; k < 4; k++)
            {
                int p = order[k];
                var go = UnityEngine.Object.Instantiate(UI.hic.PlayerRowPrefab, UI.hic.AllPlayers_ListRoot);
                BindPlayerRow(go.transform as RectTransform, p);
            }
        }

        // --- Debug box ---
        if (UI.hic.Debug_OffersText)
        {
            int masked = 0; for (int i = 0; i < UIBridge._count; i++) if (UIBridge._mask[i] == 0) masked++;
            UI.hic.Debug_OffersText.text = $"Shown: {UIBridge._count}  /  Total: {UIBridge._total}  (Masked: {masked})";
        }
        if (UI.hic.Debug_LastActionText) UI.hic.Debug_LastActionText.text = string.IsNullOrEmpty(UI._lastActionLabel) ? "—" : UI._lastActionLabel;
        if (UI.hic.Debug_SnapshotText)
        {
            // We don't hold a UIBridge._snapshot here; show some quick match counters instead.
            UI.hic.Debug_SnapshotText.text = $"CenterVP={UIBridge.gameState.GetCenterVP()}  RoundsLeft={UIBridge.gameState.RoundsLeft}";
        }
    }
}
