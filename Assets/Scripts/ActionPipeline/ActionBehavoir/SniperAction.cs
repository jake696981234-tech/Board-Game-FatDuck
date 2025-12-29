using Game.Core;
using UnityEngine;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System.Collections.Generic;

public static class SniperAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, int player, ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

        //work out Direction and Length-move to its own method when ready
        List<int> directions = new List<int>();
        var PieceIdsTofindDirections = Scratch.GetScratchNeighborBuffer(offerBuild.gameIndex);
        var numberOfCells =  BmCac.pieceIdsRingAroundCell(cell, 1, PieceIdsTofindDirections, offerBuild.gameIndex);
        if (numberOfCells <= 0) return;
        for (int i = 0; i < numberOfCells; i++)
        {
            if (Piece.sniper_enabled[PieceIdsTofindDirections[bm.pieceType[i]]])
            {
                var direction = BmCac.GetDirectionIndex(cell, bm.pieceCellId[i], offerBuild.gameIndex);
                var OccCellsinDirection = BmCac.OccupiedCellsInLine(cell, 64, 1, direction, false, false, false, player, offerBuild.gameIndex);
                if (ifSniperHasRequiredBuildings(actorType, OccCellsinDirection, offerBuild.gameIndex)) directions.Add(direction);
            } 
        }
        if (directions.Count <= 0) return;
        for (int i = 0; i < directions.Count; i++)
        {
            var victimsCells = BmCac.OccupiedCellsInLine(cell, Piece.sniper_maxRange[actorType], Piece.sniper_minRange[actorType], directions[i], Piece.sniper_isLineOfSight[actorType], Piece.sniper_isFriendlyFire[actorType], Piece.sniper_isonlySoldiers[actorType], player, offerBuild.gameIndex);
            if (victimsCells.Length <= 0) continue;

            var theAction = new Action
            {
                kind = sniper,
                pieceType = actorType,
                ActorsCellId = (ushort)cell,
                TargetCellId = (ushort)victimsCells[victimsCells.Length - 1],
                aux = (ushort)directions[i],
            };
            OfferProvider.Emit(theAction, ref offerBuild);
        }
    }


    public static bool ifSniperHasRequiredBuildings(int actorType, int[] occCells, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        for (int c = 0; c < Piece.sniper_lineLength[actorType]; c++)
        {
            if (!Piece.sniper_enabled[bm.GetPieceTypeFromCell(occCells[c])]) return false;
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
