using UnityEngine;
using Game.Core;
using System;
using Unity.MLAgents.Policies;

public class GameController : MonoBehaviour
{
    public Config config;     // assign in Inspector
    public GameBootstrapper gameBootstrapper;
    public int gameIndex;

    public PerGameConfig perGameConfig;

    public BoardViewController boardView;           // assign in Inspector

    public EventManager eventManager;

    public bool inspectGame = false;

    private int _completedGamesCount = 0;
    private bool _restartInProgress = false;
    private bool _gameOverHandled = false;
    public BoardModel board;

    [Range(0, 3)] public byte startingPlayer = 0;
    private PlayerAgent[] heuristicControllers;
    private MLAgentController[] mlControllers;
    private bool _resetPendingFromML = false;
    private int _matchIndex = 0;


    public bool PieceLimitEnabled;

    public int PieceLimitPerPlayer;

    public bool twoPlayerHurdle;

    public int learningAim = 50; //to do- set up this functionality


    private void curriculumCheck()
    {
        if (perGameConfig.Curriculum.Count == 0) return;
        Debug.Log(perGameConfig.Curriculum.Count);

        foreach (var hurdle in perGameConfig.Curriculum)
        {
            if (hurdle.restriction == curriculumRestriction.onePiece)
            {
                if (hurdle.goalRequirement >= learningAim)
                {
                    PieceLimitEnabled = true;
                    PieceLimitPerPlayer = 1;
                }
                else
                {
                    PieceLimitEnabled = false;
                }
            }
            if (hurdle.restriction == curriculumRestriction.twoPlayer)
            {
                if (hurdle.goalRequirement >= learningAim)
                {
                    twoPlayerHurdle = true;
                }
                else
                {
                    twoPlayerHurdle = false;
                }
            }
        }

    }


    public Game.Core.GameState gameState;


    public GameSnapshot currentSnapshot;   // latest snapshot (read-only for views)
    public GameSnapshotComposer snapshotComposer;   // snapshot builder


