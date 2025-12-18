// OfferProvider.cs — decoupled Create enumeration + EndTurn appended
// Zero-alloc hot path, deterministic ordering.
// Kinds (from Game.Core.ActionKind): Move, Shoot, CaptureVP, CoreDamage, Create, EndTurn
// Changes vs attached base:
//  - Create actions no longer come from ability slots. We enumerate legal empty cells and buildable piece types directly.
//  - Emit format for Create is locked to: { kind=Create, abilitySlot=0, pieceType=t, srcCell=0xFFFF, dstCell=cell, aux=0 }.
//  - Determinism: actors by cellId↑, slots↑ for ability-based kinds; for Create, cells↑ then type↑; EndTurn last.
//  - EndTurn is always appended as final action.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using UnityEngine;

public static class OfferProvider
{
    #region Action List Method
    public static int BuildActionList(
        in OfferQuery q,
        Span<Action> outActions,
        Span<float> outCosts,
        Span<byte> outMask,
        int gameIndex,
        int player)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        var bm = GameRegistry.game[gameIndex].boardModel;

        int cap = outActions.Length;
        if (outCosts.Length < cap) cap = outCosts.Length;
        if (outMask.Length < cap) cap = outMask.Length;

        int write = 0;
        int total = 0;

        // Multi-create pending: emit only placement actions
        if (q.multiCreateActive && q.multiCreateRemaining > 0)
        {
            Debug.Log($"Entering MultiCreatePlacements");
            EmitMultiCreatePlacements(q, outActions, outCosts, outMask, ref write, ref total, gameIndex, player);
            // Always offer EndTurn as escape hatch
            var end = new Action
            {
                kind = EndTurn,
                pieceType = 0,
                ActorsCellId = 0xFFFF,
                TargetCellId = 0,
                aux = 0
            };
            if (write < outActions.Length)
            {
                outActions[write] = end;
                outCosts[write] = 0f;
                outMask[write] = 1;
                write++;
            }
            total++;
            ZeroTail(write, outCosts, outMask);
            return total;
        }

        int[] scratch = Scratch.GetScratchCellBuffer(gameIndex); // neighbor buffer, etc. (no allocs)
        int cellCount = bm.GetCellCount();

