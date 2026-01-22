using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class CoreDamageAction
{
    public static void CreateActions(byte actorType, int cell, ref OfferBuild offerBuild)
    {
        var gameState = GameRegistry.game[offerBuild.gameIndex].gameState;
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

        if (!Piece.coreDamage_enabled[actorType]) return;
        if (!bm.IsEnemyCoreCell(cell, offerBuild.query.playerId)) return;

        byte enemy = bm.OwnerOfCoreCell(cell);
        if (gameState.GetCoreHealth(enemy) <= 0) return; 

        var theAction = new Action
        {
            kind = CoreDamage,
            ActorsCell = cell,
        };
        OfferProvider.Emit(theAction, ref offerBuild);
    }


    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        var bm = GameRegistry.game[gameIndex].boardModel;

        gameState.ps[player].OnCoreDamage();

        int actorPid = bm.GetCellOccupant(theAction.ActorsCell);
        if (actorPid < 0) { Debug.Log("Action Fail"); return; }

        byte enemy = bm.OwnerOfCoreCell(theAction.ActorsCell);
        if (enemy >= 4) { Debug.Log("Action Fail"); return; }

        int dmg = Piece.coreDamage_damage[bm.GetPieceTypeFromCell(theAction.ActorsCell)];

        int hp = gameState.GetCoreHealth(enemy);
        gameState.SetCoreHealth(enemy, hp - dmg);
        gameState.TryEliminatePlayer(enemy);
    }
}
