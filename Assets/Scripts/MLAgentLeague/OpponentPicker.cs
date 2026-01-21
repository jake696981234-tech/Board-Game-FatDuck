using System;
using System.Collections.Generic;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;

public static class OpponentPicker
{
    private static LeagueConfig LConfig;
    static readonly System.Random rng = new System.Random();

    public enum OpponentKind { DumbGreg, FrozenBrian }

    public readonly struct OpponentChoice
    {
        public readonly OpponentKind kind;
        public readonly DumbGregAuthoring[] greg; // only set when kind == DumbGreg
        public readonly ModelAsset brain;         // only set when kind == FrozenBrian

        OpponentChoice(OpponentKind kind, DumbGregAuthoring[] greg, ModelAsset brain)
        {
            this.kind = kind;
            this.greg = greg;
            this.brain = brain;
        }

        public static OpponentChoice FromGreg(DumbGregAuthoring[] greg) =>
            new OpponentChoice(OpponentKind.DumbGreg, greg, null);

        public static OpponentChoice FromBrain(ModelAsset brain) =>
            new OpponentChoice(OpponentKind.FrozenBrian, null, brain);
    }

    public static void Pick3(
        out OpponentChoice a, out OpponentChoice b, out OpponentChoice c)
    {
        a = PickOne();
        b = PickOne();
        c = PickOne();
    }

    public static OpponentChoice PickOne()
    {
        int gCount = LConfig.DumbGregs?.Count ?? 0;
        int bCount = LConfig.FrozenBrians?.Count ?? 0;
        int total = gCount + bCount;

        if (total == 0)
            throw new InvalidOperationException("Opponent pools are empty.");

        // Weighted by list sizes => uniform over the combined pool
        int roll = rng.Next(total); // 0..total-1

        if (roll < gCount)
        {
            // Choose a random Greg
            var greg = LConfig.DumbGregs[rng.Next(gCount)];
            return OpponentChoice.FromGreg(greg);
        }
        else
        {
            // Choose a random Brain
            var brain = LConfig.FrozenBrians[rng.Next(bCount)];
            return OpponentChoice.FromBrain(brain);
        }
    }
}
