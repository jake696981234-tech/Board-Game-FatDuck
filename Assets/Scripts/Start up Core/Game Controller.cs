using UnityEngine;
using Game.Core;
using System.IO;
using System;

public class GameController : MonoBehaviour
{
    public Config config;     // assign in Inspector

    public GameBootstrapper gameBootstrapper;

    private int _completedGamesCount = 0;
    private bool _restartInProgress = false;
    private bool _gameOverHandled = false;


    [Range(0, 3)] public byte startingPlayer = 0;

    private PlayerAgent[] heuristicControllers;
    private MLAgentController[] mlControllers;

    public BoardModel board;

    public Game.Core.GameState gameState;




    void Start()
    {
        if (gameBootstrapper == null)
        {
            Debug.LogError("GameController missing GameBootstrapper reference.");
            return;
        }

        var geometry = GeometryBuilder.Build(gameBootstrapper.hub.board_radius); // your existing builder call
        board = new BoardModel();
        board.Init(in geometry, in gameBootstrapper.hub, gameBootstrapper.hub.player_count);

        var ps = new PlayerState[4];
        for (byte i = 0; i < 4; i++)
        {
            ps[i] = new PlayerState();
            bool active = i < gameBootstrapper.hub.player_count;
            ps[i].applyBotSurcharges = active && gameBootstrapper.hub.player_applyBotSurcharges[i];
            ps[i].applyStartOfTurnBudgetDecrease = active && gameBootstrapper.hub.player_applyStartOfTurnBudgetDecrease[i];
            ps[i].isAI = active && gameBootstrapper.hub.player_isAI[i];
            ps[i].team = active ? gameBootstrapper.hub.player_team[i] : i;
            ps[i].name = active ? gameBootstrapper.hub.player_name[i] : $"P{i}";
        }

        gameState = new Game.Core.GameState();



        heuristicControllers = new PlayerAgent[4];
        mlControllers = new MLAgentController[4];



        for (byte seat = 0; seat < gameBootstrapper.hub.player_count; seat++)
        {
            switch (gameBootstrapper.hub.playerControl[seat])
            {
                case GameConfigHub.ControlMode.Heuristic:
                    {
                        var agent = new PlayerAgent();
                        // Select a policy per seat
                        IBotPolicy policy;
                        if (gameBootstrapper.hub.playerPolicy != null && seat < gameBootstrapper.hub.playerPolicy.Length)
                        {
                            switch (gameBootstrapper.hub.playerPolicy[seat])
                            {
                                case GameConfigHub.PolicyKind.DumbGreg:
                                    {
                                        var dg = (config != null) ? config.dumbGreg : default;
                                        int? seed = null;
                                        if (dg.seedBase != 0)
                                        {
                                            seed = dg.seedBySeat ? dg.seedBase + seat : dg.seedBase;
                                        }
                                        policy = new DumbGregBotPolicy(
                                            endTurnAfterFirstPct: dg.endTurnAfterFirstPct,
                                            shootInsteadPct: dg.shootInsteadPct,
                                            moveAnotherPct: dg.moveAnotherPct,
                                            moveBuildingPct: dg.moveBuildingPct,
                                            createInsteadPct: dg.createInsteadPct,
                                            seed: seed
                                        );
                                        break;
                                    }
                                case GameConfigHub.PolicyKind.Heuristic:
                                default: policy = new HeuristicPolicy(); break;
                            }
                        }
                        else { policy = new HeuristicPolicy(); }

                        agent.Init(in gameBootstrapper.hub, gameState, board, GameBootstrapper.PiecesData, gameBootstrapper.cost, gameBootstrapper.offers, policy);
                        agent.BindSeat(seat); // (see tiny method below)
                        heuristicControllers[seat] = agent;
                        break;
                    }
                case GameConfigHub.ControlMode.ML:
                    {
                        if (!gameBootstrapper.hub.enableMLAgents)
                        {
                            Debug.LogWarning($"Seat {seat}: ML requested but ML Agents disabled; seat left idle.");
                            break;
                        }
                        // Create/attach ML Agent in scene (self-driven via ML-Agents)
                        var go = new GameObject($"MLAgent_Player_{seat}");


                        // --- Auto inject Behavior Parameters based on config ---
                        var bp = go.AddComponent<Unity.MLAgents.Policies.BehaviorParameters>();
                        bp.BehaviorName = gameBootstrapper.hub.mlBehavior.name;
                        bp.UseChildSensors = gameBootstrapper.hub.mlBehavior.useChildSensors;
                        bp.BrainParameters.VectorObservationSize = gameBootstrapper.hub.mlBehavior.obsSize;
                        bp.BrainParameters.ActionSpec =
                            Unity.MLAgents.Actuators.ActionSpec.MakeDiscrete(gameBootstrapper.hub.mlBehavior.actionBranchSize);
                        bp.TeamId = (seat < gameBootstrapper.hub.player_team.Length)
                            ? gameBootstrapper.hub.player_team[seat]
                            : seat;
                        // Optional external model binding
#if BARRACUDA_PRESENT
                        if (gameBootstrapper.hub.modelConfig.useExternalModel && gameBootstrapper.hub.modelConfig.bindings != null)
                        {
                            foreach (var binding in gameBootstrapper.hub.modelConfig.bindings)
                            {
                                if (!string.IsNullOrEmpty(binding.behaviorName) && binding.behaviorName != bp.BehaviorName) continue;
                                if (binding.teamId >= 0 && bp.TeamId != binding.teamId) continue;
                                if (string.IsNullOrEmpty(binding.onnxPath)) continue;
                        var nn = ModelLoaderUtil.LoadModelFromStreamingAssets(binding.onnxPath);
                                if (nn != null)
                                {
                                    bp.Model = nn;
                                    bp.InferenceDevice = gameBootstrapper.hub.modelConfig.inferenceDevice;
                                }
                                break;
                            }
                        }
#endif

                        // Now add the Agent so Awake() reads the configured BehaviorParameters
                        var ml = go.AddComponent<MLAgentController>();
                        mlControllers[seat] = ml;

                        // Build a small PlayerAgent bridge for obs & offer building
                        var paBridge = new PlayerAgent();

                        paBridge.BindSeat(seat);
                        paBridge.Init(in gameBootstrapper.hub, gameState, board, GameBootstrapper.PiecesData, gameBootstrapper.cost, gameBootstrapper.offers);
                        // Wire everything into the ML controller
                        ml.Init(gameBootstrapper.hub, gameState, board, GameBootstrapper.PiecesData, gameBootstrapper.cost, gameBootstrapper.offers, paBridge, seat, in config.mlRewards);
                        break;
                    }
                case GameConfigHub.ControlMode.Human:
                default:
                    // No controller; human input not implemented in Phase A
                    break;
            }
        }

        gameState.Initialize(in gameBootstrapper.hub, board, GameBootstrapper.PiecesData, gameBootstrapper.cost, ps, startingPlayer);

    }

