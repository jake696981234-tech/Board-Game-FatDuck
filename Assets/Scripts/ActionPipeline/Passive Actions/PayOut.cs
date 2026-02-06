using UnityEngine;
using System.Collections.Generic;
using System;
using Game.Core;
using Action = Game.Core.Action;

public static class Payout
{
    public static void GiveMePayPlayersOut(int gameIndex, int player)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int Payment = 0;
        Payment += GroupFactoryAction.GiveMeTotalGroupFactoryForPlayer(player: player, gameIndex: gameIndex);
        for (int i = 0; i < bm.pieceCount; i++)
        {
            if (bm.pieceOwner[i] != player) continue;
            if (!Piece.groupFactory_enabled[i]) continue;
            if (Piece.factory_enabled[bm.pieceType[i]]) Payment += FactoryAction.GiveMePiecesFactoryPayOut(pieceType: bm.pieceType[i], gameIndex: gameIndex);
            if (Piece.Instantfactory_enabled[bm.pieceType[i]]) Payment += InstantFactoryAction.GiveMePiecesInstantFactoryPenality(pieceId: i, pieceType: bm.pieceType[i], gameIndex: gameIndex);
        }
    }












}
