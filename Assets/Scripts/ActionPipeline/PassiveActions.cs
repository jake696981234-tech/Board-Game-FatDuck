using System.Collections.Generic;
using System;
using Game.Core;

public static class PassiveActions
{
    private const int MaxSacrificeCombos = 64;

    /// <summary>
    /// Populate sacrifice options for a create action.
    /// Returns false if no legal options exist.
    /// </summary>
    public static bool GenerateSacrificeCosts(in Game.Core.Action theAction, int playerId, int gameIndex, List<int[]> outOptions)
    {
        outOptions.Clear();

        var bm = GameRegistry.game[gameIndex].boardModel;

        int need = PieceDefinition.sacrificeCost_howManyItNeeds[theAction.pieceType];
        if (need <= 0) return false;

        int[] owned = Scratch.GetScratchCellBuffer(gameIndex);
        int ownedCount = bm.GetOwnedPieceIds(playerId, owned);
        if (ownedCount < need) return false;

        int excludePid = -1;
        if (theAction.kind == ActionKind.Upgrade)
        {
            int pid = bm.GetCellOccupant(theAction.ActorsCellId);
            if (pid >= 0) excludePid = pid;
        }

        // Filter eligible pieces into the front of the same buffer
        int eligibleCount = 0;
        bool requiresSpecific = PieceDefinition.sacrificeCost_isNeedsSpecificPiece[theAction.pieceType];
        int requiredType = PieceDefinition.sacrificeCost_specificPiece[theAction.pieceType];

        for (int i = 0; i < ownedCount; i++)
        {
            int pid = owned[i];
            if (pid == excludePid) continue;
            if (!bm.IsValidPieceId(pid)) continue;
            if (requiresSpecific && bm.pieceType[pid] != requiredType) continue;
            owned[eligibleCount++] = pid;
        }
        if (eligibleCount < need) return false;

        Array.Sort(owned, 0, eligibleCount); // deterministic combos

        // Generate combinations deterministically, capped to avoid blowup
        if (need == 1)
        {
            int limit = Math.Min(eligibleCount, MaxSacrificeCombos);
            for (int i = 0; i < limit; i++)
                outOptions.Add(new[] { owned[i] });
            return outOptions.Count > 0;
        }

        int[] combo = new int[need];
        void Recurse(int start, int depth)
        {
            if (outOptions.Count >= MaxSacrificeCombos) return;
            if (depth == need)
            {
                int[] arr = new int[need];
                Array.Copy(combo, arr, need);
                outOptions.Add(arr);
                return;
            }
            int remainingSlots = need - depth;
            for (int i = start; i <= eligibleCount - remainingSlots; i++)
            {
                combo[depth] = owned[i];
                Recurse(i + 1, depth + 1);
                if (outOptions.Count >= MaxSacrificeCombos) return;
            }
        }

        Recurse(0, 0);
        return outOptions.Count > 0;
    }



    #region Factory Ability
    public static float[] ComputePlayersFactoryIncome(int gameIndex)
    {
        float[] perPlayerFactoryIncome = new float[4];
        Array.Clear(perPlayerFactoryIncome, 0, 4);

        for (int i = 0; i < 4; i++)
        {
            PerPiecePayout income = ComputeDetailedPlayerFactoryIncome(i, gameIndex);
            float total = 0f;

            for (int c = 0; c < income.payout.Length; i++)
            {
                total += income.payout[c];
            }
            perPlayerFactoryIncome[i] = total;
        }
        return perPlayerFactoryIncome;
    }

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
        var counts = Game.Core.GameState.SnapshotOwnerTypeCounts(bm); // map of (owner,type) -> count

        foreach (var kv in counts)
        {
            if (kv.Key.owner != playerId)
                continue;

            int owner = kv.Key.owner;
            if (owner < 0 || owner >= gameState.ps.Length) continue;

            int type = kv.Key.type;
            int count = kv.Value;

            if (!PieceDefinition.factory_enabled[type]) continue;

            float AuxPayout = AuxFactoryPayout(owner, type, gameIndex);

            int baseAmt = PieceDefinition.factory_amount[type];
            if (baseAmt == 0 && AuxPayout == 0) continue;

            // Flags for scaling
            bool roundMul = PieceDefinition.factory_isRoundMultiplier[type];

            bool group = PieceDefinition.factory_isGroup[type];

            int groupAmt = PieceDefinition.factory_groupAmount[type];

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



    private static float AuxFactoryPayout(int playerId, int thisType, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        float payOut = 0;

        for (int pieceId = 0; pieceId < bm.pieceCount; pieceId++)
        {
            byte type = bm.GetPieceType(pieceId);
            if (thisType != type) continue;
            if (bm.pieceOwner[pieceId] != playerId) continue;

            payOut += bm.pieceFactoryAux[pieceId];
        }
        return payOut;
    }

    #endregion

    #region Sanctuary
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

            if (!PieceDefinition.sanctuary_enabled[type]) continue;
            sanctuaryRange = PieceDefinition.sanctuary_range[type];

            if (sanctuaryRange < 0) continue;
            int centerCell = bm.pieceCellId[pid];
            if (centerCell < 0) continue;

            for (int range = 0; range <= sanctuaryRange; range++)
            {
                int found = BmAbilityCac.pieceIdsRingAroundCell(centerCell, range, protectedpieces, gameIndex);

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
    #endregion
    #region Feeding Ground

    public static void FeedingGround(int gameIndex, int pieceIDKilled)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        int[] PiecesInRange = Scratch.GetScratchCellBuffer(gameIndex);

        int Player = bm.pieceOwner[pieceIDKilled];

        for (int pid = 0; pid < bm.pieceCount; pid++)
        {
            if (bm.pieceOwner[pid] == Player) continue;
            int pieceType = bm.pieceType[pid];
            if (!PieceDefinition.feedingGround_enabled[pieceType]) continue;

            for (int range = 0; range <= PieceDefinition.feedingGround_Range[pieceType]; range++)
            {
                int found = BmAbilityCac.pieceIdsRingAroundCell(bm.pieceCellId[pid], range, PiecesInRange, gameIndex);

                if (found <= 0) continue;

                for (int i = 0; i < found; i++)
                {
                    if (PiecesInRange[i] != pieceIDKilled) continue;
                    bm.pieceFactoryAux[pid] += PieceDefinition.feedingGround_payOut[pieceType];
                }
            }
        }
    }

    #endregion
}
