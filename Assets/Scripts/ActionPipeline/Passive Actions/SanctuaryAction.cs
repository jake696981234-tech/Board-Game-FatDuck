using System.Collections.Generic;
using System;
using Game.Core;


public static class SanctuaryAction
{
    public static int ProtectedBySanctuary(Span<int> outPieceIds, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var gameState = GameRegistry.game[gameIndex].gameState;

        gameState.dublicateFilter.Clear();
        Span<int> protectedpieces = stackalloc int[240];
        int foundPieces = 0;

        for (int pid = 0; pid < bm.pieceCount; pid++)
        {
            byte type = bm.GetPieceType(pid);
            int sanctuaryRange = -1;

            // Find a Sanctuary ability on this type and grab its range

            if (!Piece.sanctuary_enabled[type]) continue;
            sanctuaryRange = Piece.sanctuary_range[type];

            if (sanctuaryRange < 0) continue;
            int centerCell = bm.pieceCellId[pid];
            if (centerCell < 0) continue;

            for (int range = 0; range <= sanctuaryRange; range++)
            {
                int found = BmCac.pieceIdsRingAroundCell(centerCell, range, protectedpieces, gameIndex);

                if (found <= 0) continue;

                for (int pp = 0; pp < found; pp++)
                {
                    if (gameState.dublicateFilter.Add(protectedpieces[pp]))
                    {
                        outPieceIds[foundPieces] = protectedpieces[pp];
                        foundPieces++;
                    }
                }
            }
        }
        return foundPieces;
    }

    private static bool IsPieceProtectedBySanctuary(int pieceId, int gameIndex)
    {
        Span<int> protectedpieces = stackalloc int[240];
        int numberOfProtectedPieces = ProtectedBySanctuary(protectedpieces, gameIndex);

        for (int i = 0; i < numberOfProtectedPieces; i++)
        {
            if (pieceId != protectedpieces[i]) continue;
            return true;
        }
        return false;
    }

    public static bool IsPieceApartOfSpan(int PieceId, Span<int> inPieceIds, int spanLength)
    {
        for (int i = 0; i < spanLength; i++)
        {
            if (PieceId != inPieceIds[i]) continue;
            return true;
        }
        return false;
    }
}
