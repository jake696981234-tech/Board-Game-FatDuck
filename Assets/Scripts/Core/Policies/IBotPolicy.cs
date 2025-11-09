using System;
using Game.Core;

public interface IBotPolicy
{
    // Returns chosen action index in acts, or -1 to indicate no-op.
    int PickAction(in OfferQuery q,
                   ReadOnlySpan<Game.Core.Action> acts,
                   ReadOnlySpan<float> costs,
                   ReadOnlySpan<byte> mask);
}

