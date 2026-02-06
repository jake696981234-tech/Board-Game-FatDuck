using UnityEngine;

public static class FeedingGroundAction
{
    public static void FeedingGround(int gameIndex, int pieceIDKilled)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        int[] PiecesInRange = Scratch.GetScratchCellBuffer(gameIndex);

        int Player = bm.pieceOwner[pieceIDKilled];

        for (int pid = 0; pid < bm.pieceCount; pid++)
        {
            if (bm.pieceOwner[pid] == Player) continue;
            int pieceType = bm.pieceType[pid];
            if (!Piece.feedingGround_enabled[pieceType]) continue;

            for (int range = 0; range <= Piece.feedingGround_Range[pieceType]; range++)
            {
                int found = BmCac.pieceIdsRingAroundCell(bm.pieceCellId[pid], range, PiecesInRange, gameIndex);

                if (found <= 0) continue;

                for (int i = 0; i < found; i++)
                {
                    if (PiecesInRange[i] != pieceIDKilled) continue;
                    bm.addToEndRoundPayout[pid] += Piece.feedingGround_payOut[pieceType];
                }
            }
        }
    }
}
