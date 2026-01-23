using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System;

public static class CaptureVPAction
{
    public static void CreateActions(byte actorType, int cell, ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

        ushort vpCell = (ushort)bm.GetVictoryPointCellId();
        if (cell != vpCell) return;
        if (!Piece.captureVP_enabled[actorType]) return;

        var theAction = new Action
        {
            kind = CaptureVP,
            ActorsCell = cell,
        };
        OfferProvider.Emit(theAction, ref offerBuild);
    }
    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;

        gameState.ps[player].OnCaptureVP();
        gameState.AddCenterVictoryPoints(-1);
    }
}
