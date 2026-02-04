using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public class WorkYardAction
{
    public static void CreateActions(
        int actorPid,
        int actorType,
        int actorCell,
        ref OfferBuild offerBuild)
    {
        var occCells = BmCac.CellIdsRingAndLessthanRing(originCell: actorCell, ringSize: Piece.workYard_range[actorType], requireEmpty: false, requireOcc: true, requireOwned: offerBuild.query.playerId, gameIndex: offerBuild.gameIndex);
        if (occCells.Count < 1) return;
        
            for (int i = 0; i < occCells.Count; i++)
            {
                if (Piece.workYard_excludeBuildings[actorType] && occCells[i]) continue;
                Action theAction = new Action
                {
                    kind = WorkYard,
                    ActorsCell = actorCell,
                    TargetCell = occCells[i],
                };
                OfferProvider.Emit(theAction, ref offerBuild);

            }
    }
}
