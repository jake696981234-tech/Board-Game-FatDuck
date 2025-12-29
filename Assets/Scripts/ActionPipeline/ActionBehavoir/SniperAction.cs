using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class SniperAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

        //work out Direction and Length-move to its own method when ready

        



        int[] scratch = Scratch.GetScratchCellBuffer(offerBuild.gameIndex);
        int theNumberOfTargets = GetLegalTargets(pieceId, actorType, scratch, offerBuild.gameIndex);
        for (int i = 0; i < theNumberOfTargets; i++)
        {
            int tgtPid = scratch[i];
            ushort targetCellId = (ushort)bm.GetPieceCell(tgtPid);
            var theAction = new Action
            {
                kind = Shoot,
                pieceType = actorType,
                ActorsCellId = (ushort)cell,
                TargetCellId = targetCellId,
                aux = (ushort)tgtPid
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

        int rmin = Piece.sniper_minRange[actorType];
        int rmax = Piece.sniper_maxRange[actorType];
        if (rmax < rmin) { int t = rmax; rmax = rmin; rmin = t; }

        int cap = outTargets != null ? outTargets.Length : 0;
        int count = 0;

        int cellCount = bm.GetCellCount();
        for (int c = 0; c < cellCount; c++)
        {
            int pid = bm.GetCellOccupant(c);
            if (pid < 0) continue;
            if (bm.GetPieceOwner(pid) == actorOwner) continue;
            if (Piece.sniper_isonlySoldiers[actorType] && !Piece.isBuilding[bm.pieceType[pid]])

            int d = bm.Distance(originCell, c);
            if (d < rmin || d > rmax) continue;
            if (!BmAbilityCac.LineOfSightClear(originCell, c, gameIndex)) continue;

            if (count < cap) outTargets[count] = pid; // pieceId target
            count++;
        }
        return count;
    }
}