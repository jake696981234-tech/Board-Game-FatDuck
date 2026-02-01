using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class LauncherAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    {
        int[] pairs = Scratch.GetScratchCellBuffer(offerBuild.gameIndex);
        int theNumberOfTargets = GetLegalTargets(pieceId, actorType, pairs, offerBuild.gameIndex);
        for (int i = 0; i < theNumberOfTargets; i += 2)
        {
            int victimscell = pairs[i];
            int targetcell = pairs[i + 1];
            var theAction = new Action
            {
                kind = Launcher,
                ActorsCell = cell,
                TargetCell = targetcell,
                IntakeCell = victimscell, 
            };
            OfferProvider.Emit(theAction, ref offerBuild);
        }
    }

     public static int GetLegalTargets(int actorPid, int actorType, int[] outPairs, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int inputRange = Piece.launcher_inputRange[actorType];
        int outputRange = Piece.launcher_outputRange[actorType];
        bool allowFriendly = Piece.launcher_isfriendlyFire[actorType];
        bool allowEnemy = Piece.launcher_isEnemyFire[actorType];

        int originCell = bm.GetPieceCell(actorPid);
        if (originCell < 0) return 0;

        int cap = outPairs != null ? outPairs.Length : 0;
        int write = 0;
        if (cap < 2) return 0; // need at least one pid,dst pair slot

        int cellCount = bm.GetCellCount();
        int actorOwner = bm.GetPieceOwner(actorPid);

        // Find candidate pieces
        for (int victimsCell = 0; victimsCell < cellCount; victimsCell++)
        {
            if (bm.GetCellOccupant(victimsCell) == bm._invalidId) continue;
            if (!Info.AbilitysCanSeperatePiecesWithWalls && Piece.connectors_enabled[bm.GetPieceTypeFromCell(victimsCell)]) continue;
            byte owner = (byte)bm.GetPieceOwnerFromCell(victimsCell);
            if (owner == actorOwner && !allowFriendly) continue;
            if (owner != actorOwner && !allowEnemy) continue;

            int distIn = bm.Distance(originCell, victimsCell);
            if (distIn < 1 || distIn > inputRange) continue;
            if (!BmCac.LineOfSightClear(originCell, victimsCell, gameIndex)) continue;

            // For each candidate destination within outputRange from launcher
            for (int targetCell = 0; targetCell < cellCount; targetCell++)
            {
                if (!bm.IsEmpty(targetCell)) continue;
                int distOut = bm.Distance(originCell, targetCell);
                if (distOut < 1 || distOut > outputRange) continue;
                if (!BmCac.LineOfSightClear(originCell, targetCell, gameIndex)) continue;

                if (write + 1 >= cap) return write; // buffer full; return what we wrote

                outPairs[write] = victimsCell;
                outPairs[write + 1] = targetCell;
                write += 2;
            }
        }

        return write; // count of ints (pairs pid,dst)
    }

    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        bm.MovePieceRow(bm.occupantPieceId[theAction.IntakeCell], theAction.TargetCell);
        GameActions.RefreshConnectorState(gameIndex);
    }
}
