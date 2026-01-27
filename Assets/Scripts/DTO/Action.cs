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
        public int kind;        // 0..6

        /// <summary>
        /// For Create: the type to build (casted to byte by OfferProvider).
        /// Otherwise 0.
        /// </summary>
        public int ActorsCell;

        /// <summary>
        /// Actor’s cell id.
        /// For EndTurn, use 0xFFFF sentinel.
        /// used to src
        /// </summary>
        public int TargetCell;

        /// <summary>
        /// Target cell id:
        /// Move/Create/Shoot → destination; CaptureVP → VP cell; CoreDamage/EndTurn → 0 (or core later).
        /// </summary>
        /// used to dst
        public int TargetType;

        /// <summary>
        /// Auxiliary ID when needed.
        /// For Shoot: targetPieceId; otherwise 0.
        /// </summary>
        public ushort WallConfig;
        public int intakeCell;

        public int[] addCost;

        /// <summary>Convenience constructor (optional).</summary>
        public Action(int kind, int ActorsCell, int TargetCell = -1, int TargetType = -1, ushort WallConfig = 0, int intakeCell = -1, int[] addCost = null)
        {
            this.kind = kind;
            this.ActorsCell = ActorsCell;
            this.TargetCell = TargetCell;
            this.TargetType = TargetType;
            this.WallConfig = WallConfig; //wall of 6 options
            this.intakeCell = intakeCell; 
            this.addCost = addCost ?? Array.Empty<int>();
        }

        // public override string ToString()
            // => $"Action(kind={kind}, type={pieceType}, src={ActorsCell}, dst={TargetCell}, aux={aux})";
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
        public const byte Push = 4;
        public const byte GroupBuild = 5;
        public const byte Upgrade = 6;
        public const byte Launcher = 7;
        public const byte Spawner = 8;
        public const byte SacrificeFactory = 9; 
        public const byte ConversionFactory = 10;
        public const byte Explosive = 11;
        public const byte Sniper = 12;
        public const byte NecroSpawn = 13;
        public const byte Create = 14;
        public const byte EndTurn = 15;
    }
}
