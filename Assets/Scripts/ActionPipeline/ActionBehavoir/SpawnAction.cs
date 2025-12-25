using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class SpawnAction
{
    public static void CreateActions()
    {
        EmitSpawnerActions(q, pieceId, actorType, cell, ref write, ref total, cap, outActions, outCosts, outMask, gameIndex, player);
    }

    public static bool IsLegal(int actorPid, int actorType, in Game.Core.Action a, byte currentPlayer, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var gameState = GameRegistry.game[gameIndex].gameState;
        var controller = GameRegistry.game[gameIndex].gameController;


        int targetType = PieceDefinition.spawn_targetType[actorType];
        if (targetType < 0 || targetType >= PieceDefinition.typeCount)
        {
            Debug.Log("IsLegal_Spawner - (targetType < 0 || targetType >= PieceDefinition.typeCount) Returned False");
            return false;
        }

        int amount = PieceDefinition.spawn_pieceAmount[actorType];
        int range = PieceDefinition.spawn_range[actorType];
        bool once = PieceDefinition.spawn_isOnlyOncePerTurn[actorType];

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
        int reqDigit = PieceDefinition.requiredDigit[(byte)targetType];
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
            if (!BmAbilityCac.LineOfSightClear(origin, c, gameIndex)) continue;
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

        int actorPid = bm.GetCellOccupant(theAction.ActorsCellId);
        if (actorPid < 0) return;


        int targetType = PieceDefinition.spawn_targetType[theAction.pieceType];
        int amount = PieceDefinition.spawn_pieceAmount[theAction.pieceType];
        int range = PieceDefinition.spawn_range[theAction.pieceType];

        int origin = bm.GetPieceCell(actorPid);
        int[] empties = Scratch.GetScratchCellBuffer(gameIndex);
        int cellCount = bm.GetCellCount();
        int eCount = 0;
        for (int c = 0; c < cellCount; c++)
        {
            if (!bm.IsEmpty(c)) continue;
            int dist = bm.Distance(origin, c);
            if (dist < 1 || dist > range) continue;
            if (!BmAbilityCac.LineOfSightClear(origin, c, gameIndex)) continue;
            empties[eCount++] = c;
        }
        Array.Sort(empties, 0, eCount);

        int availableLimit = int.MaxValue;
        if (controller.PieceLimitEnabled && controller.PieceLimitPerPlayer > 0)
            availableLimit = controller.PieceLimitPerPlayer - bm.GetPieceCountForPlayer(currentPlayer);

        int canCreate = Math.Min(amount, Math.Min(eCount, Math.Max(0, availableLimit)));
        if (canCreate <= 0) return;

        // Prefer the chosen cell (a.TargetCellId) if still legal; then fill remaining
        int spawned = 0;

        bool ChosenCellIsLegal = false;
        for (int i = 0; i < eCount; i++)
        {
            if (empties[i] == theAction.TargetCellId)
            {
                ChosenCellIsLegal = true;
                break;
            }
        }

        if (ChosenCellIsLegal && spawned < canCreate)
        {
            int pid = bm.AllocateRow();
            bm.PlacePieceRow(pid, currentPlayer, (byte)targetType, theAction.TargetCellId, PieceDefinition.maxHP[targetType]);
            int g = PieceDefinition.digitItGives[(byte)targetType];
            if (g >= 0) gameState.ps[currentPlayer].GrantDigit(g);
            spawned++;
        }

        for (int i = 0; i < eCount && spawned < canCreate; i++)
        {
            int cell = empties[i];
            if (cell == theAction.TargetCellId) continue; // already used chosen cell
            int pid = bm.AllocateRow();
            bm.PlacePieceRow(pid, currentPlayer, (byte)targetType, cell, PieceDefinition.maxHP[targetType]);
            int g = PieceDefinition.digitItGives[(byte)targetType];
            if (g >= 0) gameState.ps[currentPlayer].GrantDigit(g);
            spawned++;
        }

        // Mark once-per-turn flag
        if (PieceDefinition.spawn_isOnlyOncePerTurn[theAction.pieceType])
            bm.spawnerUsedThisTurn.Add(actorPid);

        RefreshConnectorState(gameIndex);
    }
}
