using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System;

public static class NecroSpawnAction
{
        public static void CreateActions(
        int actorPid,
        int actorType,
        int actorCell,
        ref OfferBuild offerBuild)
        {
            var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

            if (bm.necroSpawnStore[actorPid] < 0) return;
            if (CreateAction.PieceLimitReached(ref offerBuild)) return;
            int type = bm.necroSpawnStore[actorPid];
            if (!CreateAction.isPieceTypeLegal(type, ref offerBuild)) return;
            if (Piece.isBuilding[type]) return;
            var emptyCells = BmCac.CellIdsRingAndLessthanRing(originCell: actorCell, ringSize: Piece.necroSpawn_range[actorType], requireEmpty: true, requireOcc: false, requireOwned: -1, gameIndex: offerBuild.gameIndex);
            Debug.Log($"Necro Spawn, Empty Cells amount:{emptyCells.Count}");
            if (emptyCells.Count < 1) return;
            for (int i = 0; i < emptyCells.Count; i++)
            {
                Action theAction = new Action
                {
                    kind = NecroSpawn,
                    ActorsCell = actorCell,
                    TargetCell = emptyCells[i],
                };
                OfferProvider.Emit(theAction, ref offerBuild);
            }
        }

    public static void Record(int gameIndex, int pieceIDKilled)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int victimPieceOwner = bm.pieceOwner[pieceIDKilled];

        for (int recipientNecro = 0; recipientNecro < bm.pieceCount; recipientNecro++)
        {
            //trying to find for piece with necro spawn, that is owned by the same player of victim piece
            if (bm.pieceOwner[recipientNecro] != victimPieceOwner) continue;
            int recipientType = bm.pieceType[recipientNecro];
            if (!Piece.necroSpawn_enabled[recipientType]) continue;
            bm.necroSpawnStore[recipientNecro] = recipientType;
        }
    }

    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        var bm = GameRegistry.game[gameIndex].boardModel;

        int pid = bm.AllocateRow();
        bm.PlacePieceRow(pid, player, (byte)bm.necroSpawnStore[pid], theAction.TargetCell, Piece.maxHP[bm.necroSpawnStore[pid]]);
        int g = Piece.digitItGives[bm.necroSpawnStore[pid]];
        if (g >= 0) gameState.ps[player].GrantDigit(g);

        GameActions.RefreshConnectorState(gameIndex);
    }
}
