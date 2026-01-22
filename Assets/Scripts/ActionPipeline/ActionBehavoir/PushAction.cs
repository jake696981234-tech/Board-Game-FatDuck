using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class PushAction
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
                kind = Push,
                ActorsCell = cell,
                TargetCell = targetCellId,
            };
            OfferProvider.Emit(theAction, ref offerBuild);
        }
    }

    // public static bool IsLegal(int actorPid, int actorType, in Game.Core.Action a, int gameIndex)
    // {
    //     var bm = GameRegistry.game[gameIndex].boardModel;


    //     int actorOwner = bm.GetPieceOwner(actorPid);
    //     int targetPid = a.aux;
    //     if (targetPid < 0 || !bm.IsValidPieceId(targetPid)) return false;
    //     if (bm.GetPieceCell(targetPid) != a.TargetCellId) return false;

    //     bool allowFriendly = Piece.push_isFriendlyFire[actorType];
    //     if (!allowFriendly && bm.GetPieceOwner(targetPid) == actorOwner) return false;

    //     bool allowBuildings = Piece.push_IsTargetsBuildings[actorType];
    //     bool allowSoldiers = Piece.push_isTargetsSoldiers[actorType];
    //     byte tgtType = bm.GetPieceType(targetPid);
    //     bool targetIsBuilding = Piece.isBuilding[tgtType];
    //     if (targetIsBuilding && !allowBuildings) return false;
    //     if (!targetIsBuilding && !allowSoldiers) return false;

    //     int rangeMax = Piece.push_rangeMax[actorType];
    //     int originCell = bm.GetPieceCell(actorPid);
    //     int targetCell = bm.GetPieceCell(targetPid);
    //     int dist = bm.Distance(originCell, targetCell);
    //     if (dist < 1 || dist > rangeMax) return false;
    //     if (!BmCac.LineOfSightClear(originCell, targetCell, gameIndex)) return false;

    //     int pushDest = BmCac.ComputePushDestination(actorPid, actorType, targetPid, gameIndex);
    //     return pushDest >= 0 && bm.IsValidCellId(pushDest);
    // }

    public static int GetLegalTargets(int actorPieceId, int actorType, int[] outPieceIds, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        // Inline minimal legality similar to GameActions.GetLegalTargets_Push
        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return 0;


        int actorOwner = bm.GetPieceOwner(actorPieceId);

        bool allowBuildings = Piece.push_IsTargetsBuildings[actorType];
        bool allowSoldiers = Piece.push_isTargetsSoldiers[actorType];
        int rangeMax = Piece.push_rangeMax[actorType];
        bool allowFriendly = Piece.push_isFriendlyFire[actorType];

        int cap = outPieceIds != null ? outPieceIds.Length : 0;
        int count = 0;
        int cellCount = bm.GetCellCount();

        for (int c = 0; c < cellCount; c++)
        {
            int victimId = bm.GetCellOccupant(c);
            if (victimId < 0) continue;

            if (!allowFriendly && bm.GetPieceOwner(victimId) == actorOwner) continue;

            byte type = bm.GetPieceType(victimId);
            bool targetIsBuilding = Piece.isBuilding[type];
            if (targetIsBuilding && !allowBuildings) continue;
            if (!targetIsBuilding && !allowSoldiers) continue;

            int dist = bm.Distance(originCell, c);
            if (dist < 1 || dist > rangeMax) continue;
            if (!BmCac.LineOfSightClear(originCell, c, gameIndex)) continue;

            int pushDest = BmCac.ComputePushDestination(actorPieceId, actorType, victimId, gameIndex);
            if (pushDest < 0 || !bm.IsValidCellId(pushDest)) continue;

            if (count < cap) outPieceIds[count] = victimId;
            count++;
        }

        return count;
    }

    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var events = GameRegistry.game[gameIndex].eventManager;

        // int victimID = theAction.TargetCell;
        // if (victimID < 0) return;

        // int actorPid = bm.GetCellOccupant(theAction.ActorsCell);
        // if (actorPid < 0) return;

        // byte actorType = bm.GetPieceType(actorPid);

        int dmg = Piece.push_damage[bm.GetPieceTypeFromCell(theAction.ActorsCell)];
        bool killed = GameActions.ApplyDamageWithCapital(theAction.ActorsCell, bm.GetCellOccupant(theAction.TargetCell), dmg, gameIndex);
        if (killed)
        {
            // Revoke digit from the defender's owner if this type granted one
            GameActions.pieceKilled(bm.GetCellOccupant(theAction.TargetCell), gameIndex, theAction);
        }
        else
        {
            int pushedCellID = BmCac.ComputePushDestination(bm.GetCellOccupant(theAction.TargetCell), bm.GetPieceTypeFromCell(theAction.ActorsCell), bm.GetCellOccupant(theAction.TargetCell), gameIndex);
            bm.MovePieceRow(bm.GetCellOccupant(theAction.TargetCell), pushedCellID);
        }
        GameActions.RefreshConnectorState(gameIndex);
    }
}
