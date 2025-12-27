using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System.Collections.Generic;

public static class UpgradeAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    {
        // Upgrade-as-piece-action: find destination types that upgrade from this actorType
        for (int upgradedToPieceType = 0; upgradedToPieceType < PieceDefinition.typeCount; upgradedToPieceType++)
        {
            if (!PieceDefinition.upgrade_enabled[upgradedToPieceType]) continue;
            if (PieceDefinition.upgrade_target[upgradedToPieceType] != actorType) continue;
            if (!CreateAction.HasRequiredDigits(upgradedToPieceType, ref offerBuild)) continue;
           

            var theAction = new Action
            {
                kind = Upgrade,
                pieceType = (byte)upgradedToPieceType, // destination type
                ActorsCellId = (ushort)cell, // to do need to change the rest of the method - I switched this around. PieceType = used to be upgradedToPieceType- and TargetCellId used to be cell.
                TargetCellId = actorType,
                aux = 0
            };

            List<Action> CreateActions = new List<Action> {theAction};
            if (PieceDefinition.sacrificeCost_enabled[upgradedToPieceType] && !CreateAction.GenerateSacrificeCosts(CreateActions, ref offerBuild)) return;
            for (int i = 0; i < CreateActions.Count; i++) { OfferProvider.Emit(CreateActions[i], ref offerBuild); }
        }
    }

    //isItLegal


    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        var bm = GameRegistry.game[gameIndex].boardModel;

        CreateAction.PaySacCost(theAction, player, gameIndex);

        var UpgradedFromPieceId = bm.GetCellOccupant(theAction.ActorsCellId);
        var sourceConnector = bm.pieceConnectorConfig[UpgradedFromPieceId];
        bm.FreeRowSwapBack(UpgradedFromPieceId);

        int PieceId = bm.AllocateRow();
        bm.PlacePieceRow(PieceId, player, (byte)theAction.pieceType, theAction.ActorsCellId, PieceDefinition.maxHP[theAction.pieceType]);
        if (PieceDefinition.connectors_enabled[theAction.pieceType]) { bm.pieceConnectorConfig[PieceId] = sourceConnector; } else { bm.pieceConnectorConfig[PieceId] = (byte)theAction.aux; }
        int g = PieceDefinition.digitItGives[(byte)theAction.pieceType];
        if (g >= 0) gameState.ps[player].GrantDigit(g);

        GameActions.RefreshConnectorState(gameIndex);
    }
}
