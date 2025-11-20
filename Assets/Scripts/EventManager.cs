using UnityEngine;
using System;

public static class EventManager
{
    public readonly struct ActionLogEvent
    {
        public ActionLogEvent(
            int actionType,
            int? pieceId,
            int? targetPlayerIndex,
            int actingPlayerIndex,
            decimal actionCost,
            decimal? buildCost,
            decimal? surchargeCost)
        {
            ActionType = actionType;
            PieceId = pieceId;
            TargetPlayerIndex = targetPlayerIndex;
            ActingPlayerIndex = actingPlayerIndex;
            ActionCost = actionCost;
            BuildCost = buildCost;
            SurchargeCost = surchargeCost;
        }

        public int ActionType { get; }
        public int? PieceId { get; }
        public int? TargetPlayerIndex { get; }
        public int ActingPlayerIndex { get; }
        public decimal ActionCost { get; }
        public decimal? BuildCost { get; }
        public decimal? SurchargeCost { get; }
    }

    public static event Action<ActionLogEvent> ActionLogRequested;

    public static void RaiseActionLog(ActionLogEvent payload)
    {
        ActionLogRequested?.Invoke(payload);
    }

    public readonly struct TurnLogEvent
    {
        public TurnLogEvent(
            int playerIndex,
            int turnOrdinal,
            bool isPassOnly,
            int coreHealthEnd,
            int victoryPointsEnd,
            decimal? resourceTotalEnd,
            int? digitsStart,
            int? digitsEnd,
            int? piecesOnBoardStart,
            int? piecesOnBoardEnd,
            int playerTurnOrdinal)
        {
            PlayerIndex = playerIndex;
            TurnOrdinal = turnOrdinal;
            IsPassOnly = isPassOnly;
            CoreHealthEnd = coreHealthEnd;
            VictoryPointsEnd = victoryPointsEnd;
            ResourceTotalEnd = resourceTotalEnd;
            DigitsStart = digitsStart;
            DigitsEnd = digitsEnd;
            PiecesOnBoardStart = piecesOnBoardStart;
            PiecesOnBoardEnd = piecesOnBoardEnd;
            PlayerTurnOrdinal = playerTurnOrdinal;
        }

        public int PlayerIndex { get; }
        public int TurnOrdinal { get; }
        public bool IsPassOnly { get; }
        public int CoreHealthEnd { get; }
        public int VictoryPointsEnd { get; }
        public decimal? ResourceTotalEnd { get; }
        public int? DigitsStart { get; }
        public int? DigitsEnd { get; }
        public int? PiecesOnBoardStart { get; }
        public int? PiecesOnBoardEnd { get; }
        public int PlayerTurnOrdinal { get; }
    }

    public enum GameResultType
    {
        Elimination,
        EndOfTurnVictoryPoints,
        Tie
    }

    public readonly struct GameResultEvent
    {
        public GameResultEvent(GameResultType resultType, int? winnerPlayerIndex)
        {
            ResultType = resultType;
            WinnerPlayerIndex = winnerPlayerIndex;
        }

        public GameResultType ResultType { get; }
        public int? WinnerPlayerIndex { get; }
    }

    public static event Action TurnPrepRequested;
    public static event Action<TurnLogEvent> TurnLogRequested;
    public static event Action RoundLogRequested;
    public static event Action<GameResultEvent> GameResultLogged;

    public static void RaiseTurnPrep()
    {
        TurnPrepRequested?.Invoke();
    }

    public static void RaiseTurnLog(TurnLogEvent payload)
    {
        TurnLogRequested?.Invoke(payload);
    }

    public static void RaiseRoundLog()
    {
        RoundLogRequested?.Invoke();
    }

    public static void RaiseGameResult(GameResultEvent payload)
    {
        GameResultLogged?.Invoke(payload);
    }
}
