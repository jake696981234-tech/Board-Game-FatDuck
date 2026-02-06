using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System;
using static Game.Core.GameActions;


public static class SpawnAction
{
    public static void CreateActions(
    int actorPid,
    int actorType,
    int actorCell,
    ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        var gameState = GameRegistry.game[offerBuild.gameIndex].gameState;

        int amount = Piece.spawn_pieceAmount[actorType];
        int range = Piece.spawn_range[actorType];
        int targetType = Piece.spawn_targetType[actorType];
        bool once = Piece.spawn_isOnlyOncePerTurn[actorType];
        if (amount <= 0 || targetType < 0 || targetType >= Piece.typeCount) return;

        // Digit gate; buildable override allowed
        int reqDigit = Piece.requiredDigit[(byte)targetType];
        if (reqDigit >= 0 && !gameState.ps[offerBuild.query.playerId].HasDigit(reqDigit)) return;

        // Collect empty, LOS-valid cells within range from launcher
        int[] empties = Scratch.GetScratchCellBuffer(offerBuild.gameIndex);
        int eCount = 0;
        for (int c = 0; c < Info.totalCells; c++)
        {
            if (!bm.IsEmpty(c)) continue;
            int dist = bm.Distance(actorCell, c);
            if (dist < 1 || dist > range) continue;
            if (!BmCac.LineOfSightClear(actorCell, c, offerBuild.gameIndex)) continue;
            empties[eCount++] = c;
        }
        if (eCount <= 0) return;
        Array.Sort(empties, 0, eCount);

        // Piece limit: allow as many as possible
        int availableLimit = int.MaxValue;
        if (offerBuild.query.pieceLimitEnabled && offerBuild.query.pieceLimitPerPlayer > 0)
            availableLimit = offerBuild.query.pieceLimitPerPlayer - bm.GetPieceCountForPlayer(offerBuild.query.playerId);
        int possible = Math.Min(amount, Math.Min(eCount, Math.Max(0, availableLimit)));
        if (possible <= 0) return;

        // Once-per-turn flag cannot be observed here; Perform will reject if already used.

        // Emit one action per legal empty cell (deterministic order)
        for (int i = 0; i < eCount; i++)
        {
            var theAction = new Action
            {
                kind = Spawner,
                ActorsCell = actorCell,
                TargetCell = empties[i],
            };
            OfferProvider.Emit(theAction, ref offerBuild);
        }
    }


    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        if (Piece.spawn_isOnlyOncePerTurn[bm.GetPieceTypeFromCell(theAction.ActorsCell)]) bm.spawnerUsedThisTurn.Add(bm.occupantPieceId[theAction.ActorsCell]);
        placePiece(wallConfig: 0, createdPieceType: Piece.spawn_targetType[bm.GetPieceTypeFromCell(theAction.ActorsCell)], targetCell: theAction.TargetCell, player: player, gameIndex: gameIndex);
    }
}
