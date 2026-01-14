using System;
using Game.Core; // Action, OfferQuery, GameState, PlayerState
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public ref struct OfferBuild
{
    public OfferQuery query;
    public Span<Action> outActions;
    public Span<float> outCosts;
    public Span<byte> outMask;
    public int gameIndex;
    public int write;
    public int total;
    public int cap;
}

public ref struct newOfferBuild
{
    public OfferQuery query;
    // public Span<Action> outActions;
    public Span<int> whatAction;
    public Span<int> actingCell;
    public Span<byte> targetCell;
    public Span<byte> aux;
    public int gameIndex;
    public int write;
    public int total;
    public int cap;
}


