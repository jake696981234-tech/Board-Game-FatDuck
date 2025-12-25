using UnityEngine;
using System.Collections.Generic;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class CreateAction
{
    public static void CreateActions(int cell, OfferBuild offerBuild, ref int[] scratch)
    {
        if (!PieceLimitReached(offerBuild))
        {
            if (!isCellLegalPlacement(cell, offerBuild, ref scratch)) return;

            // For each buildable type (default: all types 0..TypeCount-1)
            int typeCount = PieceDefinition.typeCount;
            for (int type = 0; type < typeCount; type++)
            {
                if (!isPieceTypeLegal(type)) return;
                GenerateCompleteCreateActions(type);
            }
        }
    }

    public static bool PieceLimitReached(OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        bool limitActive = offerBuild.query.pieceLimitEnabled && offerBuild.query.pieceLimitPerPlayer > 0;
        return limitActive && bm.GetPieceCountForPlayer(offerBuild.query.playerId) >= offerBuild.query.pieceLimitPerPlayer; 
    }

    public static void GenerateCompleteCreateActions(int type, in Game.Core.Action theAction, OfferBuild offerBuild)
    {
        bool hasConn = PieceDefinition.connectors_enabled[type];
        bool hasSacCost = PieceDefinition.sacrificeCost_enabled[type];
        List<Game.Core.Action> CreateActions = new List<Game.Core.Action>();
        
        if (hasConn) CreateActions.AddRange(CreateConnectorOptions());
        if (hasSacCost) CreateActions.AddRange(GenerateSacrificeCosts(in theAction, offerBuild, ));
    }

    
    public static List<Game.Core.Action> CreateConnectorOptions() //to do- make thi return the actions
    {
        List<Game.Core.Action> ConnectorActions = new List<Game.Core.Action>(64);
        for (int cfg = 0; cfg < 64; cfg++)
        {
            // if ((allowedMask & (1UL << cfg)) == 0) continue;
            if (!PiecesSides.IsConnectorPlacementLegal(cell, (byte)type, cfg, offerBuild.query.playerId, gameIndex))
                continue;

            var theAction = new Game.Core.Action
            {
                kind = Create,
                pieceType = (byte)type,
                ActorsCellId = (ushort)0xFFFF,
                TargetCellId = (ushort)cell,
                aux = (ushort)cfg // carry config index
            };

            ConnectorActions.Add(theAction);
        }
        return ConnectorActions;
    }

    

    public static bool isPieceTypeLegal(int type)
    {
        if (!PieceDefinition.isBuildable[type]) return false; // buildable gate (CSV flag)
        if (!HasRequiredDigits()) return false;                                            
        
        bool hasConn = PieceDefinition.connectors_enabled[type];
        ulong allowedMask = hasConn ? PieceDefinition.connector_allowedMasks[type] : 0UL;
        if (hasConn && allowedMask == 0UL) return false;

        return true;
    }

    public static bool isCellLegalPlacement(int cell, OfferBuild offerBuild, ref int[] scratch)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        if (!bm.IsEmpty(cell)) return false; // only empties

        int coreCell = bm.GetPlayerCoreCellId(offerBuild.query.playerId);
        bool legal = (cell == coreCell); // allow 'on core'
        if (!legal) legal = isBaseHex(offerBuild, coreCell, ref scratch, cell);
        if (!legal) legal = isAdjecentABuilding(cell, ref scratch);
        if (!legal) return false;
        return true;
    }

    public static bool isBaseHex(OfferBuild offerBuild, int coreCell, ref int[] scratch, int cell)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        int numberOfCoreNeighbors = bm.GetNeighbors(coreCell, scratch);
        for (int i = 0; i < numberOfCoreNeighbors; i++) 
        { 
            if (scratch[i] == cell) return true;
        }
        return false;
    }

    public static bool isAdjecentABuilding(int cell, ref int[] scratch, bool legal, OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        int nNbrs = bm.GetNeighbors(cell, scratch);
        for (int i = 0; i < nNbrs && !legal; i++)
        {
            int nbCell = scratch[i];
            int nbPid = bm.GetCellOccupant(nbCell);
            if (newOfferProvider.IsInvalid(bm, nbPid)) continue;
            if (bm.GetPieceOwner(nbPid) != offerBuild.query.playerId) continue;
            byte nbType = bm.GetPieceType(nbPid);
            if (PieceDefinition.isBuilding[nbType]) return true;
        }
        return false;
    }

    public static bool HasRequiredDigits()
    {
        int req = PieceDefinition.requiredDigit[(byte)type];
        if (req >= 0 && !gameState.ps[player].HasDigit(req)) return false;

        return true;
    }

    /// <summary>
    /// Populate sacrifice options for a create action.
    /// Returns false if no legal options exist.
    /// </summary>
    public static bool GenerateSacrificeCosts(in Game.Core.Action theAction, OfferBuild offerBuild, List<int[]> outOptions)
    {
        outOptions.Clear();

        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

        int need = PieceDefinition.sacrificeCost_howManyItNeeds[theAction.pieceType];
        if (need <= 0) return false;

        int[] owned = Scratch.GetScratchCellBuffer(offerBuild.gameIndex);
        int ownedCount = bm.GetOwnedPieceIds(offerBuild.player, owned);
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


    public static void Apply(in Action theAction, byte player, int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        var bm = GameRegistry.game[gameIndex].boardModel;
        

        // Upgrade-create: ActorsCellId carries source piece id (for upgrade flow)
        int sourcePid = bm.GetCellOccupant(theAction.ActorsCellId);
        bool isUpgradeCreate = PieceDefinition.upgrade_enabled[theAction.pieceType] && sourcePid >= 0 && bm.IsValidPieceId(sourcePid);
        byte sourceType = isUpgradeCreate ? bm.GetPieceType(sourcePid) : (byte)0xFF;
        if (isUpgradeCreate)
        {
            int expectedSourceType = PieceDefinition.upgrade_target[theAction.pieceType];
            if (sourceType != expectedSourceType) isUpgradeCreate = false;
        }

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
                    pieceKilled(pieceid, gameIndex);
                    killed++;
                }
                if (killed < need) return; // safety: not enough valid sacrifices
            }
        }

        int pid = bm.AllocateRow();
        bm.PlacePieceRow(pid, player, (byte)theAction.pieceType, theAction.TargetCellId, PieceDefinition.maxHP[theAction.pieceType]);
        // Set connector config if applicable (Create uses aux for config index)
        bm.pieceConnectorConfig[pid] = (byte)theAction.aux;
        // Preserve connector config from source on upgrade
        if (isUpgradeCreate && sourcePid >= 0)
            bm.pieceConnectorConfig[pid] = bm.pieceConnectorConfig[sourcePid];
        // Grant digit if this type provides one
        int g = PieceDefinition.digitItGives[(byte)theAction.pieceType];
        if (g >= 0) gameState.ps[player].GrantDigit(g);

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

        RefreshConnectorState(gameIndex);

        // Remove source piece after placing new one (no connector wipe for intermediate state)
        if (isUpgradeCreate && bm.IsValidPieceId(sourcePid))
        {
            bm.FreeRowSwapBack(sourcePid);
        }
    }
}
