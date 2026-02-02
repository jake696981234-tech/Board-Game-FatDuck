using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using static Game.Core.GameActions;

public static class SacrificeFactoryAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        int[] scratch = Scratch.GetScratchCellBuffer(offerBuild.gameIndex);
        int theNumberOfTargets = GetLegalTargets(pieceId, actorType, scratch, offerBuild.gameIndex);
        for (int i = 0; i < theNumberOfTargets; i++)
        {
            int tgtPid = scratch[i];
            ushort targetCellId = (ushort)bm.GetPieceCell(tgtPid);
            var theAction = new Action
            {
                kind = SacrificeFactory,
                ActorsCell = cell,
                TargetCell = targetCellId,
            };
            OfferProvider.Emit(theAction, ref offerBuild);
        }
    }

    public static int GetLegalTargets(int actorPieceId, int actorType, int[] outTargets, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return 0;
        int actorOwner = bm.GetPieceOwner(actorPieceId);

        int rmin = Piece.sacrificeFactory_rangeMin[actorType];
        int rmax = Piece.sacrificeFactory_rangeMax[actorType];

        if (rmax < rmin) { int t = rmax; rmax = rmin; rmin = t; }

        int cap = outTargets != null ? outTargets.Length : 0;
        int count = 0;

        int cellCount = bm.GetCellCount();
        for (int c = 0; c < cellCount; c++)
        {
            int pid = bm.GetCellOccupant(c);
            if (pid < 0) continue;
            if (bm.GetPieceOwner(pid) != actorOwner) continue;

            int d = bm.Distance(originCell, c);
            if (d < rmin || d > rmax) continue;
            if (!BmCac.LineOfSightClear(originCell, c, gameIndex)) continue;

            if (count < cap) outTargets[count] = pid; // pieceId target
            count++;
        }
        return count;
    }

    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        bm.pieceFactoryAux[bm.GetCellOccupant(theAction.ActorsCell)] += Piece.sacrificeFactory_amount[bm.GetPieceTypeFromCell(theAction.ActorsCell)];
        pieceKilled(victimsCell: theAction.TargetCell, actorsCell: theAction.ActorsCell, gameIndex: gameIndex);
    }
  
}
