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
        if (!PieceDefinition.captureVP_enabled[actorType]) return;

        var theAction = new Action
        {
            kind = CaptureVP,
            pieceType = actorType,
            ActorsCellId = (ushort)cell,
            TargetCellId = vpCell,
            aux = 0
        };
        OfferProvider.Emit(theAction, ref offerBuild);
    }



    /// <summary>
    /// CAPTURE VP (targetless): legal if actor stands on VP cell.
    /// </summary>
    public static bool IsLegal(int actorPieceId, ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

        
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
