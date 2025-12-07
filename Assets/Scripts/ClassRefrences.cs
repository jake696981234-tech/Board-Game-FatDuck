using UnityEngine;


public class Games
{
    public Game.Core.GameState gameState;
    public BoardModel boardModel;
    public EventManager eventManager;
    public GameController gameController;
}

public static class GameRegistry
{
    public static Games[] game;

    public static void Init(int gameCount)
    {
        game = new Games[gameCount];
        for (int i = 0; i < gameCount; i++)
        {
            game[i] = new Games
            {
                gameState = new Game.Core.GameState(),
                boardModel = new BoardModel(),
                eventManager = new EventManager(),
                gameController = null
            };
        }
    }

    public static void Register(int gameId, Game.Core.GameState gameState, BoardModel boardModel, EventManager eventManager, GameController gameController)
    {
        if (game == null || gameId < 0 || gameId >= game.Length)
            return;

        var entry = game[gameId] ?? new Games();
        entry.gameState = gameState;
        entry.boardModel = boardModel;
        entry.eventManager = eventManager;
        entry.gameController = gameController;
        game[gameId] = entry;
    }
}
