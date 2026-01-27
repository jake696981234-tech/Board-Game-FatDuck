using System;
using Game.Core;
using Action = Game.Core.Action;



public class IBotPolicy
{
    // Returns chosen action index in acts, or -1 to indicate no-op.
    // int PickAction(in OfferQuery q,
    //                ReadOnlySpan<Game.Core.Action> acts,
    //                ReadOnlySpan<float> costs,
    //                ReadOnlySpan<byte> mask,
    //                int gameIndex,
    //                 byte playerId);
    public byte playerId = 0; // 0..3
    public Action[] Offers;
    public int NumberOfOffers;
    public float[] Quoted;
    public byte[] ActionMask;
    public int gameIndex;

    public int BuildOffersForCurrentPlayer() 
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        // Build OfferQuery: (bm, pcs, ps, playerId, cost)
        // GameActions.GetMultiCreateState(out bool mcActive, out byte mcType, out bool mcBorder, out int mcRemaining, out int[] mcCells, out int mcCellCount, gameIndex);
        var query = new OfferQuery(playerId, gameState.PieceLimitEnabled, gameState.pieceLimitPerPlayer
            /*mcActive, mcType, mcBorder, mcRemaining, mcCells, mcCellCount */);

        var acts = Offers.AsSpan();
        var costs = Quoted.AsSpan();
        var mask = ActionMask.AsSpan();

        OfferBuild offerBuild;
        offerBuild.query = query;
        offerBuild.outActions = acts;
        offerBuild.outCosts = costs;
        offerBuild.outMask = mask;
        offerBuild.gameIndex = gameIndex;
        offerBuild.write = 0;
        offerBuild.total = 0;
        offerBuild.cap = 0;

        int total = OfferProvider.BuildActionList(ref offerBuild);
        // We only allow the emitted prefix to be selectable by the policy
        return Math.Min(total, acts.Length);
    }
}

