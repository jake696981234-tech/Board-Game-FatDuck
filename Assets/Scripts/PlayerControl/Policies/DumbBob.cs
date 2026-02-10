using UnityEngine;
using System.Collections.Generic;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System;
using static BotHelpers;



public static class DumbBob
{
    // Gate to sperfic factions

    public static void PickAction(in OfferQuery q, //needs to change back to an return int
                          ReadOnlySpan<Action> acts,
                          ReadOnlySpan<float> costs,
                          ReadOnlySpan<byte> mask,
                          int gameIndex,
                          byte playerId, Bot bot) // to do- remove bot from here
    {
        bot.LegalOffers = SetLegalOffers(acts: acts, mask: mask);
        bot.availableActions = giveMeSortedActions(bot: bot);
        giveMePriotisedActionIndex(bot: bot);
    }

    public static int giveMePriotisedActionIndex(Bot bot)
    {
        for (int i = 0; i < Info.dumbBobAuthoring.Priority.Length; i++)
        {
            for (int j = 0; j < bot.availableActions.Length; j++)
            {
                if ((int)Info.dumbBobAuthoring.Priority[i] == bot.availableActions[j])
                {
                    if (!handle(Info.dumbBobAuthoring.Priority[i], bot: bot, out int returnAction)) continue;
                    return returnAction;
                }
            }
        }
        Debug.Log($"Should not be possible, FIX ME!");
        return findMeActionWith(bot: bot, kind: EndTurn, actorsCell: -1, targetCell: -1, targetType: -1, wallConfig: -1, intakeCell: -1);
    }

    private static bool handle(Piece.AbilityKind abilityKind, Bot bot, out int returnAction)
    {
        returnAction = -1;
        switch ((byte)abilityKind)
        {
            case CoreDamage:
                returnAction = findMeActionWith(bot: bot, kind: (int)abilityKind, actorsCell: -1, targetCell: -1, targetType: -1, wallConfig: -1, intakeCell: -1);
                return true;
            case CaptureVP:
                returnAction = findMeActionWith(bot: bot, kind: (int)abilityKind, actorsCell: -1, targetCell: -1, targetType: -1, wallConfig: -1, intakeCell: -1);
                return true;
            case Spawner:
                return trySpawnAction(bot: bot, out returnAction);
            case Create:
                if (!tryCreateVilliage(bot: bot, out returnAction)) enterPath(kind: Create, bot: bot);
                return true;
            case Move:
                enterPath(kind: Move, bot: bot);
                return true;
            case Shoot:
                enterPath(kind: Shoot, bot: bot);
                return true;
            case Hop:
                enterPath(kind: Hop, bot: bot);
                return true;
            case EndTurn:
                returnAction = findMeActionWith(bot: bot, kind: (int)abilityKind, actorsCell: -1, targetCell: -1, targetType: -1, wallConfig: -1, intakeCell: -1);
                return true;
        }
        Debug.Log($"Should not be possible, failed to find action- FIX ME!");
        return false;
    }

    private static int enterPath(int kind, Bot bot)
    {
        return findActionNature(findRandomWeightedAction(theKind: (Piece.AbilityKind)kind, bot: bot), bot: bot);
    }

    public static int findActionNature(int kind, Bot bot)
    {
        switch (kind)
        {
            case Create:
                return PickRandomKindFromAvailable(Kind: kind, bot: bot);
            case Move:
                return bestTargetCellByCellAim(acts: bot.LegalOffers, cellAim: cellAim(bot: bot), kind: Move, gameIndex: bot.gameIndex);
            case Shoot:
                return bestTargetCellByCellAim(acts: bot.LegalOffers, cellAim: cellAim(bot: bot), kind: Shoot, gameIndex: bot.gameIndex);
            case Hop:
                return bestTargetCellByCellAim(acts: bot.LegalOffers, cellAim: cellAim(bot: bot), kind: Hop, gameIndex: bot.gameIndex);

        }
        Debug.Log($"Should not be possible, failed to find action- FIX ME!");
        return -1;
    }

    private static int PickRandomKindFromAvailable(int Kind, Bot bot) => PickRandom(options: giveMeAllActionsOfKind(kind: Kind, bot: bot), bot: bot);
    private static int PickRandom(int[] options, Bot bot) => options[bot.rng.Next(options.Length)];



    private static byte findRandomWeightedAction(Piece.AbilityKind theKind, Bot bot)
    {
        int totalWeight = 0;
        // 1️⃣ Sum weights of available actions
        for (int i = 0; i < bot.availableActions.Length; i++)
        {
            if (!Info.BobWeights[(int)theKind].TryGetValue((Piece.AbilityKind)bot.availableActions[i], out int weight)) continue;
            totalWeight += weight;
        }
        // 2️⃣ Nothing valid → EndTurn
        if (totalWeight == 0)
        {
            Debug.Log($"Should not be possible, likley problem with availableActions- FIX ME!");
            return EndTurn; // EndTurn
        }
        // 3️⃣ Roll
        int roll = bot.rng.Next(totalWeight);
        // 4️⃣ Select
        for (int i = 0; i < bot.availableActions.Length; i++)
        {
            var action = bot.availableActions[i];
            if (!Info.BobWeights[(int)theKind].TryGetValue((Piece.AbilityKind)action, out int weight)) continue;
            if (weight <= 0) continue;
            if (roll < weight) return (byte)action;
            roll -= weight;
        }
        Debug.Log($"Should not be possible, likley problem with availableActions- FIX ME!");
        return EndTurn;
    }

