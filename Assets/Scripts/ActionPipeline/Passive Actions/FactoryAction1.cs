using UnityEngine;

public static class FactoryAction
{
    public static int GiveMePiecesFactoryPayOut(int pieceType, int gameIndex)
    {
        if (!Piece.factory_isRoundMultiplier[pieceType]) return Piece.factory_payout[pieceType];
        var gameState = GameRegistry.game[gameIndex].gameState;
        return Piece.factory_payout[pieceType] * gameState.currentRoundNumber;
    }
}
