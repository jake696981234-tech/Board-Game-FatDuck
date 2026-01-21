using UnityEngine;
using Game.Core;
using System;
using Unity.MLAgents.Policies;
using static PlayerManager.PlayerType;
using Unity.InferenceEngine;



public class PlayerManager
{
    LeagueConfig LConfig;
    private PlayerControl[] playerControl = new PlayerControl[4];
    public enum PlayerType
    {
        ML = 0,
        DumbGreg = 1,
        Human = 2,
    }
    

    public PlayerState[] SetPlayers(int gameIndex, Config config)
    {
        PlayerState[] playerStructs = CreateAndSeedThePlayerStructs();
        SetWhoControlsPlayers();
        return playerStructs;
    }

    public PlayerState[] CreateAndSeedThePlayerStructs()
    {
        var ps = new PlayerState[4];
        for (byte i = 0; i < 4; i++)
        {
            ps[i] = new PlayerState();
            bool active = i < Info.playerCount;
            ps[i].applyBotSurcharges = active && Info.Players[i].applyBotSurcharges;
            ps[i].applyStartOfTurnBudgetDecrease = active && Info.Players[i].applyStartOfTurnBudgetDecrease;
            ps[i].isAI = active && Info.Players[i].isAI;
            ps[i].team = active ? Info.Players[i].team : i;
            ps[i].name = active ? Info.Players[i].name : $"P{i}";
        }
        return ps;
    }

    private void SetWhoControlsPlayers()
    {
        if (LConfig.EnableMLLeague)
        {
            SetLeaguePlayers();
            return;
        }
        SetPlayersManually();
    }
    
    private void SetPlayersManually()
    {
        for (byte seat = 0; seat < Info.playerCount; seat++)
        {
            switch (Info.playerControl[seat])
            {
                case Info.ControlMode.DumbGreg:
                        playerControl[seat].initAsDumbGreg(seat);
                        playerControl[seat].subscribePlayer();
                        break;
                case Info.ControlMode.MLFrozenBrain:
                        playerControl[seat].initAsMLFrozenBrain(Info.playerBehaviorOverrides[seat].modelAsset, seat);
                        playerControl[seat].subscribePlayer();
                        break;
                case Info.ControlMode.MLLearning:
                        playerControl[seat].initAsMLLearning(Info.behaviorName, seat);
                        playerControl[seat].subscribePlayer();
                        break;
                case Info.ControlMode.Human:
                default:
                    // Dont Need go do anything- could change this for multple players, and/or can seed some values here 
                    break;
            }
        }
    }
    

    #region League SetUp

    private void SetLeaguePlayers()
    {
        playerControl[0].initAsMLLearning(LConfig.LearningPlayersBehaviorNames[0], 0);
        playerControl[0].subscribePlayer();
        setLeagueOpponents();
    }


    #endregion
    #region Run Time

    public event Action<int> TickPlayerIndex;
    public void tickPlayerIndex(int PlayerIndex)
    {
        TickPlayerIndex?.Invoke(PlayerIndex);
    }


    private OpponentPicker.OpponentChoice[] LeagueOpponents = new OpponentPicker.OpponentChoice[3];
    private void setLeagueOpponents()
    {
        OpponentPicker.Pick3(out LeagueOpponents[0], out LeagueOpponents[1], out LeagueOpponents[2]);

        for (int i = 0; i < 3; i++)
        {
            switch (LeagueOpponents[i].kind)
            {
                case OpponentPicker.OpponentKind.DumbGreg:
                    playerControl[i + 1].initAsDumbGreg(i + 1);
                    playerControl[i + 1].subscribePlayer();
                    break;

                case OpponentPicker.OpponentKind.FrozenBrian:
                    playerControl[i + 1].initAsMLFrozenBrain(LeagueOpponents[i].brain, i + 1);
                    playerControl[i + 1].subscribePlayer();
                    break;
            }
        }
    }


    private static int LeagueGamesWon = 0;
    private static int LeagueGamesPlayed = 0;
    public bool ShouldPlayerGradute() // to do
    {
        switch (LConfig.graduationRequirment)
        {
            case LeagueConfig.GraduationRequirment.WinANumberOfGames:
                return LeagueGamesWon < LConfig.HowManyGamesWonToGraduate;
            case LeagueConfig.GraduationRequirment.PlayerANumberOfGames:
                return LeagueGamesWon < LConfig.HowManyGamesPlayedToGraduate;
        }
        throw new ArgumentOutOfRangeException($"Graduation Requirment not set right");
    }


    public void onGameEnd(int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        BroadcastTerminalRewards(gameIndex);

        LeagueGamesPlayed++;
        byte winner = gameState.Winner;
        if (LConfig.EnableMLLeague)
        {
            if (gameState.Winner == 0) LeagueGamesWon++;
        }
    }

    public void BroadcastTerminalRewards(int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;

        byte winner = gameState.Winner;
        for (int i = 0; i < playerControl.Length; i++)
        {
            playerControl[i].OnTerminal(winner);
        }
    }
    

    #endregion 
}