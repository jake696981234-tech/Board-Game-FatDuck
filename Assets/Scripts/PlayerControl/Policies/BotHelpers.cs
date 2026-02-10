using UnityEngine;
using System.Collections.Generic;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System;



public static class BotHelpers
{
    // public static void giveBestTargetByAim(ReadOnlySpan<Action> acts, int cellAim, int kind, int gameIndex) // needs Legal acts currently
    // {
    //     var bm = GameRegistry.game[gameIndex].boardModel;
    //     bool foundFirst = false;
    //     int best = 0;

    //     for (int i = 0; i < acts.Length; i++)
    //     {
    //         if (acts[i].kind != kind) continue;
    //         if (!foundFirst) 
    //         {
    //             best = bm.DistanceCells(acts[i].TargetCell, cellAim);
    //             foundFirst = true;
    //             continue;
    //         }
    //         int candidate =  

    //     }
    // }

    public static int bestTargetCellByCellAim(ReadOnlySpan<Action> acts, int cellAim, int kind, int gameIndex) // needs Legal acts currently
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        List<int> dstList = new();
        for (int i = 0; i < acts.Length; i++) if (acts[i].kind == kind) dstList.Add(bm.DistanceCells(acts[i].TargetCell, cellAim));
        return FindMin(dstList.ToArray());
    }

    static int FindMin(int[] candidates) // this only goes to 0 as min
    {
        int min = candidates[0];
        for (int i = 1; i < candidates.Length; i++)
        {
            if (candidates[i] == 0) return candidates[i];
            if (candidates[i] < min) min = candidates[i];
        }
        return min;
    }

    public static Action[] SetLegalOffers(Bot bot)
    {
        List<Action> returnList = new();
        for (int i = 0; i < bot.Offers.Length; i++) if (bot.ActionMask[i] != 0) returnList.Add(bot.Offers[i]);
        return returnList.ToArray();
    }

    public static int[] MakeRandomPlayerOrder(int excludePlayer, Bot bot)
    {
        int count = Info.playerCount;

        // Build pool 0..count-1, excluding excludePlayer
        int[] pool = new int[count - 1];
        int idx = 0;

        for (int i = 0; i < count; i++)
        {
            if (i == excludePlayer) continue;
            pool[idx++] = i;
        }

        // Shuffle (Fisher–Yates)
        for (int i = pool.Length - 1; i > 0; i--)
        {
            int j = bot.rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool;
    }


    // public static int[] MakeRandomPlayerOrder(int excludePlayer, Bot bot)
    // {
    //     // Build pool 1..playerCount, excluding one player
    //     int[] pool = new int[Info.playerCount - (excludePlayer >= 1 && excludePlayer <= Info.playerCount ? 1 : 0)];
    //     int idx = 0;
    //     for (int i = 1; i <= Info.playerCount; i++)
    //     {
    //         if (i == excludePlayer) continue;
    //         pool[idx++] = i;
    //     }
    //     // Shuffle
    //     for (int i = pool.Length - 1; i > 0; i--)
    //     {
    //         int j = bot.rng.Next(i + 1);
    //         (pool[i], pool[j]) = (pool[j], pool[i]);
    //     }
    //     return pool;
    // }
}