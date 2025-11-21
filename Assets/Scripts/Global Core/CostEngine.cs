using System;
using Game.Core;
using Action = Game.Core.Action; // avoid System.Action clash

/// <summary>
/// CostEngine (pricing-only)
/// - Pure, allocation-free reads for quoting and affordability checks.
/// - <b>No</b> mutations: budget, indices, flags/VP are owned by GameState reducers.
/// - Uses integer pricing; turn fee follows geometric growth with first action free.
/// </summary>
public sealed class CostEngine
{
    private readonly int baseActionCost;
    private readonly float actionGrowthFactor;

    public CostEngine(in GameConfigHub hub)
    {
        this.baseActionCost = hub.cost_baseActionCost;
        this.actionGrowthFactor = hub.cost_actionGrowthFactor;
    }

    /// <summary>
    /// Pure read: return the deterministic price of taking <paramref name="a"/> in the given context.
    /// Quote = TurnFee(k) + AbilityCost + BuildCost, where k = cur.actionIndexThisTurn.
    /// No side effects. No allocations.
    /// </summary>
    public int Quote(in PlayerState cur, in Action a, in BoardModel b, in Pieces pcs)
    {
        // Turn fee: 0 for the first action; then geometric progression from config.
        int k = cur.actionIndexThisTurn; // before taking this action
        int turnFee = (k == 0) ? 0 : RoundToInt(baseActionCost * MathF.Pow(actionGrowthFactor, k - 1));

        // Resolve ability once. EndTurn has no ability.
        int abilityId = ResolveAbilityId(a, b, pcs);

        // Ability surcharge: none for EndTurn/invalid.
        int abilityCost = 0;
        if (a.kind != ActionKind.EndTurn && a.kind != ActionKind.Create && abilityId >= 0)
        {
            abilityCost = pcs.AbilitySurcharge(abilityId, applyBotSurcharges: cur.applyBotSurcharges);
        }


        // Build cost: only for Create actions.
        int buildCost = 0;
        if (a.kind == ActionKind.Create)
        {
            buildCost = pcs.GetBuildCost(a.pieceType); // new accessor on Pieces
        }


        return turnFee + abilityCost + buildCost;
    }

    /// <summary>
    /// Pure read: compute <paramref name="quoted"/> (including turn fee) and check budget + per-turn caps.
    /// EndTurn is always affordable with quoted = 0. No side effects.
    /// </summary>
    public bool IsAffordable(in PlayerState cur, in Action a, in BoardModel b, in Pieces pcs, out float quoted)
    {
        // EndTurn is always free & affordable (never blocked)
        if (a.kind == ActionKind.EndTurn)
        {
            quoted = 0;
            return true;
        }

        if (a.kind == ActionKind.EndTurn)
        {
            quoted = 0;
            return true;
        }

        quoted = Quote(cur, a, b, pcs);
        if (cur.budget < quoted) return false;

        // Once-per-turn gates (read-only caps)
        if (a.kind == ActionKind.CaptureVP && cur.didCaptureVP) return false;
        if (a.kind == ActionKind.CoreDamage && cur.didCoreDamage) return false;

        return true;
    }

    // -------------------- Internals --------------------
    /// <summary>
    /// Resolve abilityId from the action's (srcCell → pieceId → type) + abilitySlot.
    /// Returns -1 for EndTurn or if any part of the chain is invalid. No allocations.
    /// </summary>
    public static int ResolveAbilityId(in Action a, in BoardModel b, in Pieces pcs)
    {
        if (a.kind == ActionKind.EndTurn) return -1;
        if (a.srcCell == (ushort)0xFFFF) return -1; // per Action.cs contract

        int pieceId = b.GetCellOccupant(a.srcCell);
        if (pieceId < 0) return -1;

        byte type = (byte)b.GetPieceType(pieceId); // explicit cast for BM APIs that return int
        return pcs.AbilityIdAtSlot(type, a.abilitySlot);
    }

    private static int RoundToInt(float value)
    {
        // Deterministic rounding for pricing (midpoint away from zero)
        return (int)MathF.Round(value, MidpointRounding.AwayFromZero);
    }


    // ---- NEW: structured breakdown ----
    public readonly struct CostBreakdown
    {
        public readonly int TurnFee;       // geometric per-turn fee
        public readonly int AbilityCost;   // surcharge from ability metadata
        public readonly int BuildCost;     // create/build cost
        public readonly int Total;         // TurnFee + AbilityCost + BuildCost
        public CostBreakdown(int tf, int ac, int bc)
        { TurnFee = tf; AbilityCost = ac; BuildCost = bc; Total = tf + ac + bc; }
    }

    public CostBreakdown QuoteBreakdown(in PlayerState cur, in Action a, in BoardModel b, in Pieces pcs)
    {
        // Turn fee
        int k = cur.actionIndexThisTurn;
        int turnFee = (k == 0) ? 0 : RoundToInt(baseActionCost * MathF.Pow(actionGrowthFactor, k - 1));
        // Ability surcharge (non-EndTurn/Create)
        int abilityCost = 0;
        int abilityId = ResolveAbilityId(a, b, pcs);
        if (a.kind != ActionKind.EndTurn && a.kind != ActionKind.Create && abilityId >= 0)
            abilityCost = pcs.AbilitySurcharge(abilityId, applyBotSurcharges: cur.applyBotSurcharges);
        // Build cost (Create only)
        int buildCost = (a.kind == ActionKind.Create) ? pcs.GetBuildCost(a.pieceType) : 0;
        return new CostBreakdown(turnFee, abilityCost, buildCost);
    }




    // ---- NEW: overload that also returns the breakdown (non-breaking addition) ----
    public bool IsAffordable(in PlayerState cur, in Action a, in BoardModel b, in Pieces pcs, out CostBreakdown breakdown)
    {
        if (a.kind == ActionKind.EndTurn)
        {
            breakdown = new CostBreakdown(0, 0, 0);
            return true;
        }
        breakdown = QuoteBreakdown(cur, a, b, pcs);
        if (cur.budget < breakdown.Total) return false;
        if (a.kind == ActionKind.CaptureVP && cur.didCaptureVP) return false;
        if (a.kind == ActionKind.CoreDamage && cur.didCoreDamage) return false;
        return true;
    }















}

