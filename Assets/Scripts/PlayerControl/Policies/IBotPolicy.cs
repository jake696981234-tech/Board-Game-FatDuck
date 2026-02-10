using System;
using Game.Core;
using Action = Game.Core.Action;




public class Bot
{
    public void initAsBob()
    {
        if (rng.Next(2) == 0) return;
        isCellAimVP = false;
        playerTargets = BotHelpers.MakeRandomPlayerOrder(excludePlayer: playerId, bot: this);
    }
    //dumb bob additions
    public int[] playerTargets;
    public bool isCellAimVP = true;
    public int[] availableActions;
    public Random rng = new Random();
    public Action[] LegalOffers;

    






    // Returns chosen action index in acts, or -1 to indicate no-op.
    // int PickAction(in OfferQuery q,
    //                ReadOnlySpan<Game.Core.Action> acts,
    //                ReadOnlySpan<float> costs,
    //                ReadOnlySpan<byte> mask,
    //                int gameIndex,
    //                 byte playerId);
    public readonly byte playerId; // 0..3
    public readonly int gameIndex;
    public Action[] Offers = new Action[Info.maxOffersToConsider];
    public int NumberOfOffers;
    public float[] Quoted = new float[Info.maxOffersToConsider];
    public byte[] ActionMask = new byte[Info.maxOffersToConsider];


    public Bot(byte thePlayerId, int theGameIndex)
    {
        playerId = thePlayerId;
        gameIndex = theGameIndex;
    }

    public void BuildOffersForCurrentPlayer()
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        // Build OfferQuery: (bm, pcs, ps, playerId, cost)
        // GameActions.GetMultiCreateState(out bool mcActive, out byte mcType, out bool mcBorder, out int mcRemaining, out int[] mcCells, out int mcCellCount, gameIndex);
        var query = new OfferQuery(playerId, gameState.PieceLimitEnabled, gameState.pieceLimitPerPlayer);

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
        NumberOfOffers = Math.Min(total, acts.Length);
    }
}

