using UnityEngine;
using static Game.Core.ActionKind;
using System;
using System.Collections.Generic;

namespace Game.Core
{
    public static class GameActions
    {
        public static void placePiece(ushort wallConfig, int createdPieceType, int targetCell, int player, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;
            var bm = GameRegistry.game[gameIndex].boardModel;
            // PaySacCost(theAction, player, gameIndex);
            int createdPieceId = bm.AllocateRow();
            bm.PlacePieceRow(createdPieceId, player, (byte)createdPieceType, targetCell, Piece.maxHP[createdPieceType]);
            bm.pieceConnectorConfig[createdPieceId] = wallConfig;
            if (Piece.digitItGives[(byte)createdPieceType] >= 0) gameState.ps[player].GrantDigit(Piece.digitItGives[(byte)createdPieceType]);
            InstantFactoryAction.PlaceInstantFactory(createdPieceType: createdPieceType, createdPieceId: createdPieceId, player: player, gameIndex: gameIndex);
            RefreshConnectorState(gameIndex);
        }
        public static void pieceKilledWithNoTriggers(int victimsCell, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;
            var bm = GameRegistry.game[gameIndex].boardModel;

            int g = Piece.digitItGives[(byte)bm.GetPieceTypeFromCell(victimsCell)];
            if (g >= 0) gameState.ps[bm.GetPieceOwnerFromCell(victimsCell)].RevokeDigit(g);
            bm.FreeRowSwapBack(pieceId: bm.occupantPieceId[victimsCell]);
            RefreshConnectorState(gameIndex);
        }

        public static void pieceKilled(int victimsCell, int actorsCell, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var gameState = GameRegistry.game[gameIndex].gameState;

            int ActorsPieceId = bm.GetCellOccupant(actorsCell);

            bm.pieceKillCount[ActorsPieceId]++;
            InstantFactoryAction.IncrementInstantFactoryKills(gameIndex);
            bm.addToEndRoundPayout[ActorsPieceId] += Piece.eat_amount[bm.pieceType[ActorsPieceId]];
            gameState.ps[bm.pieceOwner[ActorsPieceId]].perRoundPieceKillCount++;
            FeedingGroundAction.FeedingGround(gameIndex, bm.GetCellOccupant(victimsCell));
            if (Piece.zombie_enabled[bm.GetPieceTypeFromCell(actorsCell)]) //move this above the above the other benefifts if you dont want the others to trigger
            {
                Zombie(victimsCell, actorsCell, gameIndex);
                return;
            }
            NecroSpawnAction.Record(gameIndex, bm.GetCellOccupant(victimsCell));

            pieceKilledWithNoTriggers(victimsCell: victimsCell, gameIndex: gameIndex);
        }

        private static void Zombie(int victimsCell, int actorsCell, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var gameState = GameRegistry.game[gameIndex].gameState;
            bm.pieceOwner[bm.GetCellOccupant(victimsCell)] = bm.GetPieceOwnerFromCell(actorsCell);
            bm.pieceHP[bm.GetCellOccupant(victimsCell)] = Piece.maxHP[bm.GetPieceTypeFromCell(victimsCell)];
            int g = Piece.digitItGives[(byte)bm.GetPieceTypeFromCell(victimsCell)];
            if (g >= 0) gameState.ps[bm.GetPieceOwnerFromCell(victimsCell)].GrantDigit(g);
            RefreshConnectorState(gameIndex);
        }

        public static bool ApplyTypicalDamageAndPieceKill(int actorsCell, int victimsCell, int dmg, int gameIndex)
        {
            bool killed = ApplyDamageToPiece(ActorsCell: actorsCell, victimCell: victimsCell, dmg: dmg, gameIndex: gameIndex);

            if (killed) pieceKilled(victimsCell: victimsCell, actorsCell: actorsCell, gameIndex: gameIndex);
            return killed;
        }
        public static bool ApplyDamageToPiece(int ActorsCell, int victimCell, int dmg, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            if (dmg <= 0) return false;
            int targetPid = bm.GetCellOccupant(victimCell);
            if (!Piece.connectors_enabled[bm.GetPieceType(targetPid)]) return bm.DamagePieceRow(targetPid, dmg);
            if (!PiecesSides.IsConnectorSide(bm.pieceConnectorConfig[targetPid], PiecesSides.OppositeDir(BmCac.GetDirectionIndex(ActorsCell, victimCell, gameIndex)))) return bm.DamagePieceRow(targetPid, dmg);
            int CapitalHealth = bm.pieceCapitalHP[targetPid];
            if (CapitalHealth <= 0) return bm.DamagePieceRow(targetPid, dmg);
            int CapitalHealthremaining = CapitalHealth - dmg;
            if (CapitalHealthremaining >= 0)
            {
                bm.pieceCapitalHP[targetPid] = CapitalHealthremaining;
                dmg = 0;
            }
            else
            {
                bm.pieceCapitalHP[targetPid] = 0;
                dmg = (short)-CapitalHealthremaining;
            }
            if (dmg <= 0) return false;
            return bm.DamagePieceRow(targetPid, dmg);
        }

