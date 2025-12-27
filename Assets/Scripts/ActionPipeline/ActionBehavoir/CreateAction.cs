using UnityEngine;
using System.Collections.Generic;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using System;


public static class CreateAction
{
    public static void CreateActions(int cell, ref OfferBuild offerBuild)
    {
        if (PieceLimitReached(ref offerBuild)) return;
        if (!isCellLegalPlacement(cell, ref offerBuild)) return;

        int typeCount = PieceDefinition.typeCount;
        for (int type = 0; type < typeCount; type++)
        {
            if (!isPieceTypeLegal(type, ref offerBuild)) continue;
            GenerateCompleteCreateActions(in cell, in type, ref offerBuild);
        }
    }

    public static void GenerateCompleteCreateActions(in int cell, in int type, ref OfferBuild offerBuild)
    {
         Action theAction = new Game.Core.Action
        {
            kind = Create,
            pieceType = (byte)type,
            ActorsCellId = (ushort)0xFFFF,
            TargetCellId = (ushort)cell,
            aux = 0
        };
        List<Action> CreateActions = new List<Action> {theAction};
        if (PieceDefinition.connectors_enabled[theAction.pieceType] && !CreateConnectorOptions(CreateActions, ref offerBuild)) return;
        if (PieceDefinition.sacrificeCost_enabled[theAction.pieceType] && !GenerateSacrificeCosts(CreateActions, ref offerBuild)) return;
        for (int i = 0; i < CreateActions.Count; i++) { OfferProvider.Emit(CreateActions[i], ref offerBuild); }
    } 

    public static bool PieceLimitReached(ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        bool limitActive = offerBuild.query.pieceLimitEnabled && offerBuild.query.pieceLimitPerPlayer > 0;
        return limitActive && bm.GetPieceCountForPlayer(offerBuild.query.playerId) >= offerBuild.query.pieceLimitPerPlayer; 
    }


    public static bool CreateConnectorOptions(List<Action> actions, ref OfferBuild offerBuild) 
    {
        List<Action> ConnectorActions = new List<Action>();
        bool legal = false;

        for (int i = 0; i < actions.Count; i++)
        {
            for (int cfg = 0; cfg < 64; cfg++)
            {
                // if ((allowedMask & (1UL << cfg)) == 0) continue;
                if (!PiecesSides.IsConnectorPlacementLegal(actions[i].TargetCellId, actions[i].pieceType, cfg, offerBuild.query.playerId, offerBuild.gameIndex)) continue;

                legal = true;
                Action theAction = new Game.Core.Action
                {
                    kind = actions[i].kind,
                    pieceType = actions[i].pieceType,
                    ActorsCellId = actions[i].ActorsCellId,
                    TargetCellId = actions[i].TargetCellId,
                    aux = (ushort)cfg // carry config index
                };
                ConnectorActions.Add(theAction);
            }
        }

        if (legal)
        {
            actions.Clear();
            actions.AddRange(ConnectorActions);
            return true;
        }
        return false;
    }

    

    public static bool isPieceTypeLegal(int type, ref OfferBuild offerBuild)
    {
        if (!PieceDefinition.isBuildable[type]) return false; // buildable gate (CSV flag)
        if (!HasRequiredDigits(type, ref offerBuild)) return false;                                            
        
        bool hasConn = PieceDefinition.connectors_enabled[type];
        ulong allowedMask = hasConn ? PieceDefinition.connector_allowedMasks[type] : 0UL;
        if (hasConn && allowedMask == 0UL) return false;

        return true;
    }

    public static bool isCellLegalPlacement(int cell, ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        if (!bm.IsEmpty(cell)) return false; // only empties

        var coreCell = bm.GetPlayerCoreCellId(offerBuild.query.playerId); 

        if (cell == coreCell) return true;
        if (isBaseHex(ref offerBuild, coreCell, cell)) return true;
        if (isAdjecentABuilding(cell, ref offerBuild)) return true;
        return false;
    }

    public static bool isBaseHex(ref OfferBuild offerBuild, int coreCell, int cell)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        int[] neighScratch = Scratch.GetScratchNeighborBuffer(offerBuild.gameIndex);

