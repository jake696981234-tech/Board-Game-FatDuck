// Assets/Scripts/Core/PlayerState.cs
// Phase A: per-player runtime state (mutable). Zero-alloc helpers only.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Game.Core
{
    /// <summary>
    /// Per-player runtime state used by OfferProvider/CostEngine/GameState/PlayerAgent.
    /// Keep this small and stable; pure data + tiny inlined helpers.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PlayerState
    {

        // --- Digit inventory (0..9). Ref-count so multiple pieces can grant the same digit.
        public const int MAX_DIGITS = 10;
        public byte[] digitRefCount; // allocate at init

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasDigit(int d) => (uint)d < MAX_DIGITS && digitRefCount != null && digitRefCount[d] > 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GrantDigit(int d)
        {
            if ((uint)d < MAX_DIGITS)
            {
                if (digitRefCount == null) digitRefCount = new byte[MAX_DIGITS];
                if (digitRefCount[d] < byte.MaxValue) digitRefCount[d]++;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RevokeDigit(int d)
        {
            if ((uint)d < MAX_DIGITS && digitRefCount != null && digitRefCount[d] > 0)
                digitRefCount[d]--;
        }


        
        public string name;
        public bool isAI;
        public int team;
        public int perRoundPieceKillCount;
        
        public bool applyBotSurcharges;
        /// <summary>Spendable currency for actions/builds.</summary>
        public bool endedWithoutActionThisCycle;
        public bool applyStartOfTurnBudgetDecrease;
        public float budget;

        /// <summary>Number of actions already taken this turn (0 => next action is free).</summary>
        public byte actionIndexThisTurn;

        /// <summary>Turn-cap flag: has captured VP this turn.</summary>
        public bool didCaptureVP;

        /// <summary>Turn-cap flag: has damaged a core this turn.</summary>
        public bool didCoreDamage;

        /// <summary>Count of VPs gained during the current round (paid out at end of round, then reset).</summary>
        public byte vpGainedThisRound;

        /// <summary>Count of core hits during the current round (paid out at end of round, then reset).</summary>
        public byte coreHitsThisRound;

        /// <summary>Total VPs across the match (never resets until endgame/elimination).</summary>
        public int vpTotal;

        /// <summary>Elimination flag: true if this player is out (e.g., core <= 0).</summary>
        public bool isEliminated;

        // ---------- Tiny, inlined helpers (no allocations) ----------

        /// <summary>Reset per-turn counters at the beginning of the player's turn.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginTurnReset()
        {
            actionIndexThisTurn = 0;
            didCaptureVP = false;
            didCoreDamage = false;
        }

        /// <summary>Clear round counters after end-of-round payouts are applied.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearRoundCounters()
        {
            vpGainedThisRound = 0;
            coreHitsThisRound = 0;
            perRoundPieceKillCount = 0;
        }

        /// <summary>Record a successful VP capture (used by reducer/CostEngine.Spend).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnCaptureVP()
        {
            didCaptureVP = true;
            vpGainedThisRound++;
            vpTotal++;
        }

        /// <summary>Record a successful core damage (used by reducer/CostEngine.Spend).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnCoreDamage()
        {
            didCoreDamage = true;
            coreHitsThisRound++;
        }

        /// <summary>Increment action index after a successfully applied action.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AdvanceActionIndex()
        {
            actionIndexThisTurn++;
        }

        /// <summary>Add (or subtract) budget and clamp to [0, +inf).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddBudget(float delta)
        {
            budget += delta;
            if (budget < 0f) budget = 0f;
        }
    }
}
