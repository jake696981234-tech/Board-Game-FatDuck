using Game.Core;
using UnityEngine;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System.Collections.Generic;
using System;

public static class SniperAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        
        Span<int> neighbouringPieceIds = Scratch.GetScratchNeighborBuffer(offerBuild.gameIndex);
        var numberOfCells =  BmCac.pieceIdsRingAroundCell(cell, 1, neighbouringPieceIds, offerBuild.gameIndex);
        if (numberOfCells == 0) return;

        var directions = LegalDirections(neighbouringPieceIds, numberOfCells,cell, actorType, offerBuild);

        int reversedDirection;
        var howManyDirections = directions.Count;
        for (int i = 0; i < howManyDirections; i++) { if (BmCac.TryReverseDirection(cell, directions[i], offerBuild.gameIndex, out reversedDirection)) directions.Add(reversedDirection);}

        if (directions.Count <= 0) return;
        for (int i = 0; i < directions.Count; i++)
        {
            var victimsCells = BmCac.OccupiedCellsInLine(cell, Piece.sniper_maxRange[actorType], Piece.sniper_minRange[actorType], directions[i], Piece.sniper_isLineOfSight[actorType], Piece.sniper_isFriendlyFire[actorType], Piece.sniper_isonlySoldiers[actorType], offerBuild.query.playerId, offerBuild.gameIndex);
            if (victimsCells.Length <= 0) continue;

            var theAction = new Action
            {
                kind = Sniper,
                pieceType = actorType,
                ActorsCellId = (ushort)cell,
                TargetCellId = (ushort)victimsCells[victimsCells.Length - 1],
                aux = (ushort)directions[i],
            };
            OfferProvider.Emit(theAction, ref offerBuild);
        }
    }

    public static List<int> LegalDirections(Span<int> neighbouringPieceIds, int numberOfIds, int cell, int actorType, OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        List<int> directions = new List<int>();
        for (int i = 0; i < numberOfIds; i++) // Find Each Legal Direction, a return directions
        {
            var checkingPieceType = bm.pieceType[neighbouringPieceIds[i]];
            var pieceCellId = bm.pieceCellId[neighbouringPieceIds[i]];
            if (!Piece.sniper_enabled[checkingPieceType]) continue;
            var direction = BmCac.GetDirectionIndex(cell, pieceCellId, offerBuild.gameIndex);
            var OccCellsinDirection = BmCac.OccupiedCellsInLine(cell, 64, 1, direction, false, false, false, offerBuild.query.playerId, offerBuild.gameIndex);
            if (OccCellsinDirection.Length < Piece.sniper_lineLength[actorType]) continue;
            if (ifSniperHasRequiredBuildings(actorType, OccCellsinDirection, offerBuild.gameIndex)) directions.Add(direction);
        }
        return directions;
    }


    public static bool ifSniperHasRequiredBuildings(int actorType, int[] occCells, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        for (int i = 0; i < Piece.sniper_lineLength[actorType]; i++)
        {
            if (!Piece.sniper_enabled[bm.GetPieceTypeFromCell(occCells[i])]) return false;
        }
        return true;
    }


    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        var victimsCells = BmCac.OccupiedCellsInLine(theAction.ActorsCellId, Piece.sniper_maxRange[theAction.pieceType], Piece.sniper_minRange[theAction.pieceType], theAction.aux, Piece.sniper_isLineOfSight[theAction.pieceType], Piece.sniper_isFriendlyFire[theAction.pieceType], Piece.sniper_isonlySoldiers[theAction.pieceType], player, gameIndex);
        if (victimsCells.Length <= 0) { Debug.Log("sniper Action Encoding is broken, this should not be possible"); return; }
        int dmg = Piece.sniper_damage[theAction.pieceType];

        for (int i = 0; i < victimsCells.Length; i++)
        {
            bool killed = GameActions.ApplyDamageWithCapital(theAction.ActorsCellId, bm.occupantPieceId[victimsCells[i]], dmg, gameIndex);
            if (killed) GameActions.pieceKilled(bm.occupantPieceId[victimsCells[i]], gameIndex, theAction);
            GameActions.RefreshConnectorState(gameIndex);
        }
    }
}
