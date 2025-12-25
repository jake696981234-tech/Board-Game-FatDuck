using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class ConversionFactoryAction
{
    public static void CreateActions(in int[] scratch, int pieceId, byte actorType, int cell, OfferBuild offerBuild)
    {
        var gameState = GameRegistry.game[offerBuild.gameIndex].gameState;
        if (IsItLegal.IsLegal_ConversionFactory(gameState.ps[offerBuild.player].vpTotal))
        {
            var theAction = new Action
            {
                kind = ConversionFactory,
                pieceType = actorType,
                ActorsCellId = (ushort)cell,
                TargetCellId = (ushort)cell,
                aux = 0
            };
            newOfferProvider.Emit(ref theAction, offerBuild);
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

        int Pieceid = bm.GetCellOccupant(theAction.ActorsCellId);

        gameState.ps[player].vpTotal--;
        bm.pieceFactoryAux[Pieceid] += PieceDefinition.conversionFactory_amount[theAction.pieceType];
    }
}
