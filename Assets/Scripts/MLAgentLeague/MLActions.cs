// using System;
// using System.Runtime.CompilerServices;
// using UnityEngine;
// using Unity.MLAgents;
// using Unity.MLAgents.Sensors;
// using Unity.MLAgents.Actuators;
// using Game.Core; // Action, OfferQuery, GameState, PlayerState

// public static class MLActions
// {
//     public enum MLState
//     {
//         ChoosingKind = 0,
//         ChoosingActorsCell = 2,
//         ChoosingTargetCell = 3,
//         ChoosingPieceType = 4,
//         ChoosingWallConfig = 5,
//         ChoosingInstakeCellID = 6,
//     }

//     private static void peformAction(Action theAction)
//     {
//         mlState = ChoosingKind;
//         Array.Clear(ChosenAction, 0, ChosenAction.Length);
//         _gs.Perform(theAction, _offers);
//     }

// }