        int numberOfCoreNeighbors = bm.GetNeighbors(coreCell, neighScratch);
        for (int i = 0; i < numberOfCoreNeighbors; i++) 
        { 
            if (neighScratch[i] == cell) return true;
        }
        return false;
    }

    public static bool isAdjecentABuilding(int cell, ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

        int[] neighScratch = Scratch.GetScratchNeighborBuffer(offerBuild.gameIndex);
        int nNbrs = bm.GetNeighbors(cell, neighScratch);
        for (int i = 0; i < nNbrs; i++)
        {
            int nbCell = neighScratch[i];
            int nbPid = bm.GetCellOccupant(nbCell);
            if (OfferProvider.IsInvalid(bm, nbPid)) continue;
            if (bm.GetPieceOwner(nbPid) != offerBuild.query.playerId) continue;
            byte nbType = bm.GetPieceType(nbPid);
            if (PieceDefinition.isBuilding[nbType]) return true;
        }
        return false;
    }

    public static bool HasRequiredDigits(int type, ref OfferBuild offerBuild)
    {
        var gameState = GameRegistry.game[offerBuild.gameIndex].gameState;
        int req = PieceDefinition.requiredDigit[(byte)type];
        if (req >= 0 && !gameState.ps[offerBuild.query.playerId].HasDigit(req)) return false;
        return true;
    }

    /// <summary>
    /// Populate sacrifice options for a create action.
    /// Returns false if no legal options exist.
    /// </summary>
    public static bool GenerateSacrificeCosts(List<Game.Core.Action> actions, ref OfferBuild offerBuild)
    {
        // We must preserve the original actions while computing,
        // because we are going to overwrite this same list later.
        int actionCount = actions.Count;

        // Cache original actions (shallow copy is enough)
        // This prevents us from destroying our input.
        var sourceActions = new List<Game.Core.Action>(actions);

        // All actions share the same pieceType
        var firstAction = sourceActions[0];
        int pieceType = firstAction.pieceType;

        int needPerAction = PieceDefinition.sacrificeCost_howManyItNeeds[pieceType];
        bool requiresSpecific = PieceDefinition.sacrificeCost_isNeedsSpecificPiece[pieceType];
        int requiredType = PieceDefinition.sacrificeCost_specificPiece[pieceType];

        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        // ------------------------------------------------------------------
        // 1) Get all pieces owned by the player
        // ------------------------------------------------------------------
        int[] owned = Scratch.GetScratchCellBuffer(offerBuild.gameIndex);
        int ownedCount = bm.GetOwnedPieceIds(offerBuild.query.playerId, owned);

        int totalNeed = needPerAction * actionCount;

        if (ownedCount < totalNeed)
            return false;

        // ------------------------------------------------------------------
        // 2) Build eligible list (shared across all actions)
        // ------------------------------------------------------------------
        int eligibleCount = 0;
        for (int i = 0; i < ownedCount; i++)
        {
            int pid = owned[i];

            if (!bm.IsValidPieceId(pid)) continue;
            if (requiresSpecific && bm.pieceType[pid] != requiredType) continue;

            owned[eligibleCount++] = pid;
        }

        if (eligibleCount < totalNeed)
            return false;

        Array.Sort(owned, 0, eligibleCount); // deterministic

        // ------------------------------------------------------------------
        // 3) Pick the FIRST valid combination of totalNeed pieces
        // ------------------------------------------------------------------
        int[] chosen = new int[totalNeed];
        bool found = false;

        void RecurseChoose(int startIndex, int depth)
        {
            if (found) return;

            if (depth == totalNeed)
            {
                // We now OVERWRITE the original list contents
                actions.Clear();

                int read = 0;
                for (int ai = 0; ai < actionCount; ai++)
                {
                    int[] addCost = new int[needPerAction];
                    Array.Copy(chosen, read, addCost, 0, needPerAction);
                    read += needPerAction;

                    // Sort addCost descending
                    Array.Sort(addCost);
                    Array.Reverse(addCost);

                    var src = sourceActions[ai];
                    actions.Add(new Game.Core.Action
                    {
                        kind = src.kind,
                        pieceType = src.pieceType,
                        ActorsCellId = src.ActorsCellId,
                        TargetCellId = src.TargetCellId,
                        aux = src.aux,
                        addCost = addCost
                    });
                }

                found = true;
                return;
            }

            int remaining = totalNeed - depth;
            for (int i = startIndex; i <= eligibleCount - remaining; i++)
            {
                chosen[depth] = owned[i];
                RecurseChoose(i + 1, depth + 1);
                if (found) return;
            }
        }

        RecurseChoose(0, 0);
        return found;
    }



    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        var bm = GameRegistry.game[gameIndex].boardModel;

        PaySacCost(theAction, player, gameIndex);

        int pid = bm.AllocateRow();
        bm.PlacePieceRow(pid, player, (byte)theAction.pieceType, theAction.TargetCellId, PieceDefinition.maxHP[theAction.pieceType]);
        bm.pieceConnectorConfig[pid] = (byte)theAction.aux;
        int g = PieceDefinition.digitItGives[(byte)theAction.pieceType];
        if (g >= 0) gameState.ps[player].GrantDigit(g);

        MultiCreateExecute(theAction, gameIndex);

        GameActions.RefreshConnectorState(gameIndex);
    }

    public static void MultiCreateExecute(Action theAction, int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        if (PieceDefinition.multiCreate_enabledByType[theAction.pieceType])
        {
            int total = Math.Max(1, PieceDefinition.multiCreate_amountByType[theAction.pieceType]);
            if (total > 1)
            {
                gameState.multiCreateActive = true;
                gameState.multiCreateType = (byte)theAction.pieceType;
                gameState.multiCreateBorder = PieceDefinition.multiCreate_isBoardering[theAction.pieceType];
                gameState.multiCreateRemaining = total - 1;
                gameState.multiCreateCells.Clear();
                gameState.multiCreateCells.Add(theAction.TargetCellId);
            }
        }
    }
    public static void PaySacCost(Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        if (PieceDefinition.sacrificeCost_enabled[theAction.pieceType])
        {
            int need = PieceDefinition.sacrificeCost_howManyItNeeds[theAction.pieceType];
            if (need > 0 && theAction.addCost != null)
            {
                int killed = 0;
                for (int i = 0; i < theAction.addCost.Length && killed < need; i++)
                {
                    int pieceid = theAction.addCost[i];
                    if (!bm.IsValidPieceId(pieceid)) continue;
                    if (bm.GetPieceOwner(pieceid) != player) continue;
                    GameActions.pieceKilled(pieceid, gameIndex);
                    killed++;
                }
                if (killed < need) return; // safety: not enough valid sacrifices
            }
        }
    }
}
