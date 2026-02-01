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
            if (!Info.AbilitysCanSeperatePiecesWithWalls && Piece.connectors_enabled[type]) continue;
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
            int pushedCellID = BmCac.ComputePushDestination(actorPieceId: bm.GetCellOccupant(theAction.ActorsCell), actorType: bm.GetPieceTypeFromCell(theAction.ActorsCell), targetPieceId: bm.GetCellOccupant(theAction.TargetCell), gameIndex: gameIndex);
            bm.MovePieceRow(bm.GetCellOccupant(theAction.TargetCell), pushedCellID);
        }
        GameActions.RefreshConnectorState(gameIndex);
    }
}
