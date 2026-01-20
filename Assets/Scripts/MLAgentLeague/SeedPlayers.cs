using UnityEngine;
using Game.Core;
using System;
using Unity.MLAgents.Policies;
using static PlayerManager.PlayerType;
using Unity.InferenceEngine;

public class PlayerManager
{
    LeagueConfig LConfig;
    public PlayerType[] playerType = new PlayerType[4];
    private MLSam[] mlControllers = new MLSam[4];
    private PlayerAgent[] DumbBots = new PlayerAgent[4];

    public enum PlayerType
    {
        ML = 0,
        DumbBot = 1,
        Human = 2,
    }
    

    public void BroadcastTerminalRewards(int gameIndex)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;

        byte winner = gameState.Winner;
        for (int i = 0; i < mlControllers.Length; i++)
        {
            var ml = mlControllers[i];
            if (ml == null) continue;
            ml.ApplyTerminal(winner);
        }
    }

    public PlayerState[] SetPlayers(int gameIndex, Config config)
    {
        var ps = CreateAndSeedThePlayerStructs();
        SetWhoControlsPlayers(gameIndex, config);
        return ps;
    }

    public PlayerState[] CreateAndSeedThePlayerStructs()
    {
        var ps = new PlayerState[4];
        for (byte i = 0; i < 4; i++)
        {
            ps[i] = new PlayerState();
            bool active = i < GameBootstrapper.hub.player_count;
            ps[i].applyBotSurcharges = active && GameBootstrapper.hub.player_applyBotSurcharges[i];
            ps[i].applyStartOfTurnBudgetDecrease = active && GameBootstrapper.hub.player_applyStartOfTurnBudgetDecrease[i];
            ps[i].isAI = active && GameBootstrapper.hub.player_isAI[i];
            ps[i].team = active ? GameBootstrapper.hub.player_team[i] : i;
            ps[i].name = active ? GameBootstrapper.hub.player_name[i] : $"P{i}";
        }
        return ps;
    }

    private void SetWhoControlsPlayers(int gameIndex, Config config)
    {
        if (LConfig.EnableMLLeague)
        {
            SetLeaguePlayers(gameIndex, config);
            return;
        }
        SetManualPlayers(gameIndex, config);
    }
    private void SetLeaguePlayers(int gameIndex, Config config)
    {
        var gameController = GameRegistry.game[gameIndex].gameController;

        if (gameController._completedGamesCount == 0)
        {
            setControlMLBot(0, gameIndex, config);
        }
    }
    private void SetManualPlayers(int gameIndex, Config config)
    {
        for (byte seat = 0; seat < GameBootstrapper.hub.player_count; seat++)
        {
            switch (GameBootstrapper.hub.playerControl[seat])
            {
                case GameConfigHub.ControlMode.DumbBot:
                    {
                        setControlDumbBot(seat, gameIndex, config);
                        break;
                    }
                case GameConfigHub.ControlMode.ML:
                    {
                        setControlMLBot(seat, gameIndex, config);
                        // setbehaviorName()
                        break;
                    }
                case GameConfigHub.ControlMode.Human:
                default:
                    // Dont Need go do anything- could change this for multple players, and/or can seed some values here 
                    break;
            }
        }
    }
    private void setControlMLBot(byte seat, int gameIndex, Config config)
    {
        if (!GameBootstrapper.hub.enableMLAgents)
        {
            Debug.LogWarning($"Seat {seat}: ML requested but ML Agents disabled; seat left idle.");
            return;
        }
        // Create/attach ML Agent in scene (self-driven via ML-Agents)
        GameObject MLObjectRoot = new GameObject($"MLAgent_Player_{seat}");
        // --- Auto inject Behavior Parameters based on config ---
        setBehaviorParameters(seat, MLObjectRoot, config);



        // Now add the Agent so Awake() reads the configured BehaviorParameters
        mlControllers[seat] = MLObjectRoot.AddComponent<MLSam>();

        // Wire everything into the ML controller
        // to do
        // ml.Init(gameState, board, paBridge, seat, in config.mlRewards, gameIndex);
    }

    private void setbehaviorName(BehaviorParameters behaviorParameters, string BehaviorName)
    {
        behaviorParameters.BehaviorName = BehaviorName;
    }

    private void setFrozenModel(BehaviorParameters behaviorParameters, ModelAsset modelAseet)
    {
        behaviorParameters.Model = modelAseet;
    }





    private void setBehaviorParameters(int seat, GameObject MLObjectRoot, Config config)
    {
        BehaviorParameters BehaviorParam = MLObjectRoot.AddComponent<BehaviorParameters>();
        // BehaviorParam.BehaviorName = GameBootstrapper.hub.mlBehavior.name;
        BehaviorParam.UseChildSensors = GameBootstrapper.hub.mlBehavior.useChildSensors;
        BehaviorParam.BrainParameters.VectorObservationSize = GameBootstrapper.hub.mlBehavior.obsSize;
        BehaviorParam.BrainParameters.ActionSpec = Unity.MLAgents.Actuators.ActionSpec.MakeDiscrete(GameBootstrapper.hub.mlBehavior.actionBranchSize);
        BehaviorParam.TeamId = (seat < GameBootstrapper.hub.player_team.Length) ? GameBootstrapper.hub.player_team[seat]: seat;

        var behaviorOverride = (config != null && config.playerBehaviorOverrides != null && seat < config.playerBehaviorOverrides.Length) ? config.playerBehaviorOverrides[seat] : default;
        BehaviorParam.DeterministicInference = behaviorOverride.deterministicInference;
        // if (behaviorOverride.modelAsset != null) BehaviorParam.Model = behaviorOverride.modelAsset;
    }
    private void setControlDumbBot(byte seat, int gameIndex, Config config)
    {
        var gameState = GameRegistry.game[gameIndex].gameState;
        var bm = GameRegistry.game[gameIndex].boardModel;
                


        var agent = new PlayerAgent();
        // Select a policy per seat
        IBotPolicy policy;
        if (GameBootstrapper.hub.playerPolicy != null && seat < GameBootstrapper.hub.playerPolicy.Length)
        {
            switch (GameBootstrapper.hub.playerPolicy[seat])
            {
                case GameConfigHub.PolicyKind.DumbGreg:
                    {
                        DumbGregAuthoring DumbGregWeights = config.dumbGreg;
                        int? seed = null;
                        if (DumbGregWeights.seedBase != 0)
                        {
                            seed = DumbGregWeights.seedBySeat ? DumbGregWeights.seedBase + seat : DumbGregWeights.seedBase;
                        }
                        policy = new DumbGregBotPolicy(
                            endTurnAfterFirstPct: DumbGregWeights.endTurnAfterFirstPct,
                            shootInsteadPct: DumbGregWeights.shootInsteadPct,
                            moveAnotherPct: DumbGregWeights.moveAnotherPct,
                            moveBuildingPct: DumbGregWeights.moveBuildingPct,
                            createInsteadPct: DumbGregWeights.createInsteadPct,
                            seed: seed
                        );
                        break;
                    }
                case GameConfigHub.PolicyKind.Heuristic:
                default: policy = new HeuristicPolicy(); break;
            }
        }
        else { policy = new HeuristicPolicy(); }

        agent.Init(gameState, bm, gameIndex, policy);
        agent.BindSeat(seat); // (see tiny method below)
        DumbBots[seat] = agent;
    }
}