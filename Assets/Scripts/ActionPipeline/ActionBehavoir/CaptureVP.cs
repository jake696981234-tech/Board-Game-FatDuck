using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System;

public static class CaptureVPAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

        if (IsItLegal.IsLegal_CaptureVP(pieceId, offerBuild.gameIndex))
        {
            ushort vpCell = (ushort)bm.GetVictoryPointCellId();
            var theAction = new Action
            {
                kind = CaptureVP,
                pieceType = actorType,
                ActorsCellId = (ushort)cell,
                TargetCellId = vpCell,
                aux = 0
            };
            newOfferProvider.Emit(theAction, offerBuild);
        }
    }


    /// <summary>
    /// CAPTURE VP (targetless): legal if actor stands on VP cell.
    /// </summary>
    public static bool IsLegal(int actorPieceId, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return false;
        return originCell == bm.GetVictoryPointCellId();
    }

    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;

        gameState.ps[player].OnCaptureVP();
        gameState.AddCenterVictoryPoints(-1);
    }
}
