using UnityEngine;
using System.Collections.Generic;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System;
using static Game.Core.GameActions;


public static class ExplosiveAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    {
        var theAction = new Action
            {
                kind = Explosive,
                ActorsCell = cell,
            };
            OfferProvider.Emit(theAction, ref offerBuild);
    }


    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var occCells = BmCac.CellIdsRingAndLessthanRing(theAction.ActorsCell, Piece.explosive_range[Piece.explosive_damage[bm.GetPieceTypeFromCell(theAction.ActorsCell)]], false, true, gameIndex);
        for (int i = 0; i < occCells.Count; i++)
        {
            if (occCells[i] == theAction.ActorsCell) continue;
            if (!Piece.explosive_isFriendlyFire[bm.GetPieceTypeFromCell(theAction.ActorsCell)] && bm.GetPieceOwnerFromCell(occCells[i]) == player) continue;
            if (ApplyDamageToPiece(theAction.ActorsCell, bm.occupantPieceId[occCells[i]], Piece.explosive_damage[bm.GetPieceTypeFromCell(theAction.ActorsCell)], gameIndex)) pieceKilled(victimsCell: occCells[i], actorsCell: theAction.ActorsCell, gameIndex: gameIndex);
        }
        if (!Piece.explosive_isKillItself[bm.GetPieceTypeFromCell(theAction.ActorsCell)]) return;
        pieceKilledWithNoTriggers(victimsCell: theAction.ActorsCell, gameIndex: gameIndex);
    }
}
