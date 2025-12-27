using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class PushAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, OfferBuild offerBuild)
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
                pieceType = actorType,
                ActorsCellId = (ushort)cell,
                TargetCellId = targetCellId,
                aux = (ushort)tgtPid
            };
            newOfferProvider.Emit(theAction, offerBuild);
        }
    }

    public static bool IsLegal(int actorPid, int actorType, in Game.Core.Action a, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;


        int actorOwner = bm.GetPieceOwner(actorPid);
        int targetPid = a.aux;
        if (targetPid < 0 || !bm.IsValidPieceId(targetPid)) return false;
        if (bm.GetPieceCell(targetPid) != a.TargetCellId) return false;

        bool allowFriendly = PieceDefinition.push_isFriendlyFire[actorType];
        if (!allowFriendly && bm.GetPieceOwner(targetPid) == actorOwner) return false;

        bool allowBuildings = PieceDefinition.push_IsTargetsBuildings[actorType];
        bool allowSoldiers = PieceDefinition.push_isTargetsSoldiers[actorType];
        byte tgtType = bm.GetPieceType(targetPid);
        bool isBuilding = PieceDefinition.isBuilding[actorType];
        if (isBuilding && !allowBuildings) return false;
        if (!isBuilding && !allowSoldiers) return false;

        int rangeMax = PieceDefinition.push_rangeMax[actorType];
        int originCell = bm.GetPieceCell(actorPid);
        int targetCell = bm.GetPieceCell(targetPid);
        int dist = bm.Distance(originCell, targetCell);
        if (dist < 1 || dist > rangeMax) return false;
        if (!BmAbilityCac.LineOfSightClear(originCell, targetCell, gameIndex)) return false;

        int pushDest = BmAbilityCac.ComputePushDestination(actorPid, actorType, targetPid, gameIndex);
        return pushDest >= 0 && bm.IsValidCellId(pushDest);
    }

    public static int GetLegalTargets(int actorPieceId, int actorType, int[] outPieceIds, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        // Inline minimal legality similar to GameActions.GetLegalTargets_Push
        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return 0;


        int actorOwner = bm.GetPieceOwner(actorPieceId);

        bool allowBuildings = PieceDefinition.push_IsTargetsBuildings[actorType];
        bool allowSoldiers = PieceDefinition.push_isTargetsSoldiers[actorType];
        int rangeMax = PieceDefinition.push_rangeMax[actorType];
        bool allowFriendly = PieceDefinition.push_isFriendlyFire[actorType];

        int cap = outPieceIds != null ? outPieceIds.Length : 0;
        int count = 0;
        int cellCount = bm.GetCellCount();

        for (int c = 0; c < cellCount; c++)
        {
            int victimId = bm.GetCellOccupant(c);
            if (victimId < 0) continue;

            if (!allowFriendly && bm.GetPieceOwner(victimId) == actorOwner) continue;

            byte type = bm.GetPieceType(victimId);
            bool isBuilding = PieceDefinition.isBuilding[actorType];
            if (isBuilding && !allowBuildings) continue;
            if (!isBuilding && !allowSoldiers) continue;

            int dist = bm.Distance(originCell, c);
            if (dist < 1 || dist > rangeMax) continue;
            if (!BmAbilityCac.LineOfSightClear(originCell, c, gameIndex)) continue;

            int pushDest = BmAbilityCac.ComputePushDestination(actorPieceId, actorType, victimId, gameIndex);
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

        int victimID = theAction.aux;
        if (victimID < 0) return;

        int actorPid = bm.GetCellOccupant(theAction.ActorsCellId);
        if (actorPid < 0) return;

        byte actorType = bm.GetPieceType(actorPid);

        int dmg = PieceDefinition.push_damage[theAction.pieceType];
        bool killed = GameActions.ApplyDamageWithCapital(theAction.ActorsCellId, victimID, dmg, gameIndex);
        if (killed)
        {
            // Revoke digit from the defender's owner if this type granted one
            GameActions.pieceKilled(victimID, gameIndex, theAction);
        }
        else
        {
            int pushedCellID = BmAbilityCac.ComputePushDestination(actorPid, actorType, victimID, gameIndex);
            bm.MovePieceRow(victimID, pushedCellID);
        }
        GameActions.RefreshConnectorState(gameIndex);
    }
}
