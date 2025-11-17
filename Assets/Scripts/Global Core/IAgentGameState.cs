using Game.Core;

namespace Game.Core
{
    public interface IAgentGameState
    {
        void Initialize(in GameConfigHub hub,
                        BoardModel board,
                        Pieces pieces,
                        CostEngine pricing,
                        PlayerState[] players,
                        byte startingPlayer, GameController gameController);

        bool Perform(in Action a);

        byte CurrentPlayerId { get; }
        ref readonly PlayerState CurrentPlayerRef { get; }
        byte CurrentActionIndex { get; }
        bool CurrentDidCaptureVP { get; }
        bool CurrentDidCoreDamage { get; }

        int RoundsLeft { get; }
        bool IsGameOver { get; }
        byte Winner { get; }

        int GetCenterVP();
        int GetCoreHealth(byte player);
        int GetVP(byte player);
        float GetBudget(byte player);
    }
}
