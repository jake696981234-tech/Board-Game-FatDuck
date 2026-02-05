using UnityEngine;
using System.Collections.Generic;
using System;
using Game.Core;

public static class FactoryAction
{
     #region Factory Ability
    public static float[] ComputePlayersFactoryIncome(int gameIndex)
    {
        float[] perPlayerFactoryIncome = new float[4];

        for (int i = 0; i < 4; i++)
        {
            perPlayerFactoryIncome[i] = ComputeFactoryIncome(i, gameIndex);
        }
        return perPlayerFactoryIncome;
    }

    public static float ComputeFactoryIncome(int player, int gameIndex)
    {
        float total = 0f;
        PerPiecePayout income = ComputeDetailedPlayerFactoryIncome(player, gameIndex);

        for (int i = 0; i < income.payout.Length; i++) { total += income.payout[i]; }
        return total;
    }

    // public static PerPiecePayout ComputeDetailedPlayerFactoryIncome(int playerId, int gameIndex)
    // {
    //     var gameState = GameRegistry.game[gameIndex].gameState;
    //     var bm = GameRegistry.game[gameIndex].boardModel;

    //     // Build per-piece payouts for a single player (type, whether grouped, payout per type)
    //     var pieceTypes = new List<int>();
    //     var isGroups = new List<bool>();
    //     var payouts = new List<float>();

    //     // Ensure round number is at least 1
    //     int roundNum = Math.Max(1, gameState.currentRoundNumber);
    //     var counts = BmCac.SnapshotOwnerTypeCounts(bm); // map of (owner,type) -> count

    //     foreach (var kv in counts)
    //     {
    //         if (kv.Key.owner != playerId)
    //             continue;

    //         int owner = kv.Key.owner;
    //         if (owner < 0 || owner >= gameState.ps.Length) continue;

    //         int type = kv.Key.type;
    //         int count = kv.Value;

    //         if (!Piece.factory_enabled[type] && !Piece.feedingGround_enabled[type] && !Piece.sacrificeFactory_enabled[type] && !Piece.eat_enabled[type] && !Piece.conversionFactory_enabled[type]) continue;

    //         float AuxPayout = AuxFactoryPayout(owner, type, gameIndex);

    //         int baseAmt = Piece.factory_amount[type];
    //         if (baseAmt == 0 && AuxPayout == 0) continue;

    //         // Flags for scaling
    //         bool roundMul = Piece.factory_isRoundMultiplier[type];

    //         bool group = Piece.factory_isGroup[type];

    //         int groupAmt = Piece.factory_groupAmount[type];

    //         int pay = baseAmt;
    //         if (roundMul) pay *= roundNum;

    //         float payout;
    //         if (group)
    //         {
    //             int groups = count / Math.Max(1, groupAmt); // pay per full group
    //             payout = pay * groups + AuxPayout;
    //         }
    //         else
    //         {
    //             payout = pay * count + AuxPayout; // pay per individual piece
    //         }

    //         if (payout == 0)
    //             continue;

    //         pieceTypes.Add(type);
    //         isGroups.Add(group);
    //         payouts.Add(payout);
    //     }

    //     // Convert lists to arrays for the struct
    //     return new PerPiecePayout(
    //         pieceTypes.ToArray(),
    //         isGroups.ToArray(),
    //         payouts.ToArray()
    //     );
    // }



    // private static float AuxFactoryPayout(int playerId, int thisType, int gameIndex)
    // {
    //     var bm = GameRegistry.game[gameIndex].boardModel;

    //     float payOut = 0;

    //     for (int pieceId = 0; pieceId < bm.pieceCount; pieceId++)
    //     {
    //         byte type = bm.GetPieceType(pieceId);
    //         if (thisType != type) continue;
    //         if (bm.pieceOwner[pieceId] != playerId) continue;

    //         payOut += bm.pieceFactoryAux[pieceId];
    //     }
    //     return payOut;
    // }

    public static PerPiecePayout ComputeDetailedPlayerFactoryIncome(int playerId, int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        var bm = GameRegistry.game[gameIndex].boardModel;

        // Build per-piece payouts for a single player (type, whether grouped, payout per type)
        var pieceTypes = new List<int>();
        var isGroups = new List<bool>();
        var payouts = new List<float>();

        // Ensure round number is at least 1
        int roundNum = Math.Max(1, gameState.currentRoundNumber);
        var counts = BmCac.SnapshotOwnerTypeCounts(bm); // map of (owner,type) -> count

        foreach (var kv in counts)
        {
            if (kv.Key.owner != playerId)
                continue;

            int owner = kv.Key.owner;
            if (owner < 0 || owner >= gameState.ps.Length) continue;

            int type = kv.Key.type;
            int count = kv.Value;

            if (!Piece.factory_enabled[type] && !Piece.feedingGround_enabled[type] && !Piece.sacrificeFactory_enabled[type] && !Piece.eat_enabled[type] && !Piece.conversionFactory_enabled[type]) continue;

            float AuxPayout = AuxFactoryPayout(owner, type, gameIndex);

            int baseAmt = Piece.factory_amount[type];
            if (baseAmt == 0 && AuxPayout == 0) continue;

            // Flags for scaling
            bool roundMul = Piece.factory_isRoundMultiplier[type];

            bool group = Piece.factory_isGroup[type];

            int groupAmt = Piece.factory_groupAmount[type];

            int pay = baseAmt;
            if (roundMul) pay *= roundNum;

            float payout;
            if (group)
            {
                int groups = count / Math.Max(1, groupAmt); // pay per full group
                payout = pay * groups + AuxPayout;
            }
            else
            {
                payout = pay * count + AuxPayout; // pay per individual piece
            }

            if (payout == 0)
                continue;

            pieceTypes.Add(type);
            isGroups.Add(group);
            payouts.Add(payout);
        }

        // Convert lists to arrays for the struct
        return new PerPiecePayout(
            pieceTypes.ToArray(),
            isGroups.ToArray(),
            payouts.ToArray()
        );
    }

    #endregion
}
