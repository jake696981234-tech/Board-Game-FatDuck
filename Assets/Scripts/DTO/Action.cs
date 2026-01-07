// Assets/Scripts/Core/Action.cs
// Phase A: compact DTO describing one selectable action.
// Produced by OfferProvider; consumed by GameState/CostEngine/PlayerAgent.
// No allocations, no Unity refs, ID-only.

using System;
using System.Runtime.InteropServices;

namespace Game.Core
{
    /// <summary>
    /// Flat, ID-only record of one legal action.
    /// Layout is intentionally compact (12 bytes) for ML pipelines.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Action
    {
        /// <summary>Action family. See <see cref=""/> for stable codes.</summary>
        public byte kind;        // 0..6

        /// <summary>
        /// For Create: the type to build (casted to byte by OfferProvider).
        /// Otherwise 0.
        /// </summary>
        public byte pieceType;

        /// <summary>
        /// Actor’s cell id.
        /// For EndTurn, use 0xFFFF sentinel.
        /// used to src
        /// </summary>
        public ushort ActorsCellId;

        /// <summary>
        /// Target cell id:
        /// Move/Create/Shoot → destination; CaptureVP → VP cell; CoreDamage/EndTurn → 0 (or core later).
        /// </summary>
        /// used to dst
        public ushort TargetCellId;

        /// <summary>
        /// Auxiliary ID when needed.
        /// For Shoot: targetPieceId; otherwise 0.
        /// </summary>
        public ushort aux;

        public int[] addCost;

        /// <summary>Convenience constructor (optional).</summary>
        public Action(byte kind, byte pieceType, ushort actorsCellId, ushort targetCellId, ushort aux = 0, int[] addCost = null)
        {
            this.kind = kind;
            this.pieceType = pieceType;
            this.ActorsCellId = actorsCellId;
            this.TargetCellId = targetCellId;
            this.aux = aux; //wall of 6 options
            this.addCost = addCost ?? Array.Empty<int>();
        }

        public override string ToString()
            => $"Action(kind={kind}, type={pieceType}, src={ActorsCellId}, dst={TargetCellId}, aux={aux})";
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
        public const byte Push = 6;
        public const byte GroupBuild = 7;
        public const byte Upgrade = 8;
        public const byte Launcher = 9; //frog
        public const byte Spawner = 10; //frog and penguin
        public const byte SacrificeFactory = 11;
        public const byte ConversionFactory = 12;
        public const byte Explosive = 13;
        public const byte PieceBuild = 14;
        public const byte Sniper = 15;
        public const byte NecroSpawn = 16;
    }
}
