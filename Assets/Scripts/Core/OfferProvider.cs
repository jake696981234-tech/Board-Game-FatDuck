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
                                var a = new Action
                                {
                                    kind = CoreDamage,
                                    abilitySlot = (byte)slot,
                                    pieceType = 0,
                                    srcCell = (ushort)cell,
                                    dstCell = 0,
                                    aux = 0
                                };
                                Emit(ref a, ref write, ref total, cap, outActions, q, outCosts, outMask);
                            }
                            break;
                        }
                    case Create:
                        // NOTE: Create via ability is intentionally ignored in favor of the global Create path below.
                        break;
                    default:
                        break;
                }
            }
        }

        // =============================
        // Global Create actions (decoupled from abilities)
        // Determinism: cells↑ then pieceType↑
        // =============================
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
}