        public static void RefreshConnectorState(int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var events = GameRegistry.game[gameIndex].eventManager;

            List<int> toDestroy = PiecesSides.RecomputeConnectorComponents(gameIndex);
            if (toDestroy == null) return;
            for (int i = 0; i < toDestroy.Count; i++)
            {
                int victimID = toDestroy[i];
                if (bm.IsValidPieceId(victimID)) pieceKilledWithNoTriggers(victimsCell: bm.pieceCellId[victimID], gameIndex: gameIndex);
            }
        }



        public static void ResolveMelee(int actorsCell, int victimsCell, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            if (ApplyTypicalDamageAndPieceKill(actorsCell: actorsCell, victimsCell: victimsCell, dmg: Piece.move_damage[bm.GetPieceTypeFromCell(actorsCell)], gameIndex: gameIndex))
            {
                if (bm.IsEmpty(victimsCell)) bm.MovePieceRow(bm.occupantPieceId[actorsCell], victimsCell);
                return;
            }
            bm.MovePieceRow(bm.occupantPieceId[actorsCell], BmCac.FindNearestEmptyAdjacent(actorsCell, victimsCell, gameIndex));
        }




        // public static List<int> CollectClusterCells(byte type, int startCell, int gameIndex)
        // {
        //     var bm = GameRegistry.game[gameIndex].boardModel;

        //     var cells = new List<int>();
        //     if (startCell < 0) return cells;
        //     var visited = Scratch.GetScratchCellBuffer(gameIndex);
        //     Array.Clear(visited, 0, visited.Length);
        //     int[] queue = Scratch.GetScratchCellBuffer(gameIndex);
        //     int head = 0, tail = 0;
        //     queue[tail++] = startCell;
        //     visited[startCell] = 1;
        //     while (head < tail)
        //     {
        //         int cell = queue[head++];
        //         int pid = bm.GetCellOccupant(cell);
        //         if (pid >= 0 && bm.GetPieceType(pid) == type)
        //         {
        //             cells.Add(cell);
        //             int[] neigh = Scratch.GetScratchNeighborBuffer(gameIndex);
        //             int n = bm.GetNeighbors(cell, neigh);
        //             for (int i = 0; i < n; i++)
        //             {
        //                 int nb = neigh[i];
        //                 if (nb < 0 || nb >= visited.Length) continue;
        //                 if (visited[nb] != 0) continue;
        //                 int nbPid = bm.GetCellOccupant(nb);
        //                 if (nbPid < 0 || bm.GetPieceType(nbPid) != type) continue;
        //                 visited[nb] = 1;
        //                 queue[tail++] = nb;
        //             }
        //         }
        //     }
        //     return cells;
        // }



        // public static void GetMultiCreateState(out bool active, out byte type, out bool border, out int remaining, out int[] cells, out int count, int gameIndex)
        // {
        //     var gameState = GameRegistry.game[gameIndex].gameState;

        //     active = gameState.multiCreateActive;
        //     type = gameState.multiCreateType;
        //     border = gameState.multiCreateBorder;
        //     remaining = gameState.multiCreateRemaining;
        //     cells = gameState.multiCreateCells.ToArray();
        //     count = gameState.multiCreateCells.Count;
        // }


        //         #endregion



        //         //Helper to call methods for applyActions easier


    }
}
public readonly struct PerPiecePayout
{
    public readonly int[] pieceType;
    public readonly bool[] isGroup;
    public readonly float[] payout;

    public PerPiecePayout(int[] pieceType, bool[] isGroup, float[] payout)
    {
        this.pieceType = pieceType;
        this.isGroup = isGroup;
        this.payout = payout;
    }
}
