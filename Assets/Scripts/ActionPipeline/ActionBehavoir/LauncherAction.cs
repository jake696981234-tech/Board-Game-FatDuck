using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class LauncherAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    {
        int[] scratch = Scratch.GetScratchCellBuffer(offerBuild.gameIndex);
        int theNumberOfTargets = GetLegalTargets(pieceId, actorType, scratch, offerBuild.gameIndex);
        for (int i = 0; i < theNumberOfTargets; i += 2)
        {
            int tgtPid = scratch[i];
            int dst = scratch[i + 1];
            var theAction = new Action
            {
                kind = Move,
                ActorsCell = cell,
                TargetCell = dst,
                intakeCell = tgtPid, //to do, probs needs fix this, this encoding seems weird
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
        for (int c = 0; c < cellCount; c++)
        {
            int pid = bm.GetCellOccupant(c);
            if (pid < 0) continue;
            byte owner = (byte)bm.GetPieceOwner(pid);
            if (owner == actorOwner && !allowFriendly) continue;
            if (owner != actorOwner && !allowEnemy) continue;

            int distIn = bm.Distance(originCell, c);
            if (distIn < 1 || distIn > inputRange) continue;
            if (!BmCac.LineOfSightClear(originCell, c, gameIndex)) continue;

            // For each candidate destination within outputRange from launcher
            for (int dst = 0; dst < cellCount; dst++)
            {
                if (!bm.IsEmpty(dst)) continue;
                int distOut = bm.Distance(originCell, dst);
                if (distOut < 1 || distOut > outputRange) continue;
                if (!BmCac.LineOfSightClear(originCell, dst, gameIndex)) continue;

                if (write + 1 >= cap) return write; // buffer full; return what we wrote

                outPairs[write] = pid;
                outPairs[write + 1] = dst;
                write += 2;
            }
        }

        return write; // count of ints (pairs pid,dst)
    }

    public static void Apply(in Action a, byte p, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int targetPid = a.intakeCell;
        if (targetPid < 0) return;
        bm.MovePieceRow(targetPid, a.TargetCell);
        GameActions.RefreshConnectorState(gameIndex);
    }
}
