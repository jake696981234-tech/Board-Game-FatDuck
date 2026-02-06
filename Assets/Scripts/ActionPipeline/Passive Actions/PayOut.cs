using UnityEngine;
using System.Collections.Generic;
using System;
using Game.Core;
using Action = Game.Core.Action;

public static class Payout
{
    public static int GiveMePayPlayersOut(int player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var gameState = GameRegistry.game[gameIndex].gameState;
        int payment = 0;
        payment += GroupFactoryAction.GiveMeTotalGroupFactoryForPlayer(player: player, gameIndex: gameIndex);
        for (int pieceId = 0; pieceId < bm.pieceCount; pieceId++)
        {
            if (bm.pieceOwner[pieceId] != player) continue;
            payment += bm.addToEndRoundPayout[pieceId];
            if (Piece.factory_enabled[bm.pieceType[pieceId]]) payment += FactoryAction.GiveMePiecesFactoryPayOut(pieceType: bm.pieceType[pieceId], gameIndex: gameIndex);
            if (Piece.Instantfactory_enabled[bm.pieceType[pieceId]]) payment += InstantFactoryAction.GiveMePiecesInstantFactoryPenality(pieceId: pieceId, pieceType: bm.pieceType[pieceId], gameIndex: gameIndex);
            if (Piece.Instantfactory_enabled[bm.pieceType[pieceId]]) payment += InstantFactoryAction.GiveMePiecesInstantFactoryPenality(pieceId: pieceId, pieceType: bm.pieceType[pieceId], gameIndex: gameIndex);
        }
        payment += ComputePlayerVPReward(ps: gameState.ps[player]) + ComputePlayeroreDamageReward(ps: gameState.ps[player]);
        return payment;
    }

    public static int ComputePlayerVPReward(PlayerState ps)
    {
        return ps.vpGainedThisRound * Info.budgetBonusForVP;
    }

    public static int ComputePlayeroreDamageReward(PlayerState ps)
    {
        return ps.coreHitsThisRound * Info.budgetBonusForCoreDamage;
    }
}
