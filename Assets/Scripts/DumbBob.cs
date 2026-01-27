// using UnityEngine;
// using System;
// using Game.Core;
// using Action = Game.Core.Action;
// using static Game.Core.ActionKind; // import enum values

// public class DumbBob
// {
//     private int FactoryPayOutAim;
//     public int PickAction(in OfferQuery q,
//                           ReadOnlySpan<Action> acts,
//                           ReadOnlySpan<float> costs,
//                           ReadOnlySpan<byte> mask,
//                           int gameIndex,
//                           byte playerId)
//     {
//         var gameState = GameRegistry.game[gameIndex].gameState;
//         //Gate to sperfic factions
        
//         for (int i = 0; i < acts.Length; i++)
//         {
//             countOfSortedActions[acts[i].kind]++;
//             sortedActions[acts[i].kind, countOfSortedActions[acts[i].kind]] = i;
//         }
//         if (countOfSortedActions[CaptureVP] > 0) return sortedActions[CaptureVP, 0];
//         if (countOfSortedActions[CoreDamage] > 0) return sortedActions[CoreDamage, 0];
//         float chance = UnityEngine.Random.value; 

//         if (PassiveActions.ComputeFactoryIncome((int)playerId, gameIndex) < FactoryPayOutAim && (countOfSortedActions[CaptureVP] > 0))
//         {
//             if (a
//         }

//         return -1;
//     }

//     private int[,] sortedActions = new int[16 , 217]; //keyed by action kind
//     private int[] countOfSortedActions = new int [16];
// }
