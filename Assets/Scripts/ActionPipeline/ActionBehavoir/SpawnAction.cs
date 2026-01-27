using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System;

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
            int cellCount = bm.GetCellCount();
            for (int c = 0; c < cellCount; c++)
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

    public static bool IsLegal(int actorPid, int actorType, in Game.Core.Action a, byte currentPlayer, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var gameState = GameRegistry.game[gameIndex].gameState;
        var controller = GameRegistry.game[gameIndex].gameController;


        int targetType = Piece.spawn_targetType[actorType];
        if (targetType < 0 || targetType >= Piece.typeCount)
        {
            Debug.Log("IsLegal_Spawner - (targetType < 0 || targetType >= PieceDefinition.typeCount) Returned False");
            return false;
        }

        int amount = Piece.spawn_pieceAmount[actorType];
        int range = Piece.spawn_range[actorType];
        bool once = Piece.spawn_isOnlyOncePerTurn[actorType];

        if (once && bm.spawnerUsedThisTurn.Contains(actorPid))
        {
            Debug.Log("IsLegal_Spawner - (once && bm.spawnerUsedThisTurn.Contains(actorPid)) Returned False)");
            return false;
        }


        int origin = bm.GetPieceCell(actorPid);
        if (origin < 0)
        {
            Debug.Log("IsLegal_Spawner - (origin < 0) Returned False)");
            return false;
        }


        // Digit gate for target type
        int reqDigit = Piece.requiredDigit[(byte)targetType];
        if (reqDigit >= 0 && !gameState.ps[currentPlayer].HasDigit(reqDigit))
        {
            Debug.Log("IsLegal_Spawner - (reqDigit >= 0 && !gameState.ps[currentPlayer].HasDigit(reqDigit)) Returned False)");
            return false;
        }


        // Gather empty cells in range with LOS
        int[] scratch = Scratch.GetScratchCellBuffer(gameIndex);
        int cellCount = bm.GetCellCount();
        int emptyCount = 0;
        for (int c = 0; c < cellCount; c++)
        {
            if (!bm.IsEmpty(c)) continue;
            int dist = bm.Distance(origin, c);
            if (dist < 1 || dist > range) continue;
            if (!BmCac.LineOfSightClear(origin, c, gameIndex)) continue;
            scratch[emptyCount++] = c;
        }

        if (emptyCount <= 0)
        {
            Debug.Log("IsLegal_Spawner - (emptyCount <= 0) Returned False)");
            return false;
        }


        // Piece limit check: allow as many as possible
        int availableLimit = int.MaxValue;
        if (controller.PieceLimitEnabled && controller.PieceLimitPerPlayer > 0)
            availableLimit = controller.PieceLimitPerPlayer - bm.GetPieceCountForPlayer(currentPlayer);

        int possible = Math.Min(amount, Math.Min(emptyCount, Math.Max(0, availableLimit)));
        return possible > 0;
    }

    public static void Apply(in Action theAction, byte currentPlayer, int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;

        var controller = GameRegistry.game[gameIndex].gameController;
        var bm = GameRegistry.game[gameIndex].boardModel;

        int actorPid = bm.GetCellOccupant(theAction.ActorsCell);
        if (actorPid < 0) return;


        int targetType = Piece.spawn_targetType[bm.GetPieceTypeFromCell(theAction.ActorsCell)];
        int amount = Piece.spawn_pieceAmount[bm.GetPieceTypeFromCell(theAction.ActorsCell)];
        int range = Piece.spawn_range[bm.GetPieceTypeFromCell(theAction.ActorsCell)];

        int origin = bm.GetPieceCell(actorPid);
        int[] empties = Scratch.GetScratchCellBuffer(gameIndex);
        int cellCount = bm.GetCellCount();
        int eCount = 0;
        for (int c = 0; c < cellCount; c++)
        {
            if (!bm.IsEmpty(c)) continue;
            int dist = bm.Distance(origin, c);
            if (dist < 1 || dist > range) continue;
            if (!BmCac.LineOfSightClear(origin, c, gameIndex)) continue;
            empties[eCount++] = c;
        }
        Array.Sort(empties, 0, eCount);

        int availableLimit = int.MaxValue;
        if (controller.PieceLimitEnabled && controller.PieceLimitPerPlayer > 0)
            availableLimit = controller.PieceLimitPerPlayer - bm.GetPieceCountForPlayer(currentPlayer);

        int canCreate = Math.Min(amount, Math.Min(eCount, Math.Max(0, availableLimit)));
        if (canCreate <= 0) return;

        // Prefer the chosen cell (a.TargetCell) if still legal; then fill remaining
        int spawned = 0;

        bool ChosenCellIsLegal = false;
        for (int i = 0; i < eCount; i++)
        {
            if (empties[i] == theAction.TargetCell)
            {
                ChosenCellIsLegal = true;
                break;
            }
        }

        if (ChosenCellIsLegal && spawned < canCreate)
        {
            int pid = bm.AllocateRow();
            bm.PlacePieceRow(pid, currentPlayer, (byte)targetType, theAction.TargetCell, Piece.maxHP[targetType]);
            int g = Piece.digitItGives[(byte)targetType];
            if (g >= 0) gameState.ps[currentPlayer].GrantDigit(g);
            spawned++;
        }

        for (int i = 0; i < eCount && spawned < canCreate; i++)
        {
            int cell = empties[i];
            if (cell == theAction.TargetCell) continue; // already used chosen cell
            int pid = bm.AllocateRow();
            bm.PlacePieceRow(pid, currentPlayer, (byte)targetType, cell, Piece.maxHP[targetType]);
            int g = Piece.digitItGives[(byte)targetType];
            if (g >= 0) gameState.ps[currentPlayer].GrantDigit(g);
            spawned++;
        }

        // Mark once-per-turn flag
        if (Piece.spawn_isOnlyOncePerTurn[bm.GetPieceTypeFromCell(theAction.ActorsCell)])
            bm.spawnerUsedThisTurn.Add(actorPid);

        GameActions.RefreshConnectorState(gameIndex);
    }
}
