using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using static Game.Core.GameActions;


public class WorkYardAction
{
    public static void CreateActions(
        int actorPid,
        int actorType,
        int actorCell,
        ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

        var occCells = BmCac.CellIdsRingAndLessthanRing(originCell: actorCell, ringSize: Piece.workYard_range[actorType], requireEmpty: false, requireOcc: true, requireOwned: offerBuild.query.playerId, gameIndex: offerBuild.gameIndex);
        if (occCells.Count < 1) return;

        for (int i = 0; i < occCells.Count; i++)
        {
            if (Piece.workYard_excludeBuildings[actorType] && Piece.isBuilding[bm.pieceType[occCells[i]]]) continue;
            Action theAction = new Action
            {
                kind = WorkYard,
                ActorsCell = actorCell,
                TargetCell = occCells[i],
            };
            OfferProvider.Emit(theAction, ref offerBuild);
        }
    }

    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        pieceKilledWithNoTriggers(victimsCell: theAction.TargetCell, gameIndex: gameIndex);
        bm.WorkYardBudget[bm.GetPieceTypeFromCell(theAction.ActorsCell)]++;
    }
}
