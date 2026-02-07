using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System.Collections.Generic;
using static Game.Core.GameActions;

public static class UpgradeAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        // Upgrade-as-piece-action: find destination types that upgrade from this actorType
        for (int upgradedToPieceType = 0; upgradedToPieceType < Piece.typeCount; upgradedToPieceType++)
        {
            if (!Piece.upgrade_enabled[upgradedToPieceType]) continue;
            if (Piece.upgrade_target[upgradedToPieceType] != actorType) continue;
            if (!HasRequiredDigits(upgradedToPieceType, ref offerBuild)) continue;
            if (Piece.upgrade_isGoalKills[upgradedToPieceType])
            {
                if (Piece.upgrade_killsNeeded[upgradedToPieceType] <= bm.pieceKillCount[pieceId]) continue;
            }

            var theAction = new Action
            {
                kind = Upgrade,
                ActorsCell = cell,
                TargetCell = -1,
                TargetType = upgradedToPieceType,
            };
            GenerateCompleteCreateActions(theAction: in theAction, targetType: in upgradedToPieceType, offerBuild: ref offerBuild);
        }

    }

    // public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    // {
    //     var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
    //     // Upgrade-as-piece-action: find destination types that upgrade from this actorType
    //     for (int upgradedToPieceType = 0; upgradedToPieceType < Piece.typeCount; upgradedToPieceType++)
    //     {
    //         if (!Piece.upgrade_enabled[upgradedToPieceType]) continue;
    //         if (Piece.upgrade_target[upgradedToPieceType] != actorType) continue;
    //         if (!CreateAction.HasRequiredDigits(upgradedToPieceType, ref offerBuild)) continue;
    //         if (Piece.upgrade_isGoalKills[upgradedToPieceType])
    //         {
    //                 if (Piece.upgrade_killsNeeded[upgradedToPieceType] <= bm.pieceKillCount[pieceId]) continue;
    //         }

    //         var theAction = new Action
    //         {
    //             kind = Upgrade,
    //             ActorsCell = cell,
    //             TargetCell = -1,
    //             TargetType = upgradedToPieceType,
    //         };

    //         List<Action> CreateActions = new List<Action> {theAction};
    //         // if (Piece.sacrificeCost_enabled[upgradedToPieceType] && !CreateAction.GenerateSacrificeCosts(CreateActions, ref offerBuild)) return;
    //         for (int i = 0; i < CreateActions.Count; i++) { OfferProvider.Emit(CreateActions[i], ref offerBuild); }
    //     }

    // }

    //isItLegal


    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        bm.FreeRowSwapBack(bm.GetCellOccupant(theAction.ActorsCell));
        placePiece(wallConfig: theAction.WallConfig, createdPieceType: theAction.TargetType, targetCell: theAction.ActorsCell, player: player, gameIndex: gameIndex);

    }
}
