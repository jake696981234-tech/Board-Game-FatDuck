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
            game[i] = CreateNewGame(i);
    }

    private static Games CreateNewGame(int gameId)
    {
        return new Games
        {
            gameState = new Game.Core.GameState(),
            boardModel = new BoardModel(),
            eventManager = new EventManager(),
            gameController = new GameController(),
        };
    }
}
