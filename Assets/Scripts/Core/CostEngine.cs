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
    /// <summary>
    /// Pure read: return the deterministic price of taking <paramref name="theAction"/> in the given context.
    /// Quote = TurnFee(k) + AbilityCost + BuildCost, where k = cur.actionIndexThisTurn.
    /// No side effects. No allocations.
    /// </summary>
    public static int Quote(in Action theAction, int gameIndex, int player)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        return turnFee(player, gameState) + whatIsbotSurcharge(in gameState.ps[player], in theAction, gameIndex) + buildCost(theAction, gameIndex);
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

    public static CostBreakdown QuoteBreakdown(in PlayerState cur, in Action theAction, int gameIndex)
    {
        return new CostBreakdown(turnFee(in cur), whatIsbotSurcharge(cur, theAction, gameIndex), buildCost(theAction, gameIndex));
    }

    public static int whatIsbotSurcharge(in PlayerState cur, in Action theAction, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        if (!cur.applyBotSurcharges) return 0;

        return theAction.kind switch
        {
            Move => Piece.move_botSurcharge[bm.GetPieceTypeFromCell(theAction.ActorsCell)],
            Shoot => Piece.shoot_botSurcharge[bm.GetPieceTypeFromCell(theAction.ActorsCell)],
            CaptureVP => Piece.captureVP_botSurcharge[bm.GetPieceTypeFromCell(theAction.ActorsCell)],
            CoreDamage => Piece.coreDamage_botSurcharge[bm.GetPieceTypeFromCell(theAction.ActorsCell)],
            GroupBuild => Piece.groupBuild_botSurcharge[bm.GetPieceTypeFromCell(theAction.ActorsCell)],
            Upgrade => Piece.upgrade_botSurcharge[bm.GetPieceTypeFromCell(theAction.ActorsCell)],
            Launcher => Piece.launcher_botSurcharge[bm.GetPieceTypeFromCell(theAction.ActorsCell)],
            Spawner => Piece.spawn_botSurcharge[bm.GetPieceTypeFromCell(theAction.ActorsCell)],
            SacrificeFactory => Piece.sacrificeFactory_botSurcharge[bm.GetPieceTypeFromCell(theAction.ActorsCell)],
            ConversionFactory => Piece.conversionFactory_botSurcharge[bm.GetPieceTypeFromCell(theAction.ActorsCell)],
            _ => 0
        };
    }


    public static int turnFee(in PlayerState cur)
    {
        int ActionIndex = cur.actionIndexThisTurn;
        return (ActionIndex == 0) ? 0 : RoundToInt(Info.baseActionCost * MathF.Pow(Info.actionGrowthFactor, ActionIndex - 1));
    }

    public static int turnFee(int PlayerIndex, GameState gameState)
    {
        return turnFee(in gameState.ps[PlayerIndex]);
    }

    public static int buildCost(Action theAction, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        return theAction.kind switch
            {
                Create => Piece.BuildCost[theAction.TargetType],
                Upgrade => Piece.BuildCost[bm.GetPieceTypeFromCell(theAction.ActorsCell)],
                Spawner => spawnerBuildCost(theAction, gameIndex),
                _ => 0
            };
    }

    private static int spawnerBuildCost(Action theAction, int gameIndex)
    {
        var bm = GameRegistry.game[gameIndex].boardModel;
        int targetType = Piece.spawn_targetType[bm.GetPieceTypeFromCell(theAction.ActorsCell)];
        int amount = Piece.spawn_pieceAmount[bm.GetPieceTypeFromCell(theAction.ActorsCell)];
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
        breakdown = QuoteBreakdown(cur, a, gameIndex);
        if (cur.budget < breakdown.Total) return false;
        if (a.kind == ActionKind.CaptureVP && cur.didCaptureVP) return false;
        if (a.kind == ActionKind.CoreDamage && cur.didCoreDamage) return false;
        return true;
    }


}
