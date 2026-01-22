using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class ConversionFactoryAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    {
        var gameState = GameRegistry.game[offerBuild.gameIndex].gameState;
        if (IsLegal(gameState.ps[offerBuild.query.playerId].vpTotal))
        {
            var theAction = new Action
            {
                kind = ConversionFactory,
                ActorsCell = cell,
            };
            OfferProvider.Emit(theAction, ref offerBuild);
        }
    }

    public static bool IsLegal(int PlayersVPAmount)
    {
        if (PlayersVPAmount <= 0) return false;
        return true;
    }

    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var gameState = GameRegistry.game[gameIndex].gameState;

        int Pieceid = bm.GetCellOccupant(theAction.ActorsCell);

        gameState.ps[player].vpTotal--;
        bm.pieceFactoryAux[Pieceid] += Piece.conversionFactory_amount[bm.GetPieceTypeFromCell(theAction.ActorsCell)];
    }
}
