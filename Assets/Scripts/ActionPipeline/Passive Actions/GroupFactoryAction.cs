using UnityEngine;
using System.Collections.Generic;

public static class GroupFactoryAction
{
    public static int GiveMeTotalGroupFactoryForPlayer(int player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        int payout = 0;
        HashSet<int> alreadySearched = new HashSet<int>();
        for (int i = 0; i < bm.pieceCount; i++)
        {
            if (Piece.groupFactory_enabled[i]) continue;
            if (!alreadySearched.Add(bm.pieceType[i])) continue;
            if (bm.pieceOwner[i] != player) continue;
            payout += GiveMeGroupFactoryForType(type: bm.pieceType[i], player: player, gameIndex: gameIndex);
        }
        return payout;
    }

     public static int GiveMeGroupFactoryForType(int type, int player, int gameIndex)
    {
        int HowManyClusters = BmCac.CountClustersOfType(type: type, required: Piece.groupFactory_require[type], player: player, gameIndex: gameIndex);
        if (!Piece.groupFactory_isRoundMultiplier[type]) return Piece.groupFactory_payout[type] * HowManyClusters;
        var gameState = GameRegistry.game[gameIndex].gameState;
        return Piece.groupFactory_payout[type] * HowManyClusters * gameState.currentRoundNumber;
    }
    
    // Just a fcuntional progamming test
    // public static int GiveMeTotalGroupFactoryForPlayer(int player, int gameIndex)
    // {
    //     var bm = GameRegistry.game[gameIndex].boardModel;
    //     int payout = 0;
    //     HashSet<int> alreadySearched = new HashSet<int>();
    //     loopGroupBuild(0, ref payout, player, ref alreadySearched, gameIndex);
    //     return payout;
    // }

    // public static void loopGroupBuild(int index, ref int payout, int player, ref HashSet<int> alreadySearched, int gameIndex)
    // {
    //     var bm = GameRegistry.game[gameIndex].boardModel;
        
    //     if (bm.pieceCount < index) return;
    //     if (Piece.groupFactory_enabled[index]) 
    //     {
    //         loopGroupBuild(index + 1, ref payout, player, ref alreadySearched, gameIndex);
    //         return;
    //     }
    //     if (!alreadySearched.Add(bm.pieceType[index])) 
    //     {
    //         loopGroupBuild(index + 1, ref payout, player, ref alreadySearched, gameIndex);
    //         return;       
    //     }
    //     if (bm.pieceOwner[index] != player)
    //     {
    //         loopGroupBuild(index + 1, ref payout, player, ref alreadySearched, gameIndex);
    //         return;
    //     }
    //     payout += GiveMeGroupFactoryForType(type: bm.pieceType[index], player: player, gameIndex: gameIndex);
    //     loopGroupBuild(index + 1, ref payout, player, ref alreadySearched, gameIndex);
    // }


   


}
