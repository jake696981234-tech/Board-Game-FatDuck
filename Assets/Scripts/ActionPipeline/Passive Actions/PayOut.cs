using UnityEngine;
using System.Collections.Generic;
using System;
using Game.Core;

public static class PayOut
{
    public static void GiveMePayPlayersOut(int gameIndex, int player)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        var bm = GameRegistry.game[gameIndex].boardModel;

        int FinalPayment = 0;


        for (int i = 0; i < bm.pieceCount; i++)
        {
            if (bm.pieceOwner[i] != player) continue;
            int pieceType = bm.pieceType[i];
            if (Piece.factory_enabled[pieceType]) FinalPayment += GiveMePayFactoryActionPayOut(pieceType, gameIndex)
        }
    }

    public static int GiveMePayFactoryPayOut(int pieceType, int gameIndex)
    {
        var GroupFactory = new List<int>();
        if (Piece.factory_isGroup[pieceType]) GroupFactory.Add
    }

    public static int GiveMeGroupFactory(pieceType, gameIndex)
    {

    }

    public static void GiveMeDetialedPayOut()
    {

    }
}
