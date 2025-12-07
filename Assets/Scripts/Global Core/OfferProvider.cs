// OfferProvider.cs — decoupled Create enumeration + EndTurn appended
// Zero-alloc hot path, deterministic ordering.
// Kinds (from Game.Core.ActionKind): Move, Shoot, CaptureVP, CoreDamage, Create, EndTurn
// Changes vs attached base:
//  - Create actions no longer come from ability slots. We enumerate legal empty cells and buildable piece types directly.
//  - Emit format for Create is locked to: { kind=Create, abilitySlot=0, pieceType=t, srcCell=0xFFFF, dstCell=cell, aux=0 }.
//  - Determinism: actors by cellId↑, slots↑ for ability-based kinds; for Create, cells↑ then type↑; EndTurn last.
//  - EndTurn is always appended as final action.

using System;
using System.Runtime.CompilerServices;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class OfferProvider
{
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
            EmitMultiCreatePlacements(q, outActions, outCosts, outMask, ref write, ref total, gameIndex, player);
            // Always offer EndTurn as escape hatch
            var end = new Action
            {
                kind = EndTurn,
                abilitySlot = 0,
                pieceType = 0,
                srcCell = 0xFFFF,
                dstCell = 0,
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
            int slotCount = Pieces.AbilitySlotCount(actorType);

            for (int slot = 0; slot < slotCount; slot++)
            {
                int abilityId = Pieces.AbilityIdAtSlot(actorType, slot);
                var abilityKind = (Pieces.AbilityKind)Pieces.GetAbilityKind(actorType, slot);

                switch (abilityKind)
                {
                    case Pieces.AbilityKind.Move:
                        {
                            int n = GetLegalTargets.GetLegalTargets_Move(abilityId, pieceId, scratch, gameIndex);
                            for (int i = 0; i < n; i++)
                            {
                                int dst = scratch[i];
                                var a = new Action
                                {
                                    kind = Move,
                                    abilitySlot = (byte)slot,
                                    pieceType = 0,
                                    srcCell = (ushort)cell,
                                    dstCell = (ushort)dst,
                                    aux = 0
                                };
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                            }
                            break;
                        }
                    case Pieces.AbilityKind.Shoot:
                        {
                            int n = GetLegalTargets.GetLegalTargets_Shoot(abilityId, pieceId, scratch, gameIndex);
                            for (int i = 0; i < n; i++)
                            {
                                int tgtPid = scratch[i];
                                ushort dst = (ushort)bm.GetPieceCell(tgtPid);
                                var a = new Action
                                {
                                    kind = Shoot,
                                    abilitySlot = (byte)slot,
                                    pieceType = 0,
                                    srcCell = (ushort)cell,
                                    dstCell = dst,
                                    aux = (ushort)tgtPid
                                };
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                            }
                            break;
                        }
                    case Pieces.AbilityKind.CaptureVP:
                        {
                            if (IsItLegal.IsLegal_CaptureVP(pieceId, abilityId, gameIndex))
                            {
                                ushort vpCell = (ushort)bm.GetVictoryPointCellId();
                                var a = new Action
                                {
                                    kind = CaptureVP,
                                    abilitySlot = (byte)slot,
                                    pieceType = 0,
                                    srcCell = (ushort)cell,
                                    dstCell = vpCell,
                                    aux = 0
                                };
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                            }
                            break;
                        }
                    case Pieces.AbilityKind.CoreDamage:
                        {
                            if (IsItLegal.IsLegal_CoreDamage(pieceId, abilityId, gameIndex))
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
                                    abilitySlot = (byte)slot,
                                    pieceType = 0,
                                    srcCell = (ushort)cell,
                                    dstCell = dstCore,
                                    aux = 0
                                };
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                            }
                            break;
                        }
                    case Pieces.AbilityKind.Create:
                        // NOTE: Create via ability is intentionally ignored in favor of the global Create path below.
                        break;
                    case Pieces.AbilityKind.Push:
                        {
                            int n = GetLegalTargets.GetLegalTargets_Push(abilityId, pieceId, scratch, gameIndex);
                            for (int i = 0; i < n; i++)
                            {
                                int tgtPid = scratch[i];
                                ushort dst = (ushort)bm.GetPieceCell(gameIndex);
                                var a = new Action
                                {
                                    kind = Push,
                                    abilitySlot = (byte)slot,
                                    pieceType = 0,
                                    srcCell = (ushort)cell,
                                    dstCell = dst,
                                    aux = (ushort)tgtPid
                                };
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                            }
                            break;
                        }
                    case Pieces.AbilityKind.GroupBuild:
                        {
                            int tgtType = Pieces.groupBuildTargetType[actorType];
                            if (tgtType >= 0 && tgtType < Pieces.typeCount)
                            {
                                int require = Pieces.groupBuildRequireNumber[actorType];
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
                            break;
                        }
                    case Pieces.AbilityKind.Upgrade:
                        {
                            if (Pieces.upgradeEnabled[actorType])
                            {
                                int targetType = Pieces.upgradeTargetType[actorType];
                                if (targetType >= 0 && targetType < Pieces.typeCount)
                                {
                                    // Digit/buildable gate for target type
                                    if (Pieces.IsBuildable((byte)targetType))
                                    {
                                        int reqDigit = Pieces.GetRequiredDigit((byte)targetType);
                                        if (reqDigit < 0 || gameState.ps[player].HasDigit(reqDigit))
                                        {
                                            var a = new Action
                                            {
                                                kind = Upgrade,
                                                abilitySlot = (byte)slot,
                                                pieceType = (byte)targetType,
                                                srcCell = (ushort)cell,
                                                dstCell = (ushort)cell,
                                                aux = 0
                                            };
                                            Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                                        }
                                    }
                                }
                            }
                            break;
                        }
                    case Pieces.AbilityKind.Launcher:
                        {
                            int n = GetLegalTargets.GetLegalTargets_Launcher(abilityId, pieceId, scratch, gameIndex);
                            for (int i = 0; i < n; i += 2)
                            {
                                int tgtPid = scratch[i];
                                int dst = scratch[i + 1];
                                var a = new Action
                                {
                                    kind = Launcher,
                                    abilitySlot = (byte)slot,
                                    pieceType = 0,
                                    srcCell = (ushort)cell,
                                    dstCell = (ushort)dst,
                                    aux = (ushort)tgtPid
                                };
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                            }
                            break;
                        }
                    case Pieces.AbilityKind.Spawner:
                        {
                            EmitSpawnerActions(q, abilityId, pieceId, cell, slot, ref write, ref total, cap, outActions, outCosts, outMask, gameIndex, player);
                            break;
                        }
                    case Pieces.AbilityKind.SacrificeFactory:
                        {
                            int n = GetLegalTargets.GetLegalTargets_SacrificeFactory(abilityId, pieceId, scratch, gameIndex);
                            for (int i = 0; i < n; i++)
                            {
                                int tgtPid = scratch[i];
                                ushort dst = (ushort)bm.GetPieceCell(tgtPid);
                                var a = new Action
                                {
                                    kind = SacrificeFactory,
                                    abilitySlot = (byte)slot,
                                    pieceType = 0,
                                    srcCell = (ushort)cell,
                                    dstCell = dst,
                                    aux = (ushort)tgtPid
                                };
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                            }
                            break;
                        }
                    case Pieces.AbilityKind.ConversionFactory:
                        //    fix me
                        {
                            if (IsItLegal.IsLegal_ConversionFactory(gameState.ps[player].vpTotal))
                            {
                                var a = new Action
                                {
                                    kind = ConversionFactory,
                                    abilitySlot = (byte)slot,
                                    pieceType = 0,
                                    srcCell = (ushort)cell,
                                    dstCell = (ushort)cell,
                                    aux = 0
                                };
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                            }
                            break;
                        }

                    default:
                        break;
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

                bool legal = (cell == coreCell); // NEW: allow 'on core'
                if (!legal)
                {
                    int nCore = bm.GetNeighbors(coreCell, scratch);
                    for (int i = 0; i < nCore; i++) { if (scratch[i] == cell) { legal = true; break; } }
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
                        if (Pieces.IsBuilding(nbType)) legal = true;
                    }
                }

                if (!legal) continue;

                // For each buildable type (default: all types 0..TypeCount-1)
                int typeCount = Pieces.typeCount;
                for (int t = 0; t < typeCount; t++)
                {
                    if (!Pieces.IsBuildable((byte)t)) continue; // buildable gate (CSV flag)
                                                                // digitsRequired gate (Plan-B): skip if requirement exists and player lacks it
                    int req = Pieces.GetRequiredDigit((byte)t);
                    if (req >= 0 && !gameState.ps[player].HasDigit(req)) continue;
                    bool hasConn = Pieces.HasConnectors((byte)t);
                    ulong allowedMask = hasConn ? Pieces.connectorAllowedMasks[t] : 0UL;
                    if (hasConn && allowedMask == 0UL) continue;

                    if (!hasConn)
                    {
                        var a = new Action
                        {
                            kind = Create,
                            abilitySlot = 0,
                            pieceType = (byte)t,
                            srcCell = (ushort)0xFFFF, // sentinel no-actor
                            dstCell = (ushort)cell,
                            aux = 0
                        };
                        Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
                    }
                    else
                    {
                        for (int cfg = 0; cfg < 64; cfg++)
                        {
                            if ((allowedMask & (1UL << cfg)) == 0) continue;
                            if (!PiecesSides.IsConnectorPlacementLegal(cell, (byte)t, cfg, q.playerId, gameIndex))
                                continue;

                            var a = new Action
                            {
                                kind = Create,
                                abilitySlot = 0,
                                pieceType = (byte)t,
                                srcCell = (ushort)0xFFFF,
                                dstCell = (ushort)cell,
                                aux = (ushort)cfg // carry config index
                            };
                            Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
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
            abilitySlot = 0,
            pieceType = 0,
            srcCell = (ushort)0xFFFF,
            dstCell = 0,
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
        if (CostEngine.IsAffordable(a, out quoted, player, gameIndex))
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


    //might move this later to GetLegalTargets
    private static void EmitSpawnerActions(
        in OfferQuery q,
        int abilityId,
        int actorPid,
        int actorCell,
        int abilitySlot,
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

        if (abilityId < 0 ||
            Pieces.spawn_pieceAmount == null || Pieces.spawn_range == null || Pieces.spawn_targetType == null || Pieces.spawn_onlyOncePerTurn == null)
            return;

        int amount = Pieces.spawn_pieceAmount[abilityId];
        int range = Pieces.spawn_range[abilityId];
        int targetType = Pieces.spawn_targetType[abilityId];
        bool once = Pieces.spawn_onlyOncePerTurn[abilityId];
        if (amount <= 0 || targetType < 0 || targetType >= Pieces.typeCount) return;

        // Digit gate; buildable override allowed
        int reqDigit = Pieces.GetRequiredDigit((byte)targetType);
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
                abilitySlot = (byte)abilitySlot,
                pieceType = (byte)targetType,
                srcCell = (ushort)actorCell,
                dstCell = (ushort)empties[i],
                aux = 0
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
                abilitySlot = 0,
                pieceType = (byte)type,
                srcCell = (ushort)0xFFFF,
                dstCell = (ushort)cell,
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
            int reqDigit = Pieces.GetRequiredDigit(targetType);
            if (reqDigit >= 0 && !gameState.ps[player].HasDigit(reqDigit)) continue;
            if (!Pieces.IsBuildable(targetType)) continue;
            var a = new Action
            {
                kind = GroupBuild,
                abilitySlot = 0,
                pieceType = targetType,
                srcCell = (ushort)clusterRepresentativeCell,
                dstCell = (ushort)cell,
                aux = 0
            };
            Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask, gameIndex, player);
        }
    }


}
