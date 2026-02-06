using UnityEngine;

public static class InstantFactoryAction
{
    public static int GiveMePiecesInstantFactoryPenality(int pieceId, int pieceType, int gameIndex)
    {
        if (!Piece.Instantfactory_isKillPenalty[pieceType]) return 0;
        var bm = GameRegistry.game[gameIndex].boardModel;
        if (Piece.Instantfactory_killsNeeded[pieceType] <= bm.Instantfactory_killGoal[pieceId]) return 0;
        return Piece.Instantfactory_killsPunishment[pieceType];
    }

    public static void PlaceInstantFactory(int createdPieceType, int createdPieceId, int player, int gameIndex)
    {
        if (!Piece.Instantfactory_enabled[createdPieceType]) return;
        var gameState = GameRegistry.game[gameIndex].gameState;
        gameState.ps[player].budget += Piece.Instantfactory_payout[createdPieceId];
        if (!Piece.Instantfactory_isKillPenalty[createdPieceType]) return;
        var bm = GameRegistry.game[gameIndex].boardModel;
        bm.Instantfactory_killGoal[createdPieceId] = 0;
    }

    public static void IncrementInstantFactoryKills(int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        for (int i = 0; i < bm.pieceCount; i++) bm.Instantfactory_killGoal[i]++;
    }
}
