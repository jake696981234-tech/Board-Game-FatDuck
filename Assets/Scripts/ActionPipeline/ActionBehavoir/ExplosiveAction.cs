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
                kind = Explosive,
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

        Debug.Log($"[ExplosiveAction] Apply actorType={theAction.pieceType} actorCell={theAction.ActorsCellId} player={player} dmg={dmg} range={maxRange} friendlyFire={friendlyFire} gameIndex={gameIndex}");

        Span<int> occcells = Scratch.GetScratchCellBuffer(gameIndex);
        int howManyVictims = BmCac.OccCellIdsRingAndLessthanRing(theAction.ActorsCellId, maxRange, occcells, gameIndex);
        Debug.Log($"[ExplosiveAction] CellsWithOccupantsWithinRange={howManyVictims} bufferLen={occcells.Length}");
        for (int i = 0; i < howManyVictims; i++)
        {
            int victim = bm.GetCellOccupant(occcells[i]);
            Debug.Log($"[ExplosiveAction]   candidateCell={occcells[i]} victimPieceId={victim} owner={(victim >= 0 && bm.IsValidPieceId(victim) ? bm.pieceOwner[victim].ToString() : \"invalid\")}");
            if (!friendlyFire && bm.pieceOwner[victim] == player)
            {
                Debug.Log($"[ExplosiveAction]   skippedFriendlyFire victimPieceId={victim}");
                continue;
            }
            bool killed = GameActions.ApplyDamageWithCapital(theAction.ActorsCellId, victim, dmg, gameIndex);
            Debug.Log($"[ExplosiveAction]   damageApplied killed={killed}");
            if (killed)
            {
                Debug.Log($"[ExplosiveAction]   pieceKilled victimPieceId={victim}");
                GameActions.pieceKilled(victim, gameIndex, theAction);
            }
        }
        if (Piece.explosive_isKillItself[theAction.pieceType])
        {
            int selfPid = bm.GetCellOccupant(theAction.ActorsCellId);
            Debug.Log($"[ExplosiveAction] selfDestruct flag true, killing selfPid={selfPid}");
            GameActions.pieceKilled(selfPid, gameIndex);
        }
    }
}
