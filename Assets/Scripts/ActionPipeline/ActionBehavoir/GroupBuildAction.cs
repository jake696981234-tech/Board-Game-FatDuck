using UnityEngine;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class GroupBuildAction
{
    public static void CreateActions(in int[] scratch, int pieceId, byte actorType, int cell, OfferBuild offerBuild)
    {
        int tgtType = PieceDefinition.groupBuild_target[actorType];
        if (tgtType >= 0 && tgtType < PieceDefinition.typeCount)
        {
            int require = PieceDefinition.groupBuild_requireNumber[actorType];
            if (require > 1)
            {
                // Cluster check
                int clusterSize = BmAbilityCac.CountClusterOfType(actorType, cell, offerBuild.gameIndex);
                if (clusterSize >= require)
                {
                    // Enumerate legal create destinations for target type
                    EnumerateGroupBuildCreates(q, (byte)tgtType, cell, actorType, ref write, ref total, cap, outActions, outCosts, outMask, gameIndex, player);
                }
            }
        }
    }

    public static bool IsLegal(int actorPid, int abilityId, in Game.Core.Action a, byte currentPlayer, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var gameState = GameRegistry.game[gameIndex].gameState;

        byte actorType = bm.GetPieceType(actorPid);
        if (!PieceDefinition.groupBuild_enabled[actorType]) return false;
        int targetType = PieceDefinition.groupBuild_target[actorType];
        if (targetType < 0 || targetType >= PieceDefinition.typeCount) return false;

        // Create legality for target type at dstCell
        if (bm.GetCellOccupant(a.TargetCellId) >= 0) return false;
        int reqDigit = PieceDefinition.requiredDigit[(byte)targetType];
        if (reqDigit >= 0 && !gameState.ps[currentPlayer].HasDigit(reqDigit)) return false;

        // geometric create gate (core/building adjacency)
        if (!BmAbilityCac.IsCreateGeometryLegal(a.TargetCellId, currentPlayer, gameIndex))
            return false;

        // Cluster size check
        int clusterSize = BmAbilityCac.CountClusterOfType(actorType, bm.GetPieceCell(actorPid), gameIndex);
        return clusterSize >= PieceDefinition.groupBuild_target[actorType];
    }

    public static void Apply(in Action theAction, byte p, int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        var bm = GameRegistry.game[gameIndex].boardModel;
        var events = GameRegistry.game[gameIndex].eventManager;

        int targetType = theAction.pieceType;
        int dst = theAction.TargetCellId;
        int pid = bm.AllocateRow();
        bm.PlacePieceRow(pid, p, (byte)targetType, dst, PieceDefinition.maxHP[targetType]);
        int g = PieceDefinition.digitItGives[(byte)targetType];
        if (g >= 0) gameState.ps[p].GrantDigit(g);

        int ActorsCellId = theAction.ActorsCellId;
        int actorPid = bm.GetCellOccupant(ActorsCellId);
        if (actorPid >= 0)
        {
            byte actorType = bm.GetPieceType(actorPid);
            if (PieceDefinition.groupBuild_deletion[actorType])
            {
                var list = CollectClusterCells(actorType, ActorsCellId, gameIndex);
                int need = PieceDefinition.groupBuild_requireNumber[actorType];
                list.Sort();
                for (int i = 0; i < need && i < list.Count; i++)
                {
                    int cell = list[i];
                    int victim = bm.GetCellOccupant(cell);
                    if (victim >= 0) pieceKilled(victim, gameIndex, theAction);
                }
            }
        }

        RefreshConnectorState(gameIndex);
    }
}