    public static int cellAim(Bot bot)
    {
        var bm = GameRegistry.game[bot.gameIndex].boardModel;
        if (bot.isCellAimVP) return bm._vpCellId;
        var gameState = GameRegistry.game[bot.gameIndex].gameState;
        for (int i = 0; i < bot.playerTargets.Length; i++) if (!gameState.ps[bot.playerTargets[i]].isEliminated) return bm._coreCellIdByPlayer[bot.playerTargets[i]];
        Debug.Log($"Should not be possible, failed to find Target- FIX ME!");
        return bm._vpCellId;
    }


    private static bool trySpawnAction(Bot bot, out int returnAction)
    {
        var gameState = GameRegistry.game[bot.gameIndex].gameState;
        returnAction = -1;
        if (Info.dumbBobAuthoring.PayOutAimByRound[gameState.currentRoundNumber] < Payout.GiveMePayPlayersOut(player: bot.playerId, gameIndex: bot.gameIndex)) return false;
        returnAction = findMeActionWith(bot: bot, kind: (int)Spawner, actorsCell: -1, targetCell: -1, targetType: -1, wallConfig: -1, intakeCell: -1);
        return true;
    }

    private static bool tryCreateVilliage(Bot bot, out int returnAction)
    {
        var gameState = GameRegistry.game[bot.gameIndex].gameState;
        returnAction = -1;
        if (Info.dumbBobAuthoring.PayOutAimByRound[gameState.currentRoundNumber] < Payout.GiveMePayPlayersOut(player: bot.playerId, gameIndex: bot.gameIndex)) return false;
        returnAction = findMeActionWith(bot: bot, kind: (int)Create, actorsCell: -1, targetCell: -1, targetType: Info.dumbBobAuthoring.VillagePieceTypeId, wallConfig: -1, intakeCell: -1);
        if (returnAction == -1) return false;
        return true;
    }

    public static int[] giveMeSortedActions(Bot bot)
    {
        List<int> returnList = new();
        HashSet<int> seen = new HashSet<int>();
        for (int i = 0; i < bot.LegalOffers.Length; i++)
        {
            if (seen.Add(bot.LegalOffers[i].kind)) returnList.Add(bot.LegalOffers[i].kind);
        }
        return returnList.ToArray();
    }

    private static int findMeActionWith(Bot bot, int kind, int actorsCell, int targetCell, int targetType, int wallConfig, int intakeCell)
    {
        for (int i = 0; i < UIBridge._count; i++)
        {
            if (kind != bot.LegalOffers[i].kind && kind != -1) continue;
            if (actorsCell != bot.LegalOffers[i].kind && kind != -1) continue;
            if (targetCell != bot.LegalOffers[i].kind && kind != -1) continue;
            if (targetType != bot.LegalOffers[i].kind && kind != -1) continue;
            if (wallConfig != bot.LegalOffers[i].kind && kind != -1) continue;
            if (intakeCell != bot.LegalOffers[i].kind && kind != -1) continue;
            return i;
        }
        if (Info.dumbBobAuthoring.VillagePieceTypeId == targetType) return -1;
        Debug.Log($"Should not be possible, failed to find action- FIX ME!");
        return -1;
    }

    private static int[] giveMeAllActionsOfKind(int kind, Bot bot)
    {
        List<int> returnList = new();
        for (int i = 0; i < UIBridge._count; i++)
        {
            if (kind != bot.LegalOffers[i].kind) continue;
            returnList.Add(i);
        }
        if (returnList.Count == 0) Debug.Log($"Should not be possible, failed to find action- FIX ME!");
        return returnList.ToArray();
    }



    // public static void letssee()
    // {
    // for (int i = 0; i < acts.Length; i++)
    //     {
    //         countOfSortedActions[acts[i].kind]++;
    //         sortedActions[acts[i].kind, countOfSortedActions[acts[i].kind]] = i;
    //     }
    //     if (countOfSortedActions[CaptureVP] > 0) return sortedActions[CaptureVP, 0];
    //     if (countOfSortedActions[CoreDamage] > 0) return sortedActions[CoreDamage, 0];
    //     float chance = UnityEngine.Random.value; 

    //     if (PassiveActions.ComputeFactoryIncome((int)playerId, gameIndex) < FactoryPayOutAim && (countOfSortedActions[CaptureVP] > 0))
    //     {
    //         if (a
    //     }

    //     return -1;
    // }

    // private int[,] sortedActions = new int[16 , 217]; //keyed by action kind
    // private int[] countOfSortedActions = new int [16];


}
