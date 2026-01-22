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

        int typeCount = Piece.typeCount;
        for (int type = 0; type < typeCount; type++)
        {
            if (!Piece.isBuildable[type]) continue; 
            if (!isPieceTypeLegal(type, ref offerBuild)) continue;
            GenerateCompleteCreateActions(in cell, in type, ref offerBuild);
        }
    }

    public static void GenerateCompleteCreateActions(in int cell, in int type, ref OfferBuild offerBuild)
    {
         Action theAction = new Game.Core.Action
        {
            kind = Create,
            ActorsCell = -1,
            TargetCell = cell,
            TargetType = type,
        };
        List<Action> CreateActions = new List<Action> {theAction};
        if (Piece.connectors_enabled[theAction.TargetType] && !CreateConnectorOptions(CreateActions, ref offerBuild)) return;
        if (Piece.sacrificeCost_enabled[theAction.TargetType] && !GenerateSacrificeCosts(CreateActions, ref offerBuild)) return;
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
                if (!PiecesSides.IsConnectorPlacementLegal(actions[i].TargetCell, (byte)actions[i].TargetType, cfg, offerBuild.query.playerId, offerBuild.gameIndex)) continue;

                legal = true;
                Action theAction = new Game.Core.Action
                {
                    kind = actions[i].kind,
                    ActorsCell = actions[i].ActorsCell,
                    TargetCell = actions[i].TargetCell,
                    TargetType = actions[i].TargetType,
                    WallConfig = (ushort)cfg,
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
        
        if (!HasRequiredDigits(type, ref offerBuild)) return false;                                            
        
        bool hasConn = Piece.connectors_enabled[type];
        ulong allowedMask = hasConn ? Piece.connector_allowedMasks[type] : 0UL;
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
            if (Piece.isBuilding[nbType]) return true;
        }
        return false;
    }

    public static bool HasRequiredDigits(int type, ref OfferBuild offerBuild)
    {
        var gameState = GameRegistry.game[offerBuild.gameIndex].gameState;
        int req = Piece.requiredDigit[(byte)type];
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
        int pieceType = firstAction.TargetType;

        int needPerAction = Piece.sacrificeCost_howManyItNeeds[pieceType];
        bool requiresSpecific = Piece.sacrificeCost_isNeedsSpecificPiece[pieceType];
        int requiredType = Piece.sacrificeCost_specificPiece[pieceType];

        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        // ------------------------------------------------------------------
        // 1) Get all pieces owned by the player
        // ------------------------------------------------------------------
        int[] owned = Scratch.GetScratchCellBuffer(offerBuild.gameIndex);
        int ownedCount = bm.GetOwnedPieceIds(offerBuild.query.playerId, owned);

        if (ownedCount < needPerAction)
            return false;

        // ------------------------------------------------------------------
        // 2) Build eligible list (shared across all actions)
        // ------------------------------------------------------------------
        int eligibleCount = 0;
        for (int i = 0; i < ownedCount; i++)
        {
            int pid = owned[i];
            if (firstAction.kind == Upgrade)
            {
              if (bm.pieceCellId[pid] == firstAction.ActorsCell) continue;  
            }  
            if (!bm.IsValidPieceId(pid)) continue;
            if (requiresSpecific && bm.pieceType[pid] != requiredType) continue;

            owned[eligibleCount++] = pid;
        }

        if (eligibleCount < needPerAction)
            return false;

        Array.Sort(owned, 0, eligibleCount); // deterministic

        // ------------------------------------------------------------------
        // 3) Generate ALL valid combinations per action
        // ------------------------------------------------------------------
        bool foundAny = false;
        int[] combination = new int[needPerAction];

        void RecurseChoose(int startIndex, int depth, Game.Core.Action baseAction)
        {
            if (depth == needPerAction)
            {
                int[] addCost = new int[needPerAction];
                Array.Copy(combination, addCost, needPerAction);

                // Sort addCost descending
                Array.Sort(addCost);
                Array.Reverse(addCost);

                actions.Add(new Game.Core.Action
                {
                    kind = baseAction.kind,
                    ActorsCell = baseAction.ActorsCell,
                    TargetCell = baseAction.TargetCell,
                    TargetType = baseAction.TargetType,
                    WallConfig = baseAction.WallConfig,
                    addCost = addCost
                });

                foundAny = true;
                return;
            }

            int remaining = needPerAction - depth;
            for (int i = startIndex; i <= eligibleCount - remaining; i++)
            {
                combination[depth] = owned[i];
                RecurseChoose(i + 1, depth + 1, baseAction);
            }
        }

        actions.Clear();
        for (int ai = 0; ai < actionCount; ai++)
        {
            var baseAction = sourceActions[ai];
            RecurseChoose(0, 0, baseAction);
        }

        return foundAny;
    }





    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        placePiece(theAction, player, gameIndex);
    }

    public static void placePiece(in Action theAction, byte player, int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        var bm = GameRegistry.game[gameIndex].boardModel;

         var TargetCell = theAction.TargetCell;
         if (theAction.kind == GroupBuild) TargetCell = theAction.TargetCell;

        PaySacCost(theAction, player, gameIndex);

        int pid = bm.AllocateRow();
        bm.PlacePieceRow(pid, player, (byte)theAction.TargetType, TargetCell, Piece.maxHP[theAction.TargetType]);
        bm.pieceConnectorConfig[pid] = (byte)theAction.WallConfig;
        int g = Piece.digitItGives[(byte)theAction.TargetType];
        if (g >= 0) gameState.ps[player].GrantDigit(g);

        if (Piece.factory_isKillPenalty[theAction.TargetType]) bm.pieceFactoryKillGoalAux[pid] = Piece.factory_killsNeeded[pid];
        if (Piece.factory_isInstantPayOut[theAction.TargetType]) gameState.ps[player].budget += Piece.factory_instantPayOutAmount[pid];


        // MultiCreateExecute(theAction, gameIndex);

        GameActions.RefreshConnectorState(gameIndex);
    }

    // public static void MultiCreateExecute(Action theAction, int gameIndex)
    // {
    //     // if (!Piece.multiCreate_enabledByType[theAction.pieceType]) return;
    //     var gameState = GameRegistry.game[gameIndex].gameState;
        
    //     // int total = Math.Max(1, Piece.multiCreate_amountByType[theAction.pieceType]);
    //     // if (total > 1)
    //     // {
    //     //     gameState.multiCreateActive = true;
    //     //     gameState.multiCreateType = (byte)theAction.pieceType;
    //     //     gameState.multiCreateBorder = Piece.multiCreate_isBoardering[theAction.pieceType];
    //     //     gameState.multiCreateRemaining = total - 1;
    //     //     gameState.multiCreateCells.Clear();
    //     //     gameState.multiCreateCells.Add(theAction.TargetCellId);
    //     // }
        
    // }
    public static void PaySacCost(Action theAction, byte player, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        if (Piece.sacrificeCost_enabled[theAction.TargetType])
        {
            int need = Piece.sacrificeCost_howManyItNeeds[theAction.TargetType];
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




    public static bool CopyOfGenerateSacrificeCosts(List<Game.Core.Action> actions, ref OfferBuild offerBuild)
    {
        // We must preserve the original actions while computing,
        // because we are going to overwrite this same list later.
        int actionCount = actions.Count;

        // Cache original actions (shallow copy is enough)
        // This prevents us from destroying our input.
        var sourceActions = new List<Game.Core.Action>(actions);

        // All actions share the same pieceType
        var firstAction = sourceActions[0];
        int pieceType = firstAction.TargetType;

        int needPerAction = Piece.sacrificeCost_howManyItNeeds[pieceType];
        bool requiresSpecific = Piece.sacrificeCost_isNeedsSpecificPiece[pieceType];
        int requiredType = Piece.sacrificeCost_specificPiece[pieceType];

        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        // ------------------------------------------------------------------
        // 1) Get all pieces owned by the player
        // ------------------------------------------------------------------
        int[] owned = Scratch.GetScratchCellBuffer(offerBuild.gameIndex);
        int ownedCount = bm.GetOwnedPieceIds(offerBuild.query.playerId, owned);

        if (ownedCount < needPerAction)
            return false;

        // ------------------------------------------------------------------
        // 2) Build eligible list (shared across all actions)
        // ------------------------------------------------------------------
        int eligibleCount = 0;
        for (int i = 0; i < ownedCount; i++)
        {
            int pid = owned[i];
            if (firstAction.kind == Upgrade)
            {
              if (bm.pieceCellId[pid] == bm.GetCellOccupant(firstAction.ActorsCell)) continue;  
            }  
            if (!bm.IsValidPieceId(pid)) continue;
            if (requiresSpecific && bm.pieceType[pid] != requiredType) continue;

            owned[eligibleCount++] = pid;
        }

        if (eligibleCount < needPerAction) return false;
            

        return addCostCac(owned, needPerAction, sourceActions, actions);

    }

    //input- All Elgiable Pieces, if it needs spercfic, what the sperfic is. theAction

    public static bool addCostCac(int[] PieceCandidates, int howManyNeeded, List<Action> sourceActions, List<Action> actions)
    {
        Array.Sort(PieceCandidates, 0, PieceCandidates.Length); // deterministic

        // ------------------------------------------------------------------
        // 3) Generate ALL valid combinations per action
        // ------------------------------------------------------------------
        bool foundAny = false;
        int[] combination = new int[howManyNeeded];

        void RecurseChoose(int startIndex, int depth, Game.Core.Action baseAction)
        {
            if (depth == howManyNeeded)
            {
                int[] addCost = new int[howManyNeeded];
                Array.Copy(combination, addCost, howManyNeeded);

                // Sort addCost descending
                Array.Sort(addCost);
                Array.Reverse(addCost);

                actions.Add(new Game.Core.Action
                {
                    kind = baseAction.kind,
                    ActorsCell = baseAction.ActorsCell,
                    TargetCell = baseAction.TargetCell,
                    TargetType = baseAction.TargetType,
                    WallConfig = baseAction.WallConfig,
                    addCost = addCost
                });

                foundAny = true;
                return;
            }

            int remaining = howManyNeeded - depth;
            for (int i = startIndex; i <= PieceCandidates.Length - remaining; i++)
            {
                combination[depth] = PieceCandidates[i];
                RecurseChoose(i + 1, depth + 1, baseAction);
            }
        }

        actions.Clear();
        for (int ai = 0; ai < actions.Count; ai++)
        {
            var baseAction = sourceActions[ai];
            RecurseChoose(0, 0, baseAction);
        }

        return foundAny;
    }
}
