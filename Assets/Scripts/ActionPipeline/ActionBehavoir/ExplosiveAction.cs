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
                ActorsCell = cell,
            };
            OfferProvider.Emit(theAction, ref offerBuild);
    }


    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int dmg = Piece.explosive_damage[theAction.TargetType];
        int maxRange = Piece.explosive_range[theAction.TargetType];
        bool friendlyFire = Piece.explosive_isFriendlyFire[theAction.TargetType];

        Debug.Log($"dmg={dmg} maxRange={maxRange} friendlyFire={friendlyFire} dmg={dmg}");

        // Span<int> occcells = Scratch.GetScratchCellBuffer(gameIndex);
        var occCells = BmCac.CellIdsRingAndLessthanRing(theAction.ActorsCell, maxRange, false, true, gameIndex);
        Debug.Log($"CellIdsRingAndLessthanRing={occCells.Count}");
        for (int i = 0; i < occCells.Count; i++)
        {
            if (occCells[i] == theAction.ActorsCell) continue;
            int victim = bm.GetCellOccupant(occCells[i]);
            // Debug.Log($"[ExplosiveAction]   candidateCell={occcells[i]} victimPieceId={victim} owner={(victim >= 0 && bm.IsValidPieceId(victim) ? bm.pieceOwner[victim].ToString() : "invalid")}");
            if (!friendlyFire && bm.pieceOwner[victim] == player)
            {
                Debug.Log($"[ExplosiveAction]   skippedFriendlyFire victimPieceId={victim}");
                continue;
            }
            bool killed = GameActions.ApplyDamageWithCapital(theAction.ActorsCell, victim, dmg, gameIndex);
            Debug.Log($"[ExplosiveAction]   damageApplied killed={killed}");
            if (killed)
            {
                Debug.Log($"[ExplosiveAction]   pieceKilled victimPieceId={victim}");
                if (Piece.explosive_isKillItself[theAction.TargetType]) { GameActions.pieceKilled(victim, gameIndex); } {GameActions.pieceKilled(victim, gameIndex, theAction);}
            }
        }
        if (!Piece.explosive_isKillItself[theAction.TargetType]) return;
        int selfPid = bm.GetCellOccupant(theAction.ActorsCell);
        Debug.Log($"[ExplosiveAction] selfDestruct flag true, killing selfPid={selfPid}");
        GameActions.pieceKilled(selfPid, gameIndex);
    }
}
