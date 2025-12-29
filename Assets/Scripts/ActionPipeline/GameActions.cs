using UnityEngine;
using static Game.Core.ActionKind;
using System;
using System.Collections.Generic;

namespace Game.Core
{
    public static class GameActions
    {
        public static void pieceKilled(int victim, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;
            var bm = GameRegistry.game[gameIndex].boardModel;

            int deadOwner = bm.GetPieceOwner(victim);
            byte deadType = bm.GetPieceType(victim);
            int g = Piece.digitItGives[deadType];
            if (g >= 0) gameState.ps[deadOwner].RevokeDigit(g);
            bm.FreeRowSwapBack(victim);
            RefreshConnectorState(gameIndex);
        }

        public static void pieceKilled(int victim, int gameIndex, Action theAction)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;
            var gameState = GameRegistry.game[gameIndex].gameState;

            int pieceId = bm.GetCellOccupant(theAction.ActorsCellId);

            bm.pieceKillCount[pieceId]++;
            bm.pieceFactoryAux[pieceId] += Piece.eat_amount[theAction.pieceType];
            gameState.ps[bm.pieceOwner[pieceId]].perRoundPieceKillCount++;

            PassiveActions.FeedingGround(gameIndex, victim);

            pieceKilled(victim, gameIndex);
        }


        public static bool ApplyDamageWithCapital(int attackerCell, int targetPid, int dmg, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;

            if (targetPid < 0 || dmg <= 0) return false;

            int incomingDir = BmAbilityCac.GetDirectionIndex(attackerCell, bm.GetPieceCell(targetPid), gameIndex);
            if (incomingDir >= 0 && Piece.connectors_enabled[bm.GetPieceType(targetPid)])
            {
                int hitSide = PiecesSides.OppositeDir(incomingDir);
                bool sideIsConnector = PiecesSides.IsConnectorSide(bm.pieceConnectorConfig[targetPid], hitSide);
                if (!sideIsConnector)
                {
                    int cap = bm.pieceCapitalHP[targetPid];
                    if (cap > 0)
                    {
                        int remaining = cap - dmg;
                        if (remaining >= 0)
                        {
                            bm.pieceCapitalHP[targetPid] = remaining;
                            dmg = 0;
                        }
                        else
                        {
                            bm.pieceCapitalHP[targetPid] = 0;
                            dmg = (short)(-remaining);
                        }
                    }
                }
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
                if (bm.IsValidPieceId(victimID))
                    pieceKilled(victimID, gameIndex);
            }
        }



        public static void ResolveMelee(int actorPid, int victimID, in Action theAction, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;

            int dmg = Piece.move_damage[theAction.pieceType];
            bool killed = ApplyDamageWithCapital(actorPid, victimID, dmg, gameIndex);
            if (killed)
            {
                pieceKilled(victimID, gameIndex, theAction);
            }
            else
            {
                int origin = theAction.ActorsCellId;
                int best = BmAbilityCac.FindNearestEmptyAdjacent(origin, bm.GetPieceCell(victimID), gameIndex);
                if (best >= 0) bm.MovePieceRow(actorPid, best);
            }
            // Connector state refresh happens in GameActions after move/shoot/push/kill
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



        public static void GetMultiCreateState(out bool active, out byte type, out bool border, out int remaining, out int[] cells, out int count, int gameIndex)
        {
            var gameState = GameRegistry.game[gameIndex].gameState;

            active = gameState.multiCreateActive;
            type = gameState.multiCreateType;
            border = gameState.multiCreateBorder;
            remaining = gameState.multiCreateRemaining;
            cells = gameState.multiCreateCells.ToArray();
            count = gameState.multiCreateCells.Count;
        }


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
