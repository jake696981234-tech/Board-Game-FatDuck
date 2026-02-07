using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System;
using System.Collections.Generic;
using static Game.Core.GameActions;


public static class GroupBuildAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    {
        // Cluster check
        if (!isPieceTypeLegal(actorType, ref offerBuild)) return;
        int clusterSize = BmCac.CountClusterOfType(actorType, cell, offerBuild.gameIndex);
        if (clusterSize < Piece.groupBuild_requireNumber[actorType]) return;

        var theAction = new Action
            {
                kind = GroupBuild,
                ActorsCell = cell,
                TargetCell = -1,
                TargetType = Piece.groupBuild_target[actorType],
            };

        var actions = new List<Action> {theAction};

        if (!generateGroupBuildClusters(actions, ref offerBuild)) return;

        
        if (Piece.groupBuild_deletion[actorType]) 
        {
            for (int i = 0; i < actions.Count; i++) OfferProvider.Emit(actions[i], ref offerBuild);
            return;
        }
        EmitGroupBuild(actions.ToArray(), ref offerBuild);
    }

    public static void EmitGroupBuild(Action[] theActions, ref OfferBuild offerBuild)
    {
        if (PieceLimitReached(ref offerBuild)) return;

        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;


        for (int cell = 0; cell < Info.totalCells; cell++)
        {
            if (!isCellLegalPlacement(cell, ref offerBuild)) continue;
            for (int i = 0; i < theActions.Length; i++)
            {
                Action theAction = new Action
                {
                    kind = theActions[i].kind,
                    ActorsCell = theActions[i].ActorsCell,
                    TargetCell = cell,
                    TargetType = theActions[i].TargetType,
                };
                OfferProvider.Emit(theAction, ref offerBuild);
            }
        }
    }

    private static bool generateGroupBuildClusters(List<Action> actions, ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        if (actions == null || actions.Count == 0) return false;

        var sourceActions = new List<Action>(actions);
        actions.Clear();

        bool foundAny = false;
        for (int i = 0; i < sourceActions.Count; i++)
        {
            var baseAction = sourceActions[i];
            int actorType = bm.GetPieceTypeFromCell(sourceActions[i].ActorsCell);
            int startCell = baseAction.ActorsCell;

            var clusterCells = CollectClusterCells((byte)actorType, startCell, offerBuild.gameIndex);
            if (clusterCells.Count == 0) continue;

            // Deterministic ordering for addCost
            clusterCells.Sort();
            clusterCells.Reverse();

            actions.Add(new Action
            {
                kind = baseAction.kind,
                ActorsCell = baseAction.ActorsCell,
                TargetCell = baseAction.TargetCell,
                TargetType = baseAction.TargetType,
                // SacCost = clusterCells.ToArray()
            });
            foundAny = true;
        }

        return foundAny;
    }

     

    public static List<int> CollectClusterCells(byte type, int startCell, int gameIndex)
        {
            var bm = GameRegistry.game[gameIndex].boardModel;

            var cells = new List<int>();
            if (startCell < 0) return cells;
            var visited = Scratch.GetScratchCellBuffer(gameIndex);
            Array.Clear(visited, 0, visited.Length);
            int[] queue = Scratch.GetScratchCellBuffer(gameIndex);
            int head = 0, tail = 0;
            queue[tail++] = startCell;
            visited[startCell] = 1;
            while (head < tail)
            {
                int cell = queue[head++];
                int pid = bm.GetCellOccupant(cell);
                if (pid >= 0 && bm.GetPieceType(pid) == type)
                {
                    cells.Add(cell);
                    int[] neigh = Scratch.GetScratchNeighborBuffer(gameIndex);
                    int n = bm.GetNeighbors(cell, neigh);
                    for (int i = 0; i < n; i++)
                    {
                        int nb = neigh[i];
                        if (nb < 0 || nb >= visited.Length) continue;
                        if (visited[nb] != 0) continue;
                        int nbPid = bm.GetCellOccupant(nb);
                        if (nbPid < 0 || bm.GetPieceType(nbPid) != type) continue;
                        visited[nb] = 1;
                        queue[tail++] = nb;
                    }
                }
            }
            return cells;
        }



    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        
        // if (Piece.groupBuild_deletion[bm.GetPieceTypeFromCell(theAction.ActorsCell)]) groupBuildDeletion(theAction, gameIndex);
        placePiece(wallConfig: theAction.WallConfig, createdPieceType: theAction.TargetType, targetCell: theAction.TargetCell, player: player, gameIndex: gameIndex);
    }

    // private static void groupBuildDeletion(Action theAction, int gameIndex)
    // {
    //     var bm = GameRegistry.game[gameIndex].boardModel;

    //     for (int i = 0; i < theAction.SacCost.Length; i++)
    //     {
    //         int victim = bm.GetCellOccupant(theAction.SacCost[i]);
    //         GameActions.pieceKilled(victim, gameIndex);
    //     }   
    // }
}
