using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class LauncherAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, OfferBuild offerBuild)
    {
        int[] scratch = Scratch.GetScratchCellBuffer(offerBuild.gameIndex);
        int theNumberOfTargets = GetLegalTargets(pieceId, actorType, scratch, offerBuild.gameIndex);
        for (int i = 0; i < theNumberOfTargets; i += 2)
        {
            int tgtPid = scratch[i];
            int dst = scratch[i + 1];
            var theAction = new Action
            {
                kind = Launcher,
                pieceType = actorType,
                ActorsCellId = (ushort)cell,
                TargetCellId = (ushort)dst,
                aux = (ushort)tgtPid
            };
            OfferProvider.Emit(theAction, offerBuild);
        }
    }

    public static bool IsLegal(int actorPid, int actorType, in Game.Core.Action a, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;


        int inputRange = PieceDefinition.launcher_inputRange[actorType];
        int outputRange = PieceDefinition.launcher_outputRange[actorType];
        bool allowFriendly = PieceDefinition.launcher_isfriendlyFire[actorType];
        bool allowEnemy = PieceDefinition.launcher_isEnemyFire[actorType];

        int targetPid = a.aux;
        if (targetPid < 0 || !bm.IsValidPieceId(targetPid)) return false;
        int originCell = bm.GetPieceCell(actorPid);
        int targetCell = bm.GetPieceCell(targetPid);
        if (originCell < 0 || targetCell < 0) return false;

        int actorOwner = bm.GetPieceOwner(actorPid);
        int tgtOwner = bm.GetPieceOwner(targetPid);
        if (tgtOwner == actorOwner && !allowFriendly) return false;
        if (tgtOwner != actorOwner && !allowEnemy) return false;

        int distIn = bm.Distance(originCell, targetCell);
        if (distIn < 1 || distIn > inputRange) return false;
        if (!BmAbilityCac.LineOfSightClear(originCell, targetCell, gameIndex)) return false;

        int dst = a.TargetCellId;
        if (bm.GetCellOccupant(dst) >= 0) return false;
        int distOut = bm.Distance(originCell, dst);
        if (distOut < 1 || distOut > outputRange) return false;
        if (!BmAbilityCac.LineOfSightClear(originCell, dst, gameIndex)) return false;

        return true;
    }

     public static int GetLegalTargets(int actorPid, int actorType, int[] outPairs, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;


        int inputRange = PieceDefinition.launcher_inputRange[actorType];
        int outputRange = PieceDefinition.launcher_outputRange[actorType];
        bool allowFriendly = PieceDefinition.launcher_isfriendlyFire[actorType];
        bool allowEnemy = PieceDefinition.launcher_isEnemyFire[actorType];

        int originCell = bm.GetPieceCell(actorPid);
        if (originCell < 0) return 0;

        int cap = outPairs != null ? outPairs.Length : 0;
        int write = 0;

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
            if (!BmAbilityCac.LineOfSightClear(originCell, c, gameIndex)) continue;

            // For each candidate destination within outputRange from launcher
            for (int dst = 0; dst < cellCount; dst++)
            {
                if (!bm.IsEmpty(dst)) continue;
                int distOut = bm.Distance(originCell, dst);
                if (distOut < 1 || distOut > outputRange) continue;
                if (!BmAbilityCac.LineOfSightClear(originCell, dst, gameIndex)) continue;

                if (write + 1 < cap)
                {
                    outPairs[write] = pid;
                    outPairs[write + 1] = dst;
                }
                write += 2;
            }
        }

        return write; // count of ints (pairs pid,dst)
    }

    public static void Apply(in Action a, byte p, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int targetPid = a.aux;
        if (targetPid < 0) return;
        bm.MovePieceRow(targetPid, a.TargetCellId);
        GameActions.RefreshConnectorState(gameIndex);
    }
}
