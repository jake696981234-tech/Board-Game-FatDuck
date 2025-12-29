using UnityEngine;
using System.Collections.Generic;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System;


public static class ExplosiveAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    {
        var theAction = new Action
            {
                kind = explosive,
                pieceType = actorType,
                ActorsCellId = (ushort)cell,
                TargetCellId = (ushort)cell,
                aux = 0,
            };
            OfferProvider.Emit(theAction, ref offerBuild);
    }

    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int dmg = Piece.explosive_damage[theAction.pieceType];
        int maxRange = Piece.explosive_range[theAction.pieceType];
        bool friendlyFire = Piece.explosive_isFriendlyFire[theAction.pieceType];

        for (int range = 0; range <= maxRange; range++)
        {
            int[] pieceVictims = Scratch.GetScratchCellBuffer(gameIndex);
            int howManyVictims = BmAbilityCac.pieceIdsRingAroundCell(theAction.ActorsCellId, range, pieceVictims, gameIndex);
            int[] victimCellIds = new int[howManyVictims];
            for (int i = 0; i < howManyVictims; i++) { victimCellIds[i] = bm.pieceCellId[pieceVictims[i]]; }
            if (howManyVictims <= 0) 
            {
                for (int i = 0; i < howManyVictims; i++)
                {
                    int victim = bm.GetCellOccupant(victimCellIds[i]);
                    if (!friendlyFire)
                    {
                         if (bm.pieceOwner[victim] == player) continue;
                    }
                    bool killed = GameActions.ApplyDamageWithCapital(theAction.ActorsCellId, victim, dmg, gameIndex);
                    if (killed)  GameActions.pieceKilled(bm.GetCellOccupant(victimCellIds[i]), gameIndex, theAction);
                }
            }
        }
        if (Piece.explosive_isKillItself[theAction.pieceType]) GameActions.pieceKilled(bm.GetCellOccupant(theAction.ActorsCellId), gameIndex);
    }
}