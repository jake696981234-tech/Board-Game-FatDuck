using System.Collections.Generic;
using System;

public static class PassiveActions
{

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

    private static PerPiecePayout ComputeDetailedPlayerFactoryIncome(int playerId, int gameIndex)
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

            int factoryAid = GetFactoryAbilityId((byte)type); // ability slot that grants factory income
            if (factoryAid < 0) continue;

            float AuxPayout = AuxFactoryPayout(owner, type, gameIndex);

            int baseAmt = (factoryAid < Pieces.factory_amount.Length)
                ? Pieces.factory_amount[factoryAid]
                : 0;
            if (baseAmt == 0 && AuxPayout == 0) continue;

            // Flags for scaling
            bool roundMul = factoryAid < Pieces.factory_roundMultiplier.Length
                         && Pieces.factory_roundMultiplier[factoryAid];

            bool group = factoryAid < Pieces.factory_group.Length
                      && Pieces.factory_group[factoryAid];

            int groupAmt = (factoryAid < Pieces.factory_groupAmount.Length)
                ? Pieces.factory_groupAmount[factoryAid]
                : 1;

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

    private static int GetFactoryAbilityId(byte type)
    {
        int limit = Pieces.AbilitySlotCount(type);
        for (int s = 0; s < limit; s++)
        {
            int aid = Pieces.AbilityIdAtSlot(type, s);
            if (aid >= 0 && Pieces.abilityKind[aid] == Pieces.AbilityKind.Factory)
                return aid;
        }
        return -1;
    }

    private static float AuxFactoryPayout(int playerId, int thisType, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        float payOut = 0;

        for (int pid = 0; pid < bm.pieceCount; pid++)
        {
            byte type = bm.GetPieceType(pid);
            if (thisType != type) continue;
            if (!Pieces.HasAbilityKind(type, Pieces.AbilityKind.Factory)) continue;
            if (bm.pieceOwner[pid] != playerId) continue;

            payOut += bm.pieceFactoryAux[pid];
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
            int slotLimit = Pieces.AbilitySlotCount(type);
            for (int s = 0; s < slotLimit; s++)
            {
                int aid = Pieces.AbilityIdAtSlot(type, s);
                if (aid < 0 || aid >= Pieces.sanctuary_enabled.Length) continue;
                if (Pieces.abilityKind[aid] != Pieces.AbilityKind.Sanctuary) continue;
                if (!Pieces.sanctuary_enabled[aid]) continue;
                sanctuaryRange = (aid < Pieces.Sanctuary_range.Length) ? Pieces.Sanctuary_range[aid] : -1;
                break;
            }

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
}
