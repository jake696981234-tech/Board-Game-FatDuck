// Assets/Scripts/Core/OfferQuery.cs
// Phase A: tiny input bundle for OfferProvider. No Unity refs.

using System;

namespace Game.Core
{
    /// <summary>
    /// Immutable bundle of inputs for OfferProvider to enumerate actions
    /// for the current active player. Keep this contract stable.
    /// </summary>
    public readonly struct OfferQuery
    {
        /// <summary>World snapshot and geometry helpers (neighbors, LOS, BFS, scratch).</summary>
        public readonly BoardModel bm;

        /// <summary>Ability metadata + structural legality kernels.</summary>
        public readonly Pieces pcs;

        /// <summary>Active player's state (budget, per-turn flags, etc.).</summary>
        public readonly PlayerState ps;

        /// <summary>Active player id (0..MaxPlayers-1).</summary>
        public readonly byte playerId;

        /// <summary>
        /// Pricing/masking engine. May be null in structural-only mode.
        /// When null, OfferProvider should set cost=0 and mask=1 for structurally legal actions.
        /// </summary>
        public readonly CostEngine cost;

        public readonly bool pieceLimitEnabled;
        public readonly int pieceLimitPerPlayer;
        public readonly bool multiCreateActive;
        public readonly byte multiCreateType;
        public readonly bool multiCreateBorder;
        public readonly int multiCreateRemaining;
        public readonly int[] multiCreateCells; // snapshot of already placed cells
        public readonly int multiCreateCellCount;

        public OfferQuery(BoardModel bm, Pieces pcs, PlayerState ps, byte playerId, CostEngine cost, bool pieceLimitEnabled, int pieceLimitPerPlayer,
            bool multiCreateActive = false, byte multiCreateType = 0, bool multiCreateBorder = false, int multiCreateRemaining = 0, int[] multiCreateCells = null, int multiCreateCellCount = 0)
        {
            this.bm       = bm;
            this.pcs      = pcs;
            this.ps       = ps;
            this.playerId = playerId;
            this.cost     = cost;
            this.pieceLimitEnabled = pieceLimitEnabled;
            this.pieceLimitPerPlayer = pieceLimitPerPlayer;
            this.multiCreateActive = multiCreateActive;
            this.multiCreateType = multiCreateType;
            this.multiCreateBorder = multiCreateBorder;
            this.multiCreateRemaining = multiCreateRemaining;
            this.multiCreateCells = multiCreateCells;
            this.multiCreateCellCount = multiCreateCellCount;
        }
    }
}
