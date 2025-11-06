// Assets/Scripts/Core/Action.cs
// Phase A: compact DTO describing one selectable action.
// Produced by OfferProvider; consumed by GameState/CostEngine/PlayerAgent.
// No allocations, no Unity refs, ID-only.

using System;
using System.Runtime.InteropServices;

namespace Game.Core
{
    ///edfweadawwdwewqdwqdwqdqwdwqdwqfwd
    /// <summary>
    /// Flat, ID-only record of one legal action.
    /// Layout is intentionally compact (12 bytes) for ML pipelines.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Action
    {
        /// <summary>Action family. See <see cref=""/> for stable codes.</summary>
        public byte kind;        // 0..5

        /// <summary>Ability slot index on the actor's piece type (0..MaxSlots-1). 0 for EndTurn.</summary>
        public byte abilitySlot;

        /// <summary>
        /// For Create: the type to build (casted to byte by OfferProvider).
        /// Otherwise 0.
        /// </summary>
        public byte pieceType;

        /// <summary>
        /// Actor’s cell id.
        /// For EndTurn, use 0xFFFF sentinel.
        /// </summary>
        public ushort srcCell;

        /// <summary>
        /// Target cell id:
        /// Move/Create/Shoot → destination; CaptureVP → VP cell; CoreDamage/EndTurn → 0 (or core later).
        /// </summary>
        public ushort dstCell;

        /// <summary>
        /// Auxiliary ID when needed.
        /// For Shoot: targetPieceId; otherwise 0.
        /// </summary>
        public ushort aux;

        /// <summary>Convenience constructor (optional).</summary>
        public Action(byte kind, byte slot, byte pieceType, ushort src, ushort dst, ushort aux = 0)
        {
            this.kind = kind;
            this.abilitySlot = slot;
            this.pieceType = pieceType;
            this.srcCell = src;
            this.dstCell = dst;
            this.aux = aux;
        }

        public override string ToString()
            => $"Action(kind={kind}, slot={abilitySlot}, type={pieceType}, src={srcCell}, dst={dstCell}, aux={aux})";
    }

    /// <summary>
    /// Stable byte codes for <see cref="Action.kind"/>. Keep these frozen for Phase A.
    /// </summary>
    public static class ActionKind
    {
        public const byte Move = 0;
        public const byte Shoot = 1;
        public const byte CaptureVP = 2;
        public const byte CoreDamage = 3;
        public const byte Create = 4;
        public const byte EndTurn = 5;
    }
}