    void Update()
    {
        // Auto-restart flow: detect game end once and optionally restart
        if (gameState != null && gameState.IsGameOver)
        {
            if (!_gameOverHandled)
            {
                _gameOverHandled = true;
                _completedGamesCount++;

                bool auto = (config != null && config.autoSim.autoRestartOnGameOver);
                bool underCap = (config == null) || (config.autoSim.maxAutoGames <= 0) || (_completedGamesCount < config.autoSim.maxAutoGames);

                if (auto && underCap && !_restartInProgress)
                {
                    RestartMatch();
                    return; // let the new match tick next frame
                }
            }
            // If not auto-restarting, stop ticking
            return;
        }

        byte cur = gameState.CurrentPlayerId;
        if (gameBootstrapper.hub.playerControl[cur] == GameConfigHub.ControlMode.Heuristic)
        {
            var agent = heuristicControllers[cur];
            if (agent != null) agent.Tick();
        }
        // ML seats: driven by ML-Agents components; Human seats: idle in Phase A
    }

    private void RestartMatch()
    {
        _restartInProgress = true;
        try
        {
            // Clear the board
            if (board != null)
            {
                board.RemoveAllPieces();
            }

            // Re-seed player state from hub
            var ps = new PlayerState[4];
            for (byte i = 0; i < 4; i++)
            {
                ps[i] = new PlayerState();
                bool active = i < gameBootstrapper.hub.player_count;
                ps[i].applyBotSurcharges = active && gameBootstrapper.hub.player_applyBotSurcharges[i];
                ps[i].applyStartOfTurnBudgetDecrease = active && gameBootstrapper.hub.player_applyStartOfTurnBudgetDecrease[i];
                ps[i].isAI = active && gameBootstrapper.hub.player_isAI[i];
                ps[i].team = active ? gameBootstrapper.hub.player_team[i] : i;
                ps[i].name = active ? gameBootstrapper.hub.player_name[i] : $"P{i}";
            }

            // Reset GameState (reuse same instance so controllers keep references)
            gameState.Initialize(in gameBootstrapper.hub, board, GameBootstrapper.PiecesData, gameBootstrapper.cost, ps, startingPlayer);



            // Reset game-over gate
            _gameOverHandled = false;
        }
        finally
        {
            _restartInProgress = false;
        }
    }

}