    void Start()
    {
        if (gameBootstrapper == null)
        {
            Debug.LogError("GameController missing GameBootstrapper reference.");
            return;
        }
        curriculumCheck();
        eventManager = new EventManager();
        setGameConfigValues();

        var geometry = GeometryBuilder.Build(gameBootstrapper.hub.board_radius);
        board = new BoardModel();
        var coreCells = BuildCoreCellsForNextMatch();
        board.Init(in geometry, in gameBootstrapper.hub, gameBootstrapper.hub.player_count, coreCellIdOverride: coreCells);

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
        gameState.Initialize(in gameBootstrapper.hub, board, ps, startingPlayer, eventManager, this, gameIndex);


        if (inspectGame && config.dbLogging.enabled)
        {
            DbLoggingConfig.InitializeLoggingValues(in gameBootstrapper.hub, eventManager);
            // Apply runtime logging tuning from Config
            DbLoggingConfig.ApplyConfig(in config.dbLogging);
            DbLoggingConfig.DeleteConflictingSimIdRows();
            DbLoggingConfig.logDimSim();
            DbLoggingConfig.logDimActionType();
            DbLoggingConfig.logDimPiece();
            DbLoggingConfig.logPlayerVersion();
            DbLoggingConfig.logWinTypeVersion();
            DbLoggingConfig.prepDimGame();
            DbLoggingConfig.logRoundVersion();
            if (config.dbLogging.useSharedSession)
                DbLoggingConfig.StartLoggingSession(transactional: config.dbLogging.transactionalSession);
        }

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


                        agent.Init(in gameBootstrapper.hub, gameState, board, gameIndex, policy);
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

                        var behaviorOverride = (config != null && config.playerBehaviorOverrides != null && seat < config.playerBehaviorOverrides.Length)
                            ? config.playerBehaviorOverrides[seat]
                            : default;
                        bp.BehaviorType = behaviorOverride.behaviorType;
                        bp.DeterministicInference = behaviorOverride.deterministicInference;
                        if (behaviorOverride.modelAsset != null)
                        {
                            bp.Model = behaviorOverride.modelAsset;
                        }
                        // Now add the Agent so Awake() reads the configured BehaviorParameters
                        var ml = go.AddComponent<MLAgentController>();
                        mlControllers[seat] = ml;
                        ml.SetEpisodeBeginCallback(OnAgentEpisodeBegin);

                        // Build a small PlayerAgent bridge for obs & offer building
                        var paBridge = new PlayerAgent();


                        paBridge.BindSeat(seat);

                        paBridge.Init(in gameBootstrapper.hub, gameState, board, gameIndex);

                        // Wire everything into the ML controller

                        ml.Init(gameBootstrapper.hub, gameState, board, paBridge, seat, in config.mlRewards, gameIndex);
                        break;
                    }
                case GameConfigHub.ControlMode.Human:
                default:
                    // No controller; human input not implemented in Phase A
                    break;
            }


        }


        if (inspectGame)
        {
            var hic = FindFirstObjectByType<HumanInteractionController>();
            if (hic != null)
            {
                // pick the first seat marked Human
                byte humanSeat = 0;
                for (byte s = 0; s < gameBootstrapper.hub.player_count; s++)
                {
                    if (gameBootstrapper.hub.playerControl[s] == GameConfigHub.ControlMode.Human) { humanSeat = s; break; }
                }

                // inject live systems (same ones agents/ML use)
                hic.gameState = gameState;
                hic.boardModel = board;
                if (hic.boardView == null) hic.boardView = boardView;
                hic.SetHumanSeat(humanSeat);
            }


            // === Phase B: compose the initial snapshot & push to BoardView ===
            snapshotComposer = new GameSnapshotComposer(
                geometry,   // local variable from your builder call
                gameIndex
             );

            currentSnapshot = snapshotComposer.GetSnapshot();



            if (boardView != null)
            {
                boardView.ApplySnapshot(currentSnapshot);
            }

            else
            {
                Debug.LogWarning("[׸] BoardView not assigned in Bootstrapper (Phase B).");
            }


            // Rebuild/push snapshot after every successful action
            gameState.OnActionExecuted += () =>
            {
                currentSnapshot = snapshotComposer.GetSnapshot(); if (boardView != null) boardView.ApplySnapshot(currentSnapshot);
            };

            hic.ManualAwake(eventManager, gameIndex);
            hic.ManualEnable();
        }
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
                _resetPendingFromML = HasAnyMLControllers(); // set before broadcasting so ML callbacks can trigger restart
                BroadcastTerminalRewards();

                bool auto = (config != null && config.autoSim.autoRestartOnGameOver);
                bool underCap = (config == null) || (config.autoSim.maxAutoGames <= 0) || (_completedGamesCount < config.autoSim.maxAutoGames);

                if (!_resetPendingFromML && auto && underCap && !_restartInProgress)
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

    private void OnDestroy()
    {
        if (config != null && config.dbLogging.enabled && config.dbLogging.useSharedSession)
            DbLoggingConfig.EndLoggingSession(commit: true);
    }


    private void RestartMatch()
    {
        curriculumCheck();
        if (inspectGame && config.dbLogging.enabled)
        {
            DbLoggingConfig.prepDimGame();
            DbLoggingConfig.logRoundVersion();
        }
        _restartInProgress = true;
        try
        {
            // Clear the board
            var coreCells = BuildCoreCellsForNextMatch();
            if (board != null)
            {
                board.SetPlayerCoreCells(coreCells);
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
            gameState.Initialize(in gameBootstrapper.hub, board, ps, startingPlayer, eventManager, this, gameIndex);


            if (inspectGame)
            {
                // Push a fresh snapshot to the view
                if (snapshotComposer != null)
                {
                    currentSnapshot = snapshotComposer.GetSnapshot();
                    if (boardView != null) boardView.ApplySnapshot(currentSnapshot);
                }
            }

            // Reset game-over gate
            _gameOverHandled = false;
        }
        finally
        {
            _restartInProgress = false;
        }
    }

    private void BroadcastTerminalRewards()
    {
        byte winner = gameState.Winner;
        for (int i = 0; i < mlControllers.Length; i++)
        {
            var ml = mlControllers[i];
            if (ml == null) continue;
            ml.ApplyTerminal(winner);
        }
    }

    private void OnAgentEpisodeBegin(byte seat)
    {
        if (!_resetPendingFromML || _restartInProgress) return;
        _resetPendingFromML = false;
        _gameOverHandled = false;
        RestartMatch();
    }

    private bool HasAnyMLControllers()
    {
        for (int i = 0; i < mlControllers.Length; i++)
        {
            if (mlControllers[i] != null) return true;
        }
        return false;
    }

    private int[] BuildCoreCellsForNextMatch()
    {
        var baseIds = (int[])gameBootstrapper.hub.board_coreCellIdByPlayer.Clone();
        if (config == null || !config.board.shuffleCoreCellsPerGame)
        {
            _matchIndex++;
            return baseIds;
        }

        int seed = config.board.coreShuffleSeed;
        int effectiveSeed = (seed != 0) ? seed + _matchIndex : Environment.TickCount ^ (_matchIndex * 397);
        var rng = new System.Random(effectiveSeed);

        for (int i = baseIds.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (baseIds[i], baseIds[j]) = (baseIds[j], baseIds[i]);
        }

        _matchIndex++;
        return baseIds;
    }


    private void setGameConfigValues()
    {
        PieceLimitEnabled = perGameConfig.pieceLimitEnabled;

        PieceLimitPerPlayer = perGameConfig.pieceLimit;
    }

}
