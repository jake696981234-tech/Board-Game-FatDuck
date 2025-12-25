using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class CoreDamageAction
{
    public static void CreateActions(in int[] scratch, int pieceId, byte actorType, int cell, OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

        // Determine which adjacent cell is the enemy core and set dstCell accordingly
        ushort dstCore = 0;
        int owner = (byte)bm.GetPieceOwner(pieceId);
        int nNbrs = bm.GetNeighbors(cell, scratch);
        for (int i = 0; i < nNbrs; i++)
        {
            int nb = scratch[i];
            if (nb < 0) continue;
            if (bm.IsEnemyCoreCell(nb, owner)) { dstCore = (ushort)nb; break; }
        }

        var theAction = new Action
        {
            kind = CoreDamage,
            pieceType = actorType,
            ActorsCellId = (ushort)cell,
            TargetCellId = dstCore,
            aux = 0
        };
        newOfferProvider.Emit(ref theAction, offerBuild);
    }

    /// <summary>
    /// CORE DAMAGE (targetless): legal if actor stands on an ENEMY core cell.
    /// </summary>
    public static bool IsLegal(int actorPieceId, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return false;
        int owner = bm.GetPieceOwner(actorPieceId);
        return bm.IsEnemyCoreCell(originCell, owner);
    }

    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        var bm = GameRegistry.game[gameIndex].boardModel;

        gameState.ps[player].OnCoreDamage();

        int actorPid = bm.GetCellOccupant(theAction.ActorsCellId);
        if (actorPid < 0) return;

        int actorCell = bm.GetPieceCell(actorPid);
        byte enemy = bm.OwnerOfCoreCell(actorCell);
        if (enemy >= 4) return;

        byte typ = bm.GetPieceType(actorPid);
        int dmg = PieceDefinition.coreDamage_damage[typ];

        int hp = gameState.GetCoreHealth(enemy);
        gameState.SetCoreHealth(enemy, hp - dmg);
        gameState.TryEliminatePlayer(enemy);
    }
}
