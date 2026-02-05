using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using static Game.Core.GameActions;
using System;

public class HopAction
{
   public static void CreateActions(
        int actorPid,
        int actorType,
        int actorCell,
        ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

        Span<int> OccupiedCells = Scratch.GetScratchNeighborBuffer(offerBuild.gameIndex);
        var HowMany = bm.GetNeighborOccupiedCells(actorCell, OccupiedCells);
        if (HowMany < 1) return;
        for (int i = 0; i < HowMany; i++)
        {
            int dstCell = BmCac.FindDirectionAndStep(fromCell: actorCell, toCell: OccupiedCells[i], steps: 2, gameIndex: offerBuild.gameIndex);
            if (bm.IsCellOccupied(dstCell)) continue;
            Action theAction = new Action
            {
                kind = Hop,
                ActorsCell = actorCell,
                TargetCell = OccupiedCells[i],
            };
            OfferProvider.Emit(theAction, ref offerBuild);
        }
    }


    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        if (!bm.isPieceIDOwnedFromCell(player, theAction.TargetCell)) ApplyTypicalDamageAndPieceKill(actorsCell: theAction.ActorsCell, victimsCell: theAction.TargetCell, dmg: Piece.hop_damage[bm.GetPieceTypeFromCell(theAction.ActorsCell)], gameIndex: gameIndex);
        bm.MovePieceRow(bm.occupantPieceId[theAction.ActorsCell], BmCac.FindDirectionAndStep(fromCell: theAction.ActorsCell, toCell: theAction.TargetCell, steps: 2, gameIndex: gameIndex));
        RefreshConnectorState(gameIndex);
    }
}
