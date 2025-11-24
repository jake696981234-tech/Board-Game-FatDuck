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

public sealed class OfferProvider
{
    public int BuildActionList(
        in OfferQuery q,
        Span<Action> outActions,
        Span<float> outCosts,
        Span<byte> outMask)
    {
        int cap = outActions.Length;
        if (outCosts.Length < cap) cap = outCosts.Length;
        if (outMask.Length < cap) cap = outMask.Length;

        int write = 0;
        int total = 0;

        // Multi-create pending: emit only placement actions
        if (q.multiCreateActive && q.multiCreateRemaining > 0)
        {
            EmitMultiCreatePlacements(q, outActions, outCosts, outMask, ref write, ref total);
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

        int[] scratch = GetScratchCells(q.bm); // neighbor buffer, etc. (no allocs)
        int cellCount = GetCellCount(q.bm);

        // =============================
        // Ability-based actions (unchanged)
        // =============================
        for (int cell = 0; cell < cellCount; cell++)
        {
            int pieceId = GetPieceAt(q.bm, cell);
            if (IsInvalid(q.bm, pieceId)) continue;
            if (GetPieceOwner(q.bm, pieceId) != q.playerId) continue;

            byte actorType = GetPieceType(q.bm, pieceId);
            int slotCount = q.pcs.AbilitySlotCount(actorType);

            for (int slot = 0; slot < slotCount; slot++)
            {
                int abilityId = q.pcs.AbilityIdAtSlot(actorType, slot);
                byte kind = q.pcs.GetAbilityKind(actorType, slot);

                switch (kind)
                {
                    case Move:
                        {
                            int n = GetLegalTargets_Move(q.pcs, abilityId, q.bm, pieceId, scratch);
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
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask);
                            }
                            break;
                        }
                    case Shoot:
                        {
                            int n = GetLegalTargets_Shoot(q.pcs, abilityId, q.bm, pieceId, scratch);
                            for (int i = 0; i < n; i++)
                            {
                                int tgtPid = scratch[i];
                                ushort dst = GetPieceCell(q.bm, tgtPid);
                                var a = new Action
                                {
                                    kind = Shoot,
                                    abilitySlot = (byte)slot,
                                    pieceType = 0,
                                    srcCell = (ushort)cell,
                                    dstCell = dst,
                                    aux = (ushort)tgtPid
                                };
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask);
                            }
                            break;
                        }
                    case CaptureVP:
                        {
                            if (q.pcs.IsLegal_CaptureVP(q.bm, pieceId, abilityId))
                            {
                                ushort vpCell = (ushort)q.bm.GetVictoryPointCellId();
                                var a = new Action
                                {
                                    kind = CaptureVP,
                                    abilitySlot = (byte)slot,
                                    pieceType = 0,
                                    srcCell = (ushort)cell,
                                    dstCell = vpCell,
                                    aux = 0
                                };
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask);
                            }
                            break;
                        }
                    case CoreDamage:
                        {
                            if (q.pcs.IsLegal_CoreDamage(q.bm, pieceId, abilityId))
                            {
                                // Determine which adjacent cell is the enemy core and set dstCell accordingly
                                ushort dstCore = 0;
                                int owner = GetPieceOwner(q.bm, pieceId);
                                int nNbrs = GetNeighbors(q.bm, cell, scratch);
                                for (int i = 0; i < nNbrs; i++)
                                {
                                    int nb = scratch[i];
                                    if (nb < 0) continue;
                                    if (q.bm.IsEnemyCoreCell(nb, owner)) { dstCore = (ushort)nb; break; }
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
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask);
                            }
                            break;
                        }
                    case Create:
                        // NOTE: Create via ability is intentionally ignored in favor of the global Create path below.
                        break;
                    case Push:
                        {
                            int n = GetLegalTargets_Push(q.pcs, abilityId, q.bm, pieceId, scratch);
                            for (int i = 0; i < n; i++)
                            {
                                int tgtPid = scratch[i];
                                ushort dst = GetPieceCell(q.bm, tgtPid);
                                var a = new Action
                                {
                                    kind = Push,
                                    abilitySlot = (byte)slot,
                                    pieceType = 0,
                                    srcCell = (ushort)cell,
                                    dstCell = dst,
                                    aux = (ushort)tgtPid
                                };
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask);
                            }
                            break;
                        }
                    case GroupBuild:
                        {
                            int tgtType = q.pcs.groupBuildTargetType[actorType];
                            if (tgtType >= 0 && tgtType < q.pcs.typeCount)
                            {
                                int require = q.pcs.groupBuildRequireNumber[actorType];
                                if (require > 1)
                                {
                                    // Cluster check
                                    int clusterSize = CountClusterOfType(q.bm, actorType, cell);
                                    if (clusterSize >= require)
                                    {
                                        // Enumerate legal create destinations for target type
                                        EnumerateGroupBuildCreates(q, (byte)tgtType, cell, actorType, ref write, ref total, cap, outActions, outCosts, outMask);
                                    }
                                }
                            }
                            break;
                        }
                    case Upgrade:
                        {
                            if (q.pcs.upgradeEnabled[actorType])
                            {
                                int targetType = q.pcs.upgradeTargetType[actorType];
                                if (targetType >= 0 && targetType < q.pcs.typeCount)
                                {
                                    // Digit/buildable gate for target type
                                    if (q.pcs.IsBuildable((byte)targetType))
                                    {
                                        int reqDigit = q.pcs.GetRequiredDigit((byte)targetType);
                                        if (reqDigit < 0 || q.ps.HasDigit(reqDigit))
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
                                            Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask);
                                        }
                                    }
                                }
                            }
                            break;
                        }
                    case Launcher:
                        {
                            int n = GetLegalTargets_Launcher(q.pcs, abilityId, q.bm, pieceId, scratch);
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
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask);
                            }
                            break;
                        }
                    case Spawner:
                        {
                            EmitSpawnerActions(q, abilityId, pieceId, cell, slot, ref write, ref total, cap, outActions, outCosts, outMask);
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
        bool limitReached = limitActive && q.bm.GetPieceCountForPlayer(q.playerId) >= q.pieceLimitPerPlayer;
        if (!limitReached)
        {
            int coreCell = GetPlayerCoreCellId(q.bm, q.playerId);
            for (int cell = 0; cell < cellCount; cell++)
            {
                if (!IsEmpty(q.bm, cell)) continue; // only empties

                bool legal = (cell == coreCell); // NEW: allow 'on core'
                if (!legal)
                {
                    int nCore = GetNeighbors(q.bm, coreCell, scratch);
                    for (int i = 0; i < nCore; i++) { if (scratch[i] == cell) { legal = true; break; } }
                }

                if (!legal)
                {
                    int nNbrs = GetNeighbors(q.bm, cell, scratch);
                    for (int i = 0; i < nNbrs && !legal; i++)
                    {
                        int nbCell = scratch[i];
                        int nbPid = GetPieceAt(q.bm, nbCell);
                        if (IsInvalid(q.bm, nbPid)) continue;
                        if (GetPieceOwner(q.bm, nbPid) != q.playerId) continue;
                        byte nbType = GetPieceType(q.bm, nbPid);
                        if (IsBuilding(q.pcs, nbType)) legal = true;
                    }
                }

                if (!legal) continue;

                // For each buildable type (default: all types 0..TypeCount-1)
                int typeCount = GetPieceTypeCount(q.pcs);
                for (int t = 0; t < typeCount; t++)
                {
                    if (!IsBuildable(q.pcs, (byte)t)) continue; // buildable gate (CSV flag)
                                                                // digitsRequired gate (Plan-B): skip if requirement exists and player lacks it
                    int req = q.pcs.GetRequiredDigit((byte)t);
                    if (req >= 0 && !q.ps.HasDigit(req)) continue;
                    bool hasConn = q.pcs.HasConnectors((byte)t);
                    ulong allowedMask = hasConn ? q.pcs.connectorAllowedMasks[t] : 0UL;
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
                        Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask);
                    }
                    else
                    {
                        for (int cfg = 0; cfg < 64; cfg++)
                        {
                            if ((allowedMask & (1UL << cfg)) == 0) continue;
                            if (!PiecesSides.IsConnectorPlacementLegal(q.bm, q.pcs, cell, (byte)t, cfg, q.playerId))
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
                            Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask);
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
    private void Emit(
        ref Action a,
        ref int write,
        ref int total,
        int cap,
        Span<Action> outActions,
        in OfferQuery q,
        Span<float> outCosts,
        Span<byte>  outMask)
    {
        total++;
        if (write < cap)
        {
            outActions[write] = a;
            WriteCostMask(a, q, outCosts, outMask, write);
            write++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteCostMask(in Action a, in OfferQuery q, Span<float> outCosts, Span<byte> outMask, int idx)
    {
    // EndTurn is always free & affordable (never masked out by cost)
    if (a.kind == ActionKind.EndTurn)
    {
        outCosts[idx] = 0f;
        outMask[idx]  = 1;
        return;
    }
        if (q.cost == null) { outCosts[idx] = 0f; outMask[idx] = 1; return; }

        float quoted;
        if (q.cost.IsAffordable(q.ps, a, q.bm, q.pcs, out quoted))
        { outCosts[idx] = quoted; outMask[idx] = 1; }
        else { outCosts[idx] = quoted; outMask[idx] = 0; }
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZeroTail(int write, Span<float> outCosts, Span<byte> outMask)
    {
        for (int i = write; i < outCosts.Length; i++) outCosts[i] = 0f;
        for (int i = write; i < outMask.Length;  i++) outMask[i]  = 0;
    }

    // ---- BoardModel adapters (1-liners; edit here to match your API names if needed) ----
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetCellCount(BoardModel bm) => bm.GetCellCount();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetPieceAt(BoardModel bm, int cell) => bm.GetCellOccupant(cell); // -1 if empty

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte GetPieceOwner(BoardModel bm, int pieceId) => (byte)bm.GetPieceOwner(pieceId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte GetPieceType(BoardModel bm, int pieceId) => bm.GetPieceType(pieceId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort GetPieceCell(BoardModel bm, int pieceId) => (ushort)bm.GetPieceCell(pieceId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int[] GetScratchCells(BoardModel bm) => bm.GetScratchCellBuffer();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsEmpty(BoardModel bm, int cell) => bm.IsEmpty(cell);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInvalid(BoardModel bm, int pieceId) => pieceId == bm.InvalidId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetNeighbors(BoardModel bm, int cell, int[] outCells) => bm.GetNeighbors(cell, outCells);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetPlayerCoreCellId(BoardModel bm, int playerId) => bm.GetPlayerCoreCellId(playerId);

    // ---- Pieces adapters ----
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetPieceTypeCount(Pieces pcs) => pcs.typeCount; // or pcs.GetPieceTypeCount(); adjust here if needed

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBuilding(Pieces pcs, byte type) => pcs.IsBuilding(type); // adapter; ensure Pieces exposes this

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBuildable(Pieces pcs, byte type) => pcs.IsBuildable(type);

    // ---- Kernel adapters (ability-based kinds) ----
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetLegalTargets_Move(Pieces pcs, int abilityId, BoardModel bm, int actorPid, int[] outCells)
        => pcs.GetLegalTargets_Move(bm, actorPid, abilityId, outCells);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetLegalTargets_Shoot(Pieces pcs, int abilityId, BoardModel bm, int actorPid, int[] outPieceIds)
        => pcs.GetLegalTargets_Shoot(bm, actorPid, abilityId, outPieceIds);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetLegalTargets_Push(Pieces pcs, int abilityId, BoardModel bm, int actorPid, int[] outPieceIds)
    {
        // Inline minimal legality similar to GameActions.GetLegalTargets_Push
        int originCell = GetPieceCell(bm, actorPid);
        if (originCell < 0) return 0;
        if (abilityId < 0 ||
            pcs.push_TargetsBuildings == null || pcs.push_TargetsSoldiers == null ||
            pcs.push_rangeMax == null || pcs.push_FriendlyFire == null ||
            abilityId >= pcs.push_TargetsBuildings.Length ||
            abilityId >= pcs.push_TargetsSoldiers.Length ||
            abilityId >= pcs.push_rangeMax.Length ||
            abilityId >= pcs.push_FriendlyFire.Length)
            return 0;

        int actorOwner = GetPieceOwner(bm, actorPid);

        bool allowBuildings = pcs.push_TargetsBuildings[abilityId];
        bool allowSoldiers = pcs.push_TargetsSoldiers[abilityId];
        int rangeMax = pcs.push_rangeMax[abilityId];
        bool allowFriendly = pcs.push_FriendlyFire[abilityId];

        int cap = outPieceIds != null ? outPieceIds.Length : 0;
        int count = 0;
        int cellCount = GetCellCount(bm);

        for (int c = 0; c < cellCount; c++)
        {
            int pid = GetPieceAt(bm, c);
            if (pid < 0) continue;

            if (!allowFriendly && GetPieceOwner(bm, pid) == actorOwner) continue;

            byte type = GetPieceType(bm, pid);
            bool isBuilding = IsBuilding(pcs, type);
            if (isBuilding && !allowBuildings) continue;
            if (!isBuilding && !allowSoldiers) continue;

            int dist = bm.Distance(originCell, c);
            if (dist < 1 || dist > rangeMax) continue;
            if (!bm.LineOfSightClear(originCell, c)) continue;

            int pushDest = ComputePushDestination(bm, pcs, actorPid, pid, abilityId);
            if (pushDest < 0 || !bm.IsValidCellId(pushDest)) continue;

            if (count < cap) outPieceIds[count] = pid;
            count++;
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetLegalTargets_Launcher(Pieces pcs, int abilityId, BoardModel bm, int actorPid, int[] outPairs)
    {
        if (abilityId < 0 ||
            pcs.launcher_inputRange == null || pcs.launcher_outputRange == null ||
            pcs.launcher_friendlyFire == null || pcs.launcher_enemyFire == null)
            return 0;

        int inputRange = pcs.launcher_inputRange[abilityId];
        int outputRange = pcs.launcher_outputRange[abilityId];
        bool allowFriendly = pcs.launcher_friendlyFire[abilityId];
        bool allowEnemy = pcs.launcher_enemyFire[abilityId];

        int originCell = GetPieceCell(bm, actorPid);
        if (originCell < 0) return 0;

        int cap = outPairs != null ? outPairs.Length : 0;
        int write = 0;

        int cellCount = GetCellCount(bm);
        int actorOwner = GetPieceOwner(bm, actorPid);

        // Find candidate pieces
        for (int c = 0; c < cellCount; c++)
        {
            int pid = GetPieceAt(bm, c);
            if (pid < 0) continue;
            byte owner = GetPieceOwner(bm, pid);
            if (owner == actorOwner && !allowFriendly) continue;
            if (owner != actorOwner && !allowEnemy) continue;

            int distIn = bm.Distance(originCell, c);
            if (distIn < 1 || distIn > inputRange) continue;
            if (!bm.LineOfSightClear(originCell, c)) continue;

            // For each candidate destination within outputRange from launcher
            for (int dst = 0; dst < cellCount; dst++)
            {
                if (!bm.IsEmpty(dst)) continue;
                int distOut = bm.Distance(originCell, dst);
                if (distOut < 1 || distOut > outputRange) continue;
                if (!bm.LineOfSightClear(originCell, dst)) continue;

                if (write + 1 < cap)
                {
                    outPairs[write] = pid;
                    outPairs[write + 1] = dst;
                }
                write += 2;
            }
        }

        return write; // count of ints (pairs pid,dst)
    }

    private void EmitSpawnerActions(
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
        Span<byte> outMask)
    {
        if (abilityId < 0 ||
            q.pcs.spawn_pieceAmount == null || q.pcs.spawn_range == null || q.pcs.spawn_targetType == null || q.pcs.spawn_onlyOncePerTurn == null)
            return;

        int amount = q.pcs.spawn_pieceAmount[abilityId];
        int range = q.pcs.spawn_range[abilityId];
        int targetType = q.pcs.spawn_targetType[abilityId];
        bool once = q.pcs.spawn_onlyOncePerTurn[abilityId];
        if (amount <= 0 || targetType < 0 || targetType >= q.pcs.typeCount) return;

        // Digit gate; buildable override allowed
        int reqDigit = q.pcs.GetRequiredDigit((byte)targetType);
        if (reqDigit >= 0 && !q.ps.HasDigit(reqDigit)) return;

        // Collect empty, LOS-valid cells within range from launcher
        int[] empties = q.bm.GetScratchCellBuffer();
        int eCount = 0;
        int cellCount = GetCellCount(q.bm);
        for (int c = 0; c < cellCount; c++)
        {
            if (!IsEmpty(q.bm, c)) continue;
            int dist = q.bm.Distance(actorCell, c);
            if (dist < 1 || dist > range) continue;
            if (!q.bm.LineOfSightClear(actorCell, c)) continue;
            empties[eCount++] = c;
        }
        if (eCount <= 0) return;
        Array.Sort(empties, 0, eCount);

        // Piece limit: allow as many as possible
        int availableLimit = int.MaxValue;
        if (q.pieceLimitEnabled && q.pieceLimitPerPlayer > 0)
            availableLimit = q.pieceLimitPerPlayer - q.bm.GetPieceCountForPlayer(q.playerId);
        int possible = Math.Min(amount, Math.Min(eCount, Math.Max(0, availableLimit)));
        if (possible <= 0) return;

        // Once-per-turn flag cannot be observed here; Perform will reject if already used.

        var a = new Action
        {
            kind = Spawner,
            abilitySlot = (byte)abilitySlot,
            pieceType = (byte)targetType,
            srcCell = (ushort)actorCell,
            dstCell = (ushort)empties[0],
            aux = 0
        };
        Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask);
    }

    private void EmitMultiCreatePlacements(
        in OfferQuery q,
        Span<Action> outActions,
        Span<float> outCosts,
        Span<byte> outMask,
        ref int write,
        ref int total)
    {
        int cap = outActions.Length;
        int type = q.multiCreateType;
        int remaining = q.multiCreateRemaining;
        if (remaining <= 0) { ZeroTail(write, outCosts, outMask); return; }
        var bm = q.bm;
        var pcs = q.pcs;
        int cellCount = GetCellCount(bm);
        var placed = q.multiCreateCells;
        int placedCount = q.multiCreateCellCount;

        for (int cell = 0; cell < cellCount; cell++)
        {
            if (!IsEmpty(bm, cell)) continue;
            if (q.pieceLimitEnabled && q.pieceLimitPerPlayer > 0 &&
                bm.GetPieceCountForPlayer(q.playerId) + (write + 1) > q.pieceLimitPerPlayer)
                break;

            if (q.multiCreateBorder && placedCount > 0)
            {
                bool adjacent = false;
                int[] neigh = GetScratchCells(bm);
                int n = GetNeighbors(bm, cell, neigh);
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
                if (!IsCreateGeometryLegal(bm, pcs, cell, q.playerId)) continue;
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
            Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask);
            if (write >= remaining) break;
        }
    }

    // ---- Push helpers (local copy of GameActions.ComputePushDestination) ----
    private static int ComputePushDestination(
        BoardModel bm,
        Pieces pcs,
        int actorPieceId,
        int targetPieceId,
        int abilityId)
    {
        int actorCell = GetPieceCell(bm, actorPieceId);
        int targetCell = GetPieceCell(bm, targetPieceId);

        if (actorCell < 0 || targetCell < 0)
            return bm.InvalidId;

        if (abilityId < 0 ||
            pcs.push_PushAmount == null || pcs.push_pull == null ||
            abilityId >= pcs.push_PushAmount.Length || abilityId >= pcs.push_pull.Length)
            return bm.InvalidId;

        int pushAmount = pcs.push_PushAmount[abilityId];
        if (pushAmount <= 0)
            return bm.InvalidId;

        bool isPull = pcs.push_pull[abilityId];

        int dir = isPull
            ? bm.GetDirectionIndex(targetCell, actorCell)
            : bm.GetDirectionIndex(actorCell, targetCell);
        if (dir < 0)
            return bm.InvalidId;

        int targetOwner = GetPieceOwner(bm, targetPieceId);
        int ownerCoreCell = GetPlayerCoreCellId(bm, (byte)targetOwner);

        const int MaxRingCells = 256;
        Span<int> ringCells = stackalloc int[MaxRingCells];
        Span<int> farCells = stackalloc int[MaxRingCells];

        for (int dist = pushAmount; dist > 0; dist--)
        {
            int totalOnRing = bm.cellIdsRingAroundCell(
                originCell: targetCell,
                ringSize: dist,
                requireEmpty: true,
                outCells: ringCells);

            if (totalOnRing <= 0)
                continue;

            int ringCount = Math.Min(totalOnRing, MaxRingCells);

            int extremeDistFromActor = isPull ? int.MaxValue : -1;
            for (int i = 0; i < ringCount; i++)
            {
                int cell = ringCells[i];
                int dAct = bm.Distance(actorCell, cell);
                if (isPull)
                {
                    if (dAct < extremeDistFromActor)
                        extremeDistFromActor = dAct;
                }
                else
                {
                    if (dAct > extremeDistFromActor)
                        extremeDistFromActor = dAct;
                }
            }
            if ((!isPull && extremeDistFromActor < 0) || (isPull && extremeDistFromActor == int.MaxValue))
                continue;

            int farCount = 0;
            for (int i = 0; i < ringCount; i++)
            {
                int cell = ringCells[i];
                int dAct = bm.Distance(actorCell, cell);
                if (dAct == extremeDistFromActor)
                    farCells[farCount++] = cell;
            }
            if (farCount == 0)
                continue;

            int idealBehindCell = bm.StepInDirection(targetCell, dir, dist);

            if (idealBehindCell >= 0 && bm.IsValidCellId(idealBehindCell) && bm.IsEmpty(idealBehindCell))
            {
                for (int i = 0; i < farCount; i++)
                {
                    if (farCells[i] == idealBehindCell)
                        return idealBehindCell;
                }
            }

            int bestCell = bm.InvalidId;
            int bestScore = int.MaxValue;

            for (int i = 0; i < farCount; i++)
            {
                int cell = farCells[i];

                if (!bm.IsValidCellId(cell) || !bm.IsEmpty(cell))
                    continue;

                int dCore = ownerCoreCell >= 0 ? bm.Distance(cell, ownerCoreCell) : 0;
                if (dCore < bestScore)
                {
                    bestScore = dCore;
                    bestCell = cell;
                }
            }

            if (bestCell >= 0)
                return bestCell;
        }

        return bm.InvalidId;
    }

    // ---- GroupBuild helpers ----
    private static int CountClusterOfType(BoardModel bm, byte type, int startCell)
    {
        if (startCell < 0) return 0;
        var visited = bm.GetScratchCellBuffer();
        Array.Clear(visited, 0, visited.Length);
        int[] queue = bm.GetScratchCellBuffer();
        int head = 0, tail = 0;
        queue[tail++] = startCell;
        visited[startCell] = 1;
        int count = 0;
        while (head < tail)
        {
            int cell = queue[head++];
            int pid = GetPieceAt(bm, cell);
            if (pid >= 0 && GetPieceType(bm, pid) == type) count++;
            int[] neigh = bm.GetScratchNeighborBuffer();
            int n = bm.GetNeighbors(cell, neigh);
            for (int i = 0; i < n; i++)
            {
                int nb = neigh[i];
                if (nb < 0 || nb >= visited.Length) continue;
                if (visited[nb] != 0) continue;
                int nbPid = GetPieceAt(bm, nb);
                if (nbPid < 0 || GetPieceType(bm, nbPid) != type) continue;
                visited[nb] = 1;
                queue[tail++] = nb;
            }
        }
        return count;
    }

    private void EnumerateGroupBuildCreates(
        in OfferQuery q,
        byte targetType,
        int clusterRepresentativeCell,
        byte actorType,
        ref int write,
        ref int total,
        int cap,
        Span<Action> outActions,
        Span<float> outCosts,
        Span<byte> outMask)
    {
        int cellCount = GetCellCount(q.bm);
        for (int cell = 0; cell < cellCount; cell++)
        {
            if (!IsEmpty(q.bm, cell)) continue;
            if (!IsCreateGeometryLegal(q.bm, q.pcs, cell, q.playerId)) continue;
            int reqDigit = q.pcs.GetRequiredDigit(targetType);
            if (reqDigit >= 0 && !q.ps.HasDigit(reqDigit)) continue;
            if (!q.pcs.IsBuildable(targetType)) continue;
            var a = new Action
            {
                kind = GroupBuild,
                abilitySlot = 0,
                pieceType = targetType,
                srcCell = (ushort)clusterRepresentativeCell,
                dstCell = (ushort)cell,
                aux = 0
            };
            Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask);
        }
    }

    private static bool IsCreateGeometryLegal(BoardModel bm, Pieces pcs, int cell, byte player)
    {
        if (!bm.IsEmpty(cell)) return false;
        int core = GetPlayerCoreCellId(bm, player);
        if (cell == core) return true;
        var scratch = GetScratchCells(bm);
        int n = GetNeighbors(bm, core, scratch);
        for (int i = 0; i < n; i++) if (scratch[i] == cell) return true;
        int n2 = GetNeighbors(bm, cell, scratch);
        for (int i = 0; i < n2; i++)
        {
            int nb = scratch[i];
            int pid = GetPieceAt(bm, nb);
            if (IsInvalid(bm, pid)) continue;
            if (GetPieceOwner(bm, pid) != player) continue;
            byte t = GetPieceType(bm, pid);
            if (IsBuilding(pcs, t)) return true;
        }
        return false;
    }
}
