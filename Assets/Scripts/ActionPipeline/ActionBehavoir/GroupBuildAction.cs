using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System;
using System.Collections.Generic;

public static class GroupBuildAction
{
    public static void CreateActions(int pieceId, byte actorType, int cell, ref OfferBuild offerBuild)
    {
        // Cluster check
        if (!CreateAction.isPieceTypeLegal(actorType, ref offerBuild)) return;
        int clusterSize = BmCac.CountClusterOfType(actorType, cell, offerBuild.gameIndex);
        if (clusterSize < Piece.groupBuild_requireNumber[actorType]) return;

        var theAction = new Action
            {
                kind = GroupBuild,
                pieceType = (byte)Piece.groupBuild_target[actorType],
                ActorsCellId = (ushort)cell,
                TargetCellId = (ushort)actorType,
                aux = 0
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
        if (CreateAction.PieceLimitReached(ref offerBuild)) return;

        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        int cellCount = bm.GetCellCount();

        for (int cell = 0; cell < cellCount; cell++)
        {
            if (!CreateAction.isCellLegalPlacement(cell, ref offerBuild)) continue;
            for (int i = 0; i < theActions.Length; i++)
            {
                Action theAction = new Action
                {
                    kind = theActions[i].kind,
                    pieceType = theActions[i].pieceType,
                    ActorsCellId = theActions[i].ActorsCellId,
                    TargetCellId = theActions[i].TargetCellId,
                    aux = (ushort)cell, 
                    addCost = theActions[i].addCost,
                };
                OfferProvider.Emit(theAction, ref offerBuild);
            }
        }
    }

    private static bool generateGroupBuildClusters(List<Action> actions, ref OfferBuild offerBuild)
    {
        if (actions == null || actions.Count == 0) return false;

        var sourceActions = new List<Action>(actions);
        actions.Clear();

        bool foundAny = false;
        for (int i = 0; i < sourceActions.Count; i++)
        {
            var baseAction = sourceActions[i];
            byte actorType = (byte)baseAction.TargetCellId;
            int startCell = baseAction.ActorsCellId;

            var clusterCells = CollectClusterCells(actorType, startCell, offerBuild.gameIndex);
            if (clusterCells.Count == 0) continue;

            // Deterministic ordering for addCost
            clusterCells.Sort();
            clusterCells.Reverse();

            actions.Add(new Action
            {
                kind = baseAction.kind,
                pieceType = baseAction.pieceType,
                ActorsCellId = baseAction.ActorsCellId,
                TargetCellId = baseAction.TargetCellId,
                aux = baseAction.aux,
                addCost = clusterCells.ToArray()
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

    // private static void EnumerateGroupBuildCreates(ref OfferBuild offerBuild, byte targetType, int clusterRepresentativeCell, byte actorType)
    // {
    //     var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
    //     var gameState = GameRegistry.game[offerBuild.gameIndex].gameState;

    //     int cellCount = bm.GetCellCount();
    //     for (int cell = 0; cell < cellCount; cell++)
    //     {
    //         if (!bm.IsEmpty(cell)) continue;
    //         if (!BmCac.IsCreateGeometryLegal(cell, offerBuild.query.playerId, offerBuild.gameIndex)) continue;
    //         int reqDigit = Piece.requiredDigit[targetType];
    //         if (reqDigit >= 0 && !gameState.ps[offerBuild.query.playerId].HasDigit(reqDigit)) continue;
    //         var a = new Action
    //         {
    //             kind = GroupBuild,
    //             pieceType = targetType,
    //             ActorsCellId = (ushort)clusterRepresentativeCell,
    //             TargetCellId = (ushort)actorType,
    //             aux = 0
    //         };
    //         OfferProvider.Emit(a, ref offerBuild);
    //     }
    // }

    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        if (Piece.groupBuild_deletion[theAction.TargetCellId]) groupBuildDeletion(theAction, gameIndex);
        CreateAction.placePiece(theAction, player, gameIndex);
    }

    private static void groupBuildDeletion(Action theAction, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        for (int i = 0; i < theAction.addCost.Length; i++)
        {
            int victim = bm.GetCellOccupant(theAction.addCost[i]);
            GameActions.pieceKilled(victim, gameIndex);
        }   
    }
}
