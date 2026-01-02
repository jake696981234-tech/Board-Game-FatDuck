using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System;

public static class NecroSpawnActions
{
        public static void CreateActions(
        int actorPid,
        int actorType,
        int actorCell,
        ref OfferBuild offerBuild)
        {
            var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

            if (bm.necroSpawnStore[actorType] < 0) return;
            if (CreateAction.PieceLimitReached(ref offerBuild)) return;
            int type = bm.necroSpawnStore[actorType];
            if (!CreateAction.isPieceTypeLegal(type, ref offerBuild)) return;
            if (Piece.isBuilding[type]) return;
            var emptyCells = BmCac.CellIdsRingAndLessthanRing(actorCell, Piece.necroSpawn_range[actorType], true, offerBuild.gameIndex);
            if (emptyCells.Length > 0) return;
            for (int i = 0; i < emptyCells.Length; i++)
            {
                Action theAction = new Game.Core.Action
                {
                    kind = NecroSpawn,
                    pieceType = (byte)actorType,
                    ActorsCellId = (ushort)actorCell,
                    TargetCellId = (ushort)emptyCells[i],
                    aux = (ushort)type,
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
        bm.PlacePieceRow(pid, player, (byte)theAction.aux, theAction.TargetCellId, Piece.maxHP[theAction.pieceType]);
        int g = Piece.digitItGives[(byte)theAction.aux];
        if (g >= 0) gameState.ps[player].GrantDigit(g);

        GameActions.RefreshConnectorState(gameIndex);
    }
}
