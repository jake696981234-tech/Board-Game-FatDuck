using UnityEngine;

public class GameEvents : MonoBehaviour
{
    [Header("Debugging")]
    public static bool enableDebugLogs = false;
    public static bool enableDebugLogsFromPeform = false;


    public void OnEndTurnAction()
    {
        if (enableDebugLogs)
        {
            Debug.Log($"Player ended their turn.");
        }
    }
    public void OnGameOver(byte winner, bool tie)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"Game Over! Winner: {winner} (tie={tie})");
        }
    }
    public void OnBeginTurn(byte currentPlayer)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"Begin turn for player {currentPlayer}");
        }
    }
    public void OnEndRound()
    {
        if (enableDebugLogs)
        {
            Debug.Log("End of round.");
        }
    }
}
