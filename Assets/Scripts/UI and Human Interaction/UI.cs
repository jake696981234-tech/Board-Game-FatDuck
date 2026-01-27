using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Game.Core; // for GameState
using System.Linq;

public static class UI
{
    public static HumanInteractionController hic;
    private static EventManager events;
    public static bool giveRawActionOffers;

    public static void Init(HumanInteractionController theHic, EventManager theEvents)
    {
        hic = theHic;
        events = theEvents;
        subscribeMe();

        // Ensure player row button array is allocated (supports up to 4 seats by design)
        if (hic.PlayerRow_Button == null || hic.PlayerRow_Button.Length < 4)
        {
            hic.PlayerRow_Button = new Button[4];
        }

        // HowManyWallSelected += UIModes.EnterCreateModeWithWallChosen;
        giveRawActionOffers = hic.config.GiveRawActionOffers;

        // ChangetoSecondPanelMode += PanelToggles.SetWallOptionsSecondPanelMode;

        // UIBridge.gameState.OnActionExecuted += UIHelpers.HandleActionExecuted; // refresh on every mutation
        // UIBridge.RebuildOffersForCurrentPlayer();
        // UIModes.EnterBuildMode(); // will push menus from offers
        // UIInput.HookPresenters();
        // ShowLeftPanel.HudRefresh();
        UIInput.HookPresenters();
        AFilter.reset();
    }



    public static string _lastActionLabel = string.Empty; // for Debug HUD


    // public static void OnDisable()
    // {
    //     // UIBridge.gameState.OnActionExecuted -= UIHelpers.HandleActionExecuted;
    //     UIInput.UnhookPresenters();
    // }



    #region UI Helpers

    // Legacy small HUD fields (kept) + new consolidated HUD refresh
    // public static void UpdateHud_LegacySmall()
    // {
    //     if (hic.budgetText)
    //         hic.budgetText.text = $"Budget: {UIBridge.gameState.GetBudget(UIBridge._humanPlayer):0}";
    //     if (hic.blockInputOverlay && hic.config != null && hic.config.blockInputWhenNotYourTurn)
    //         hic.blockInputOverlay.SetActive(UIBridge.gameState.CurrentPlayerId != UIBridge._humanPlayer);
    // }


    #endregion
    #region new Subscription system

    public static int[] playerTurn = new int[4];
    public static int[] playerAction = new int[4];

    public static int roundNumber = 1;
    public static int turnNumber;
    public static int gameNumber = 1;

    // public static event System.Action<ushort> HowManyWallSelected;

    // public static event System.Action ChangetoSecondPanelMode;

    // public static void howManyWallSelected(ushort ChosenWall)
    // {
    //     HowManyWallSelected?.Invoke(ChosenWall);

    //     ChangetoSecondPanelMode?.Invoke();
    // }


    private static void subscribeMe()
    {
        events.TurnBegin += whenTurnBegins;
        events.ActionBegin += whenActionHappens;
        events.RoundBegin += whenRoundEnds; //this exludes the first round
        events.GameEnd += whenGameEnds;
    }

    private static void whenActionHappens(ActionContext actionContext)
    {
        playerAction[actionContext.ThePlayer]++;
    }

    private static void whenTurnBegins(TurnContext turnContext)
    {
        AFilter.reset();
        playerTurn[turnContext.ThePlayer]++;
        playerAction[turnContext.ThePlayer] = 0;
        turnNumber++;

        if (hic.turnNumberText) hic.turnNumberText.text = turnNumber.ToString();
    }

    private static void whenRoundEnds()
    {
        roundNumber++;
        turnNumber = 0;
        Array.Clear(playerTurn, 0, playerTurn.Length);
        if (hic.roundNumberText) hic.roundNumberText.text = $"{roundNumber}";
    }

    private static void whenGameEnds()
    {
        roundNumber = 1;
        gameNumber++;
        if (hic.gameNumberText) hic.gameNumberText.text = $"{gameNumber}";
    }

    #endregion

}
