using UnityEngine;
using Game.Core;
using System;
using Unity.MLAgents.Policies;
using static Info.ControlMode;
using Unity.InferenceEngine;



public class PlayerManager
{
    private PlayerControl[] playerControl = new PlayerControl[4];

    public PlayerState[] SetPlayers(int gameIndex)
    {
        PlayerState[] playerStructs = CreateAndSeedThePlayerStructs();
        SetWhoControlsPlayers(gameIndex);
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

    private void SetWhoControlsPlayers(int gameIndex)
    {
        if (Info.EnableMLLeague) { SetLeaguePlayers(gameIndex); return; }
        SetPlayersManually(gameIndex);
    }

    private void SetPlayersManually(int gameIndex)
    {
        for (byte seat = 0; seat < Info.playerCount; seat++)
        {
            switch (Info.playerControl[seat])
            {
                case DumbGreg:
                    playerControl[seat] = new PlayerControl();
                    playerControl[seat].init(DumbGreg, seat, gameIndex, this);
                    break;
                case FrozenML:
                    playerControl[seat] = new PlayerControl();
                    playerControl[seat].init(FrozenML, seat, gameIndex, this, Info.playerBehaviorOverrides[seat].modelAsset);
                    break;
                case LearningML:
                    playerControl[seat] = new PlayerControl();
                    playerControl[seat].init(LearningML, seat, gameIndex, this, null, Info.behaviorName);
                    break;
                case dumbBob:
                    playerControl[seat] = new PlayerControl();
                    playerControl[seat].init(dumbBob, seat, gameIndex, this);
                    break;
                case Human:
                default:
                    // Dont Need go do anything- could change this for multple players, and/or can seed some values here 
                    break;
            }
        }
    }


    #region League SetUp

    private void SetLeaguePlayers(int gameIndex)
    {
        playerControl[0] = new PlayerControl();
        playerControl[0].init(LearningML, 0, gameIndex, this, null, Info.LearningPlayersBehaviorNames[0]);
        setLeagueOpponents(gameIndex);
    }


    #endregion
    #region Run Time

    public event Action<int, GameState> TickPlayerIndex;
    public void tickPlayerIndex(int PlayerIndex, GameState gameState)
    {
        TickPlayerIndex?.Invoke(PlayerIndex, gameState);
    }


    private OpponentPicker.OpponentChoice[] LeagueOpponents = new OpponentPicker.OpponentChoice[3];
    private void setLeagueOpponents(int gameIndex)
    {
        OpponentPicker.Pick3(out LeagueOpponents[0], out LeagueOpponents[1], out LeagueOpponents[2]);

        for (byte i = 1; i < 3; i++)
        {
            switch (LeagueOpponents[i].kind)
            {
                case DumbGreg:
                    playerControl[i] = new PlayerControl();
                    playerControl[i].init(DumbGreg, i, gameIndex, this);
                    break;
                case FrozenML:
                    playerControl[i] = new PlayerControl();
                    playerControl[i].init(FrozenML, i, gameIndex, this, LeagueOpponents[i].brain);
                    break;
            }
        }
    }


    private static int LeagueGamesWon = 0;
    private static int LeagueGamesPlayed = 0;
    public bool ShouldPlayerGradute() // to do
    {
        switch (Info.graduationRequirment)
        {
            case Info.GraduationRequirment.WinANumberOfGames:
                return LeagueGamesWon < Info.HowManyGamesWonToGraduate;
            case Info.GraduationRequirment.PlayerANumberOfGames:
                return LeagueGamesWon < Info.HowManyGamesPlayedToGraduate;
        }
        throw new ArgumentOutOfRangeException($"Graduation Requirment not set right");
    }


    public void onGameEnd(int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        BroadcastTerminalRewards(gameIndex);

        if (!Info.EnableMLLeague) return;
        LeagueGamesPlayed++;
        if (gameState.Winner == 0) LeagueGamesWon++;
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