        // =============================
        // Ability-based actions (unchanged)
        // =============================
        for (int cell = 0; cell < cellCount; cell++)
        {
            int pieceId = bm.GetCellOccupant(cell);
            if (IsInvalid(bm, pieceId)) continue;
            if ((byte)bm.GetPieceOwner(pieceId) != q.playerId) continue;

            byte actorType = bm.GetPieceType(pieceId);

            if (PieceDefinition.move_enabled[actorType])
            {
                int theNumberOfTargets = GetLegalTargets.GetLegalTargets_Move(pieceId, actorType, scratch, gameIndex);
                for (int i = 0; i < theNumberOfTargets; i++)
                {
                    int targetCellId = scratch[i];
                    var a = new Action
                    {
                        kind = Move,
                        pieceType = actorType,
                        ActorsCellId = (ushort)cell,
                        TargetCellId = (ushort)targetCellId,
                        aux = 0
                    };
                    Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                }
            }
            if (PieceDefinition.shoot_enabled[actorType])
            {
                int theNumberOfTargets = GetLegalTargets.GetLegalTargets_Shoot(pieceId, actorType, scratch, gameIndex);
                for (int i = 0; i < theNumberOfTargets; i++)
                {
                    int tgtPid = scratch[i];
                    ushort targetCellId = (ushort)bm.GetPieceCell(tgtPid);
                    var a = new Action
                    {
                        kind = Shoot,
                        pieceType = actorType,
                        ActorsCellId = (ushort)cell,
                        TargetCellId = targetCellId,
                        aux = (ushort)tgtPid
                    };
                    Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                }
            }
            if (PieceDefinition.captureVP_enabled[actorType])
            {
                if (IsItLegal.IsLegal_CaptureVP(pieceId, gameIndex))
                {
                    ushort vpCell = (ushort)bm.GetVictoryPointCellId();
                    var a = new Action
                    {
                        kind = CaptureVP,
                        pieceType = actorType,
                        ActorsCellId = (ushort)cell,
                        TargetCellId = vpCell,
                        aux = 0
                    };
                    Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                }
            }
            if (PieceDefinition.coreDamage_enabled[actorType])
            {
                if (IsItLegal.IsLegal_CoreDamage(pieceId, gameIndex))
                {
                    // Determine which adjacent cell is the enemy core and set dstCell accordingly
                    ushort dstCore = 0;
                    int owner = (byte)bm.GetPieceOwner(pieceId);
                    int nNbrs = bm.GetNeighbors(cell, scratch);
                    for (int i = 0; i < nNbrs; i++)
                    {
                        int nb = scratch[i];
                        if (nb < 0) continue;
                        if (bm.IsEnemyCoreCell(nb, owner)) { dstCore = (ushort)nb; break; }
                    }

                    var a = new Action
                    {
                        kind = CoreDamage,
                        pieceType = actorType,
                        ActorsCellId = (ushort)cell,
                        TargetCellId = dstCore,
                        aux = 0
                    };
                    Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                }
            }
            if (PieceDefinition.push_enabled[actorType])
            {
                int theNumberOfTargets = GetLegalTargets.GetLegalTargets_Push(pieceId, actorType, scratch, gameIndex);
                for (int i = 0; i < theNumberOfTargets; i++)
                {
                    int tgtPid = scratch[i];
                    ushort targetCellId = (ushort)bm.GetPieceCell(tgtPid);
                    var a = new Action
                    {
                        kind = Push,
                        pieceType = actorType,
                        ActorsCellId = (ushort)cell,
                        TargetCellId = targetCellId,
                        aux = (ushort)tgtPid
                    };
                    Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                }
            }
            if (PieceDefinition.groupBuild_enabled[actorType])
            {
                int tgtType = PieceDefinition.groupBuild_target[actorType];
                if (tgtType >= 0 && tgtType < PieceDefinition.typeCount)
                {
                    int require = PieceDefinition.groupBuild_requireNumber[actorType];
                    if (require > 1)
                    {
                        // Cluster check
                        int clusterSize = BmAbilityCac.CountClusterOfType(actorType, cell, gameIndex);
                        if (clusterSize >= require)
                        {
                            // Enumerate legal create destinations for target type
                            EnumerateGroupBuildCreates(q, (byte)tgtType, cell, actorType, ref write, ref total, cap, outActions, outCosts, outMask, gameIndex, player);
                        }
                    }
                }
            }
            if (PieceDefinition.launcher_enabled[actorType])
            {
                int theNumberOfTargets = GetLegalTargets.GetLegalTargets_Launcher(pieceId, actorType, scratch, gameIndex);
                for (int i = 0; i < theNumberOfTargets; i += 2)
                {
                    int tgtPid = scratch[i];
                    int dst = scratch[i + 1];
                    var a = new Action
                    {
                        kind = Launcher,
                        pieceType = actorType,
                        ActorsCellId = (ushort)cell,
                        TargetCellId = (ushort)dst,
                        aux = (ushort)tgtPid
                    };
                    Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                }
            }
            if (PieceDefinition.spawn_enabled[actorType])
            {
                EmitSpawnerActions(q, pieceId, actorType, cell, ref write, ref total, cap, outActions, outCosts, outMask, gameIndex, player);
            }
            if (PieceDefinition.sacrificeFactory_enabled[actorType])
            {
                int theNumberOfTargets = GetLegalTargets.GetLegalTargets_SacrificeFactory(pieceId, actorType, scratch, gameIndex);
                for (int i = 0; i < theNumberOfTargets; i++)
                {
                    int tgtPid = scratch[i];
                    ushort targetCellId = (ushort)bm.GetPieceCell(tgtPid);
                    var a = new Action
                    {
                        kind = SacrificeFactory,
                        pieceType = actorType,
                        ActorsCellId = (ushort)cell,
                        TargetCellId = targetCellId,
                        aux = (ushort)tgtPid
                    };
                    Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                }
            }
            if (PieceDefinition.conversionFactory_enabled[actorType])
            {
                if (IsItLegal.IsLegal_ConversionFactory(gameState.ps[player].vpTotal))
                {
                    var a = new Action
                    {
                        kind = ConversionFactory,
                        pieceType = actorType,
                        ActorsCellId = (ushort)cell,
                        TargetCellId = (ushort)cell,
                        aux = 0
                    };
                    Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                }
            }

            // Upgrade-as-piece-action: find destination types that upgrade from this actorType
            for (int upgradedToPieceType = 0; upgradedToPieceType < PieceDefinition.typeCount; upgradedToPieceType++)
            {
                if (!PieceDefinition.upgrade_enabled[upgradedToPieceType]) continue;
                if (PieceDefinition.upgrade_target[upgradedToPieceType] != actorType) continue;
                int reqDigit = PieceDefinition.requiredDigit[(byte)upgradedToPieceType];
                if (reqDigit >= 0 && !gameState.ps[player].HasDigit(reqDigit)) continue;

                var a = new Action
                {
                    kind = Upgrade,
                    pieceType = actorType, // destination type
                    ActorsCellId = (ushort)cell, // to do need to change the rest of the method - I switched this around. PieceType = used to be upgradedToPieceType- and TargetCellId used to be cell.
                    TargetCellId = (byte)upgradedToPieceType,
                    aux = 0
                };

                if (PieceDefinition.sacrificeCost_enabled[upgradedToPieceType]) //pretty sure this sets Aux as piece IDs, i made this be reflected in UI. If theres issues, check this.
                {
                    SacrificeCostOptions.Clear();
                    if (PassiveActions.GenerateSacrificeCosts(in a, player, gameIndex, SacrificeCostOptions))
                    {
                        for (int opt = 0; opt < SacrificeCostOptions.Count; opt++)
                        {
                            var withCost = a;
                            withCost.addCost = SacrificeCostOptions[opt];
                            Emit(ref withCost, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                        }
                    }
                }
                else
                {
                    Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                }
            }
        }
        // =============================
        // Global Create actions (decoupled from abilities)
        // Determinism: cells↑ then pieceType↑
        // =============================
        bool limitActive = q.pieceLimitEnabled && q.pieceLimitPerPlayer > 0;
        bool limitReached = limitActive && bm.GetPieceCountForPlayer(q.playerId) >= q.pieceLimitPerPlayer;
        if (!limitReached)
        {
            int coreCell = bm.GetPlayerCoreCellId(q.playerId);
            for (int cell = 0; cell < cellCount; cell++)
            {
                if (!bm.IsEmpty(cell)) continue; // only empties

                bool legal = (cell == coreCell); // allow 'on core'
                if (!legal)
                {
                    int numberOfCoreNeighbors = bm.GetNeighbors(coreCell, scratch);
                    for (int i = 0; i < numberOfCoreNeighbors; i++) { if (scratch[i] == cell) { legal = true; break; } }
                }

                if (!legal)
                {
                    int nNbrs = bm.GetNeighbors(cell, scratch);
                    for (int i = 0; i < nNbrs && !legal; i++)
                    {
                        int nbCell = scratch[i];
                        int nbPid = bm.GetCellOccupant(nbCell);
                        if (IsInvalid(bm, nbPid)) continue;
                        if (bm.GetPieceOwner(nbPid) != q.playerId) continue;
                        byte nbType = bm.GetPieceType(nbPid);
                        if (PieceDefinition.isBuilding[nbType]) legal = true;
                    }
                }

                if (!legal) continue;

                // For each buildable type (default: all types 0..TypeCount-1)
                int typeCount = PieceDefinition.typeCount;
                for (int type = 0; type < typeCount; type++)
                {
                    if (PieceDefinition.upgrade_enabled[type]) continue; // handled as piece action upgrade
                    if (!PieceDefinition.isBuildable[type]) continue; // buildable gate (CSV flag)
                                                                      // digitsRequired gate (Plan-B): skip if requirement exists and player lacks it
                    int req = PieceDefinition.requiredDigit[(byte)type];
                    if (req >= 0 && !gameState.ps[player].HasDigit(req)) continue;
                    bool hasConn = PieceDefinition.connectors_enabled[type];
                    ulong allowedMask = hasConn ? PieceDefinition.connector_allowedMasks[type] : 0UL;
                    if (hasConn && allowedMask == 0UL) continue;

                    if (!hasConn)
                    {
                        var a = new Action
                        {
                            kind = Create,
                            pieceType = (byte)type,
                            ActorsCellId = (ushort)0xFFFF, // sentinel no-actor
                            TargetCellId = (ushort)cell,
                            aux = 0
                        };
                        EmitCreateWithSacrifice(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                    }
                    if (hasConn)
                    {
                        for (int cfg = 0; cfg < 64; cfg++)
                        {
                            if ((allowedMask & (1UL << cfg)) == 0) continue;
                            if (!PiecesSides.IsConnectorPlacementLegal(cell, (byte)type, cfg, q.playerId, gameIndex))
                                continue;

                            var a = new Action
                            {
                                kind = Create,
                                pieceType = (byte)type,
                                ActorsCellId = (ushort)0xFFFF,
                                TargetCellId = (ushort)cell,
                                aux = (ushort)cfg // carry config index
                            };
                            EmitCreateWithSacrifice(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                        }
                    }
                }
            }
        }

        // =============================
        // EndTurn (always present, always last in prefix, always mask=1)
        // =============================
        total++;
        var endTurn = new Action
        {
            kind = EndTurn,
            pieceType = 0,
            ActorsCellId = (ushort)0xFFFF,
            TargetCellId = 0,
            aux = 0
        };
        if (write < cap)
        {
            outActions[write] = endTurn;
            // enforced free/affordable in WriteCostMask; but set here for clarity
            outCosts[write] = 0f;
            outMask[write] = 1;
            write++;
        }
        else if (cap > 0)
        {
            // Buffer full: overwrite the last slot to guarantee EndTurn is in-branch
            int last = cap - 1;
            outActions[last] = endTurn;
            outCosts[last] = 0f;
            outMask[last] = 1;
            write = cap;
        }

        ZeroTail(write, outCosts, outMask);
        return total;
    }

    #endregion
    #region Helpers

    // ---- Emit & helpers ----
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Emit(
        ref Action a,
        ref int write,
        ref int total,
        int cap,
        Span<Action> outActions,
        in OfferQuery q,
        Span<float> outCosts,
        Span<byte> outMask,
        int gameIndex,
        int player)
    {
        total++;
        if (write < cap)
        {
            outActions[write] = a;
            WriteCostMask(a, q, outCosts, outMask, write, gameIndex, player);
            write++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteCostMask(in Action a, in OfferQuery q, Span<float> outCosts, Span<byte> outMask, int idx, int gameIndex, int player)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        // EndTurn is always free & affordable (never masked out by cost)
        if (a.kind == ActionKind.EndTurn)
        {
            outCosts[idx] = 0f;
            outMask[idx] = 1;
            return;
        }

        float quoted;
        if (CostEngine.IsAffordable(a, out quoted, gameIndex, player))
        { outCosts[idx] = quoted; outMask[idx] = 1; }
        else { outCosts[idx] = quoted; outMask[idx] = 0; }
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZeroTail(int write, Span<float> outCosts, Span<byte> outMask)
    {
        for (int i = write; i < outCosts.Length; i++) outCosts[i] = 0f;
        for (int i = write; i < outMask.Length; i++) outMask[i] = 0;
    }

    // ---- BoardModel adapters (1-liners; edit here to match your API names if needed) ----
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInvalid(BoardModel bm, int pieceId) => pieceId == bm.InvalidId;

    private static readonly List<int[]> SacrificeCostOptions = new List<int[]>(64);

    private static void EmitCreateWithSacrifice(
        ref Action baseAction,
        ref int write,
        ref int total,
        int cap,
        Span<Action> outActions,
        in OfferQuery q,
        Span<float> outCosts,
        Span<byte> outMask,
        int gameIndex,
        int player)
    {
        if (!PieceDefinition.sacrificeCost_enabled[baseAction.pieceType])
        {
            Emit(ref baseAction, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
            return;
        }

        SacrificeCostOptions.Clear();
        if (!PassiveActions.GenerateSacrificeCosts(in baseAction, player, gameIndex, SacrificeCostOptions))
            return; // no legal sacrifice options

        for (int i = 0; i < SacrificeCostOptions.Count; i++)
        {
            var withCost = baseAction;
            withCost.addCost = SacrificeCostOptions[i];
            Emit(ref withCost, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
        }
    }


    //might move this later to - this needs to split up of methods
    private static void EmitSpawnerActions(
        in OfferQuery q,
        int actorPid,
        int actorType,
        int actorCell,
        ref int write,
        ref int total,
        int cap,
        Span<Action> outActions,
        Span<float> outCosts,
        Span<byte> outMask,
        int gameIndex,
        int player)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var gameState = GameRegistry.game[gameIndex].gameState;


        int amount = PieceDefinition.spawn_pieceAmount[actorType];
        int range = PieceDefinition.spawn_range[actorType];
        int targetType = PieceDefinition.spawn_targetType[actorType];
        bool once = PieceDefinition.spawn_isOnlyOncePerTurn[actorType];
        if (amount <= 0 || targetType < 0 || targetType >= PieceDefinition.typeCount) return;

        // Digit gate; buildable override allowed
        int reqDigit = PieceDefinition.requiredDigit[(byte)targetType];
        if (reqDigit >= 0 && !gameState.ps[player].HasDigit(reqDigit)) return;

        // Collect empty, LOS-valid cells within range from launcher
        int[] empties = Scratch.GetScratchCellBuffer(gameIndex);
        int eCount = 0;
        int cellCount = bm.GetCellCount();
        for (int c = 0; c < cellCount; c++)
        {
            if (!bm.IsEmpty(c)) continue;
            int dist = bm.Distance(actorCell, c);
            if (dist < 1 || dist > range) continue;
            if (!BmAbilityCac.LineOfSightClear(actorCell, c, gameIndex)) continue;
            empties[eCount++] = c;
        }
        if (eCount <= 0) return;
        Array.Sort(empties, 0, eCount);

        // Piece limit: allow as many as possible
        int availableLimit = int.MaxValue;
        if (q.pieceLimitEnabled && q.pieceLimitPerPlayer > 0)
            availableLimit = q.pieceLimitPerPlayer - bm.GetPieceCountForPlayer(q.playerId);
        int possible = Math.Min(amount, Math.Min(eCount, Math.Max(0, availableLimit)));
        if (possible <= 0) return;

        // Once-per-turn flag cannot be observed here; Perform will reject if already used.

        // Emit one action per legal empty cell (deterministic order)
        for (int i = 0; i < eCount; i++)
        {
            var a = new Action
            {
                kind = Spawner,
                pieceType = (byte)actorType, // pieceType carries the actor type for spawners
                ActorsCellId = (ushort)actorCell,
                TargetCellId = (ushort)empties[i],
                aux = (ushort)targetType // carry target type for UI/debug readability
            };
            Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
        }
    }

    //might move this later to GetLegalTargets
    private static void EmitMultiCreatePlacements(
        in OfferQuery q,
        Span<Action> outActions,
        Span<float> outCosts,
        Span<byte> outMask,
        ref int write,
        ref int total,
        int gameIndex,
        int player)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;

        int cap = outActions.Length;
        int type = q.multiCreateType;
        int remaining = q.multiCreateRemaining;
        if (remaining <= 0) { ZeroTail(write, outCosts, outMask); return; }
        int cellCount = bm.GetCellCount();
        var placed = q.multiCreateCells;
        int placedCount = q.multiCreateCellCount;

        for (int cell = 0; cell < cellCount; cell++)
        {
            if (!bm.IsEmpty(cell)) continue;
            if (q.pieceLimitEnabled && q.pieceLimitPerPlayer > 0 &&
                bm.GetPieceCountForPlayer(q.playerId) + (write + 1) > q.pieceLimitPerPlayer)
                break;

            if (q.multiCreateBorder && placedCount > 0)
            {
                bool adjacent = false;
                int[] neigh = Scratch.GetScratchCellBuffer(gameIndex);
                int n = bm.GetNeighbors(cell, neigh);
                for (int i = 0; i < n; i++)
                {
                    int nb = neigh[i];
                    for (int j = 0; j < placedCount; j++)
                    {
                        if (placed != null && j < placed.Length && placed[j] == nb) { adjacent = true; break; }
                    }
                    if (adjacent) break;
                }
                if (!adjacent) continue;
            }
            else
            {
                if (!BmAbilityCac.IsCreateGeometryLegal(cell, q.playerId, gameIndex)) continue;
            }
            var a = new Action
            {
                kind = Create,
                pieceType = (byte)type,
                ActorsCellId = (ushort)0xFFFF,
                TargetCellId = (ushort)cell,
                aux = 0
            };
            Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
            if (write >= remaining) break;
        }
    }



    // ---- GroupBuild helpers ----

    //might move this later to GetLegalTargets
    private static void EnumerateGroupBuildCreates(
        in OfferQuery q,
        byte targetType,
        int clusterRepresentativeCell,
        byte actorType,
        ref int write,
        ref int total,
        int cap,
        Span<Action> outActions,
        Span<float> outCosts,
        Span<byte> outMask,
        int gameIndex,
        int player)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        var gameState = GameRegistry.game[gameIndex].gameState;

        int cellCount = bm.GetCellCount();
        for (int cell = 0; cell < cellCount; cell++)
        {
            if (!bm.IsEmpty(cell)) continue;
            if (!BmAbilityCac.IsCreateGeometryLegal(cell, q.playerId, gameIndex)) continue;
            int reqDigit = PieceDefinition.requiredDigit[targetType];
            if (reqDigit >= 0 && !gameState.ps[player].HasDigit(reqDigit)) continue;
            var a = new Action
            {
                kind = GroupBuild,
                pieceType = targetType,
                ActorsCellId = (ushort)clusterRepresentativeCell,
                TargetCellId = (ushort)cell,
                aux = 0
            };
            Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
        }
    }
    #endregion

}
