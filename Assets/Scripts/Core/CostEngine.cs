using System;
using Game.Core;
using Action = Game.Core.Action; // avoid System.Action clash
using static Game.Core.ActionKind; // import enum values
/// <summary>
/// CostEngine (pricing-only)
/// - Pure, allocation-free reads for quoting and affordability checks.
/// - <b>No</b> mutations: budget, indices, flags/VP are owned by GameState reducers.
/// - Uses integer pricing; turn fee follows geometric growth with first action free.
/// </summary>
public static class CostEngine
{
    public static int baseActionCost;
    public static float actionGrowthFactor;


    /// <summary>
    /// Pure read: return the deterministic price of taking <paramref name="a"/> in the given context.
    /// Quote = TurnFee(k) + AbilityCost + BuildCost, where k = cur.actionIndexThisTurn.
    /// No side effects. No allocations.
    /// </summary>
    public static int Quote(in Action a, int gameIndex, int player)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;


        // Turn fee: 0 for the first action; then geometric progression from config.
        int k = gameState.ps[player].actionIndexThisTurn; // before taking this action
        int turnFee = (k == 0) ? 0 : RoundToInt(baseActionCost * MathF.Pow(actionGrowthFactor, k - 1));


        // Ability surcharge: none for EndTurn/invalid.
        int botSurcharge = 0;

        if (gameState.ps[player].applyBotSurcharges)
        {
            botSurcharge = a.kind switch
            {
                (byte)Piece.AbilityKind.Move => Piece.move_botSurcharge[a.pieceType],
                (byte)Piece.AbilityKind.Shoot => Piece.shoot_botSurcharge[a.pieceType],
                (byte)Piece.AbilityKind.CaptureVP => Piece.captureVP_botSurcharge[a.pieceType],
                (byte)Piece.AbilityKind.CoreDamage => Piece.coreDamage_botSurcharge[a.pieceType],
                (byte)Piece.AbilityKind.GroupBuild => Piece.groupBuild_botSurcharge[a.pieceType],
                (byte)Piece.AbilityKind.Upgrade => Piece.upgrade_botSurcharge[a.pieceType],
                (byte)Piece.AbilityKind.Launcher => Piece.launcher_botSurcharge[a.pieceType],
                (byte)Piece.AbilityKind.Spawner => Piece.spawn_botSurcharge[a.pieceType],
                (byte)Piece.AbilityKind.SacrificeFactory => Piece.sacrificeFactory_botSurcharge[a.pieceType],
                (byte)Piece.AbilityKind.ConversionFactory => Piece.conversionFactory_botSurcharge[a.pieceType],
                _ => 0
            };
        }



        // Build cost: Create or Spawner actions.
        int buildCost = 0;
        if (a.kind == ActionKind.Create)
        {
            buildCost = Piece.BuildCost[a.pieceType]; // new accessor on PieceDefinition
        }
        else if (a.kind == ActionKind.Spawner)
        {
            int targetType = Piece.spawn_targetType[a.pieceType];
            int amount = Piece.spawn_pieceAmount[a.pieceType];
            if (targetType >= 0 && amount > 0) buildCost = Piece.BuildCost[targetType] * amount;
        }
        else if (a.kind == ActionKind.Upgrade)
        {
            buildCost = Piece.BuildCost[a.pieceType];
        }

        return turnFee + botSurcharge + buildCost;
    }

    /// <summary>
    /// Pure read: compute <paramref name="quoted"/> (including turn fee) and check budget + per-turn caps.
    /// EndTurn is always affordable with quoted = 0. No side effects.
    /// </summary>
    public static bool IsAffordable(in Action a, out float quoted, int gameIndex, int player)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;

        // EndTurn is always free & affordable (never blocked)
        if (a.kind == ActionKind.EndTurn)
        {
            quoted = 0;
            return true;
        }

        quoted = Quote(a, gameIndex, player);

        if (gameState.ps[player].budget < quoted) return false;

        // Once-per-turn gates (read-only caps)
        if (a.kind == ActionKind.CaptureVP && gameState.ps[player].didCaptureVP) return false;
        if (a.kind == ActionKind.CoreDamage && gameState.ps[player].didCoreDamage) return false;

        return true;
    }

    // -------------------- Internals --------------------

    

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

    public static CostBreakdown QuoteBreakdown(in PlayerState cur, in Action theAction)
    {
        return new CostBreakdown(turnFee(in cur), botSurcharge(cur, theAction), buildCost(theAction));
    }

    public static int botSurcharge(in PlayerState cur, in Action theAction)
    {
        if (!cur.applyBotSurcharges) return 0;

        return theAction.kind switch
        {
            Move => Piece.move_botSurcharge[theAction.pieceType],
            Shoot => Piece.shoot_botSurcharge[theAction.pieceType],
            CaptureVP => Piece.captureVP_botSurcharge[theAction.pieceType],
            CoreDamage => Piece.coreDamage_botSurcharge[theAction.pieceType],
            GroupBuild => Piece.groupBuild_botSurcharge[theAction.pieceType],
            Upgrade => Piece.upgrade_botSurcharge[theAction.pieceType],
            Launcher => Piece.launcher_botSurcharge[theAction.pieceType],
            Spawner => Piece.spawn_botSurcharge[theAction.pieceType],
            SacrificeFactory => Piece.sacrificeFactory_botSurcharge[theAction.pieceType],
            ConversionFactory => Piece.conversionFactory_botSurcharge[theAction.pieceType],
            _ => 0
        };
    }


    public static int turnFee(in PlayerState cur)
    {
        int ActionIndex = cur.actionIndexThisTurn;
        return (ActionIndex == 0) ? 0 : RoundToInt(baseActionCost * MathF.Pow(actionGrowthFactor, ActionIndex - 1));
    }

    public static int turnFee(int PlayerIndex, GameState gameState)
    {
        return turnFee(in gameState.ps[PlayerIndex]);
    }

    public static int buildCost(Action theAction)
    {
        return theAction.kind switch
            {
                Create => Piece.BuildCost[theAction.pieceType],
                Upgrade => Piece.BuildCost[theAction.pieceType],
                Spawner => spawnerBuildCost(theAction),
                _ => 0
            };
    }

    private static int spawnerBuildCost(Action theAction)
    {
        int targetType = Piece.spawn_targetType[theAction.pieceType];
        int amount = Piece.spawn_pieceAmount[theAction.pieceType];
        if (targetType >= 0 && amount > 0) return Piece.BuildCost[targetType] * amount;
        return 0;
    }


    // ---- NEW: overload that also returns the breakdown (non-breaking addition) ----
    public static bool IsAffordable(in PlayerState cur, in Action a, out CostBreakdown breakdown, int gameIndex)
    {
        if (a.kind == ActionKind.EndTurn)
        {
            breakdown = new CostBreakdown(0, 0, 0);
            return true;
        }
        breakdown = QuoteBreakdown(cur, a);
        if (cur.budget < breakdown.Total) return false;
        if (a.kind == ActionKind.CaptureVP && cur.didCaptureVP) return false;
        if (a.kind == ActionKind.CoreDamage && cur.didCoreDamage) return false;
        return true;
    }


}
