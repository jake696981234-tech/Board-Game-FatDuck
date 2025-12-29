using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class MoveAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    {
        int[] scratch = Scratch.GetScratchCellBuffer(offerBuild.gameIndex);
        int theNumberOfTargets = GetLegalTargets(pieceId, actorType, scratch, offerBuild.gameIndex);
        for (int i = 0; i < theNumberOfTargets; i++)
        {
            int targetCellId = scratch[i];
            var theAction = new Action
            {
                kind = Move,
                pieceType = actorType,
                ActorsCellId = (ushort)cell,
                TargetCellId = (ushort)targetCellId,
                aux = 0
            };
            OfferProvider.Emit(theAction, ref offerBuild);
        }
    }

    /// <summary>
    /// MOVE structural legality: empty-only reachability; melee-on-move targets among enemies adjacent to reachable cells.
    /// Uses BoardModel's zero-alloc helpers (EnumerateReachableEmpty, GetNeighbors, etc.).
    /// </summary>
    public static int GetLegalTargets(int actorPieceId, int actorType, int[] outTargets, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return 0;

        int rmin = Piece.move_rangeMin[actorType];
        int rmax = Piece.move_rangeMax[actorType];
        if (rmax < rmin) { int t = rmax; rmax = rmin; rmin = t; }

        int count = 0;
        int cap = outTargets != null ? outTargets.Length : 0;

        // Reachable empty cells up to rmax
        int[] tmpReachable = Scratch.GetScratchCellBuffer(gameIndex);
        int reachCount = bm.EnumerateReachableEmpty(originCell, rmax, tmpReachable);

        // Emit EMPTY destinations (distance-filtered)
        for (int i = 0; i < reachCount; i++)
        {
            int cell = tmpReachable[i];
            int d = bm.Distance(originCell, cell);
            if (d >= rmin && d <= rmax)
            {
                if (count < cap) outTargets[count] = cell;
                count++;
            }
        }

        // Emit MELEE targets (enemy cells adjacent to any reachable empty approach cell)
        int meleeStart = count;

        // Direct adjacent melee (one-step onto enemy) when [rmin,rmax] includes 1
        if (rmin <= 1 && 1 <= rmax)
        {
            int[] neigh0 = Scratch.GetScratchNeighborBuffer(gameIndex);
            int Neighbors = bm.GetNeighbors(originCell, neigh0);
            int actorOwner0 = bm.GetPieceOwner(actorPieceId);
            for (int NeighborNumber = 0; NeighborNumber < Neighbors; NeighborNumber++)
            {
                int tgt = neigh0[NeighborNumber];
                int victimId = bm.GetCellOccupant(tgt);
                if (victimId < 0) continue;
                if (bm.GetPieceOwner(victimId) == actorOwner0) continue;
                // de-dup within melee segment
                bool seen = false;
                for (int k = meleeStart; k < count && k < cap; k++) { if (outTargets[k] == tgt) { seen = true; break; } }
                if (seen) continue;
                if (count < cap) outTargets[count] = tgt;
                count++;
            }
        }
        int[] neigh = Scratch.GetScratchNeighborBuffer(gameIndex);
        int actorOwner = bm.GetPieceOwner(actorPieceId);
        for (int i = 0; i < reachCount; i++)
        {
            int approach = tmpReachable[i];
            int steps = bm.Distance(originCell, approach);
            int nCount = bm.GetNeighbors(approach, neigh);
            for (int n = 0; n < nCount; n++)
            {
                int targetCell = neigh[n];
                int pid = bm.GetCellOccupant(targetCell);
                if (pid < 0) continue;
                if (bm.GetPieceOwner(pid) == actorOwner) continue;
                int total = steps + 1;
                if (total < rmin || total > rmax) continue;

                // de-dup within melee segment
                bool seen = false;
                for (int k = meleeStart; k < count && k < cap; k++) { if (outTargets[k] == targetCell) { seen = true; break; } }
                if (seen) continue;

                if (count < cap) outTargets[count] = targetCell;
                count++;
            }
        }
        return count;
    }

    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int actorPid = bm.GetCellOccupant(theAction.ActorsCellId);
        if (actorPid < 0) return;
        int dstOcc = bm.GetCellOccupant(theAction.TargetCellId);
        if (dstOcc >= 0)
        {
            GameActions.ResolveMelee(actorPid, dstOcc, in theAction, gameIndex);
            bm.MovePieceRow(actorPid, theAction.ActorsCellId);
        }
        else
        {
            bm.MovePieceRow(actorPid, theAction.TargetCellId);
        }
        GameActions.RefreshConnectorState(gameIndex);
    }
}
