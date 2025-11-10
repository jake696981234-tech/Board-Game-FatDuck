// Assets/Scripts/Core/GameBootstrapper.cs
using UnityEngine;
using Game.Core;
using System.IO;
using System;

public sealed class GameBootstrapper : MonoBehaviour
{
    [Header("Authoring")]
    public Config config;     // assign in Inspector
    public Pieces pieces;     // your pieces registry asset / component
    public static Pieces PiecesData;
    [Range(2, 4)] public int playerCount = 4;
    [Range(0, 3)] public byte startingPlayer = 0;

    // Live systems (optional to expose for debugging)
    public BoardModel board;
    public Game.Core.GameState gameState;



    private PlayerAgent[] heuristicControllers;   // null on non-heuristic seats
    private MLAgentController[] mlControllers;    // optional, may be null per seat
    private GameConfigHub hub;
    public CostEngine cost;

    public OfferProvider offers;
    public PlayerAgent playerAgent;

    public GameSnapshotComposer snapshotComposer;   // snapshot builder
    public GameSnapshot currentSnapshot;     // latest snapshot (read-only for views)
    public BoardViewController boardView;           // assign in Inspector




    // Auto-sim controls
    private int _completedGamesCount = 0;
    private bool _restartInProgress = false;
    private bool _gameOverHandled = false;

    void Awake()
    {
        string csvPath = Path.Combine(Application.streamingAssetsPath, "pieces.csv");
        PiecesData = PiecesCsvImporter.Import(csvPath);

        Debug.Log($"Loaded {PiecesData.typeCount} types, {PiecesData.abilityCount} abilities.");

        if (config == null) { Debug.LogError("Config asset not assigned."); return; }
        // Ensure everyone uses the imported dataset
        pieces = PiecesData;




        // 1) Freeze authoring into an immutable hub
        hub = config.BuildHub();

        // 2) Build geometry + board
        var geometry = GeometryBuilder.Build(hub.board_radius); // your existing builder call
        board = new BoardModel();
        board.Init(in geometry, in hub, hub.player_count);

        // 3) Cost engine
        cost = new CostEngine(in hub);

        // 4) Seed players from hub
        var ps = new PlayerState[4];
        for (byte i = 0; i < 4; i++)
        {
            ps[i] = new PlayerState();
            bool active = i < hub.player_count;
            ps[i].applyBotSurcharges = active && hub.player_applyBotSurcharges[i];
            ps[i].applyStartOfTurnBudgetDecrease = active && hub.player_applyStartOfTurnBudgetDecrease[i];
            ps[i].isAI = active && hub.player_isAI[i];
            ps[i].team = active ? hub.player_team[i] : i;
            ps[i].name = active ? hub.player_name[i] : $"P{i}";
        }

        // 5) Initialize GameState
        gameState = new Game.Core.GameState();
        //Telementry to data base
        //To do- add missing call, and check order of call
        DbLoggingConfig.InitializeLoggingValues(in hub);
        DbLoggingConfig.DeleteConflictingSimIdRows();
        DbLoggingConfig.logDimSim();
        DbLoggingConfig.logDimActionType();
        DbLoggingConfig.logDimPiece();
        DbLoggingConfig.logPlayerVersion();
        DbLoggingConfig.logWinTypeVersion();
        DbLoggingConfig.prepDimGame();
        DbLoggingConfig.logRoundVersion();
        // Start a shared logging session for faster inserts during gameplay
        DbLoggingConfig.StartLoggingSession(transactional: false);
        gameState.Initialize(in hub, board, GameBootstrapper.PiecesData, cost, ps, startingPlayer);





        // 6) Shared offer provider
        offers = new OfferProvider();

        // 7) Controllers per seat
        heuristicControllers = new PlayerAgent[4];
        mlControllers = new MLAgentController[4];

        for (byte seat = 0; seat < hub.player_count; seat++)
        {
            switch (hub.playerControl[seat])
            {
                case GameConfigHub.ControlMode.Heuristic:
                    {
                        var agent = new PlayerAgent();
                        // Select a policy per seat
                        IBotPolicy policy;
                        if (hub.playerPolicy != null && seat < hub.playerPolicy.Length)
                        {
                            switch (hub.playerPolicy[seat])
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

                        agent.Init(in hub, gameState, board, GameBootstrapper.PiecesData, cost, offers, policy);
                        agent.BindSeat(seat); // (see tiny method below)
                        heuristicControllers[seat] = agent;
                        break;
                    }
                case GameConfigHub.ControlMode.ML:
                    {
                        if (!hub.enableMLAgents)
                        {
                            Debug.LogWarning($"Seat {seat}: ML requested but ML Agents disabled; seat left idle.");
                            break;
                        }
                        // Create/attach ML Agent in scene (self-driven via ML-Agents)
                        var go = new GameObject($"MLAgent_Player_{seat}");


                        // --- Auto inject Behavior Parameters based on config ---
                        var bp = go.AddComponent<Unity.MLAgents.Policies.BehaviorParameters>();
                        bp.BehaviorName = hub.mlBehavior.name;
                        bp.UseChildSensors = hub.mlBehavior.useChildSensors;
                        bp.BrainParameters.VectorObservationSize = hub.mlBehavior.obsSize;
                        bp.BrainParameters.ActionSpec =
                            Unity.MLAgents.Actuators.ActionSpec.MakeDiscrete(hub.mlBehavior.actionBranchSize);

                        // Now add the Agent so Awake() reads the configured BehaviorParameters
                        var ml = go.AddComponent<MLAgentController>();
                        mlControllers[seat] = ml;

                        // Build a small PlayerAgent bridge for obs & offer building
                        var paBridge = new PlayerAgent();

                        paBridge.BindSeat(seat);
                        paBridge.Init(in hub, gameState, board, GameBootstrapper.PiecesData, cost, offers);
                        // Wire everything into the ML controller
                        ml.Init(hub, gameState, board, GameBootstrapper.PiecesData, cost, offers, paBridge, seat);
                        break;
                    }
                case GameConfigHub.ControlMode.Human:
                default:
                    // No controller; human input not implemented in Phase A
                    break;
            }
        }

        // 8) Startup summary
        for (byte seat = 0; seat < hub.player_count; seat++)
        {
            var mode = hub.playerControl[seat];
            Debug.Log($"Seat {seat}: {mode}");
        }

        // 9) Wire the HumanInteractionController (bind human seat + live refs)
        var hic = FindFirstObjectByType<HumanInteractionController>();
        if (hic != null)
        {
            // pick the first seat marked Human
            byte humanSeat = 0;
            for (byte s = 0; s < hub.player_count; s++)
            {
                if (hub.playerControl[s] == GameConfigHub.ControlMode.Human) { humanSeat = s; break; }
            }

            // inject live systems (same ones agents/ML use)
            hic.gameState = gameState;
            hic.boardModel = board;
            hic.pieces = PiecesData;
            hic.costEngine = cost;
            hic.offerProvider = offers;
            if (hic.boardView == null) hic.boardView = boardView;
            hic.SetHumanSeat(humanSeat);
        }

        // -----------------------------------------------------------------------------
        // Sanity logging for ML schema and control modes
        // -----------------------------------------------------------------------------
#if UNITY_EDITOR
        Debug.Log($"[Ω] ====== Phase-A Boot Summary ======");
        Debug.Log($"[Ω] Players: {hub.player_count}, starting player: {startingPlayer}");
        Debug.Log($"[Ω] Board radius: {hub.board_radius}, cells: {hub.board_totalCells}");
        Debug.Log($"[Ω] Pieces: {pieces.typeCount} types, {pieces.abilityCount} abilities");
        Debug.Log($"[Ω] Cost: base {hub.cost_baseActionCost}, growth {hub.cost_actionGrowthFactor}");
        Debug.Log($"[Ω] Agent max offers: {hub.agent.maxOffersToConsider}, rollout depth: {hub.agent.rolloutDepth}, think budget ms: {hub.agent.thinkBudgetMs}");
        int mlObsSize = 21 + 12 * hub.obs_maxCells;
        Debug.Log($"[Ω] ML observation size (Phase A schema): {mlObsSize}");

        // Confirm Config & Hub agreement
        int expectedObs = 21 + 12 * hub.obs_maxCells;
        Debug.Log($"[Ω] Observation vector length: {expectedObs} (Config: {hub.obs_maxCells} cells)");
        Debug.Log($"[Ω] Action branch size: {hub.agent.maxOffersToConsider}");
        Debug.Log($"[Ω] ML-Agents enabled: {hub.enableMLAgents}");


        // Scan all MLAgentControllers in the scene
        var mlAgents = FindObjectsByType<MLAgentController>(FindObjectsSortMode.None);
        Debug.Log($"[Ω] Found {mlAgents.Length} MLAgentController components in scene.");

        foreach (var ml in mlAgents)

        {
            var bp = ml.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            if (bp == null)
            {
                Debug.LogWarning($"[Ω] ML seat {ml.gameObject.name}: Missing BehaviorParameters!");
                continue;
            }

            int obsCount = bp.BrainParameters.VectorObservationSize;
            int actionSize = bp.BrainParameters.ActionSpec.BranchSizes.Length > 0
                ? bp.BrainParameters.ActionSpec.BranchSizes[0]
                : -1;

            string okObs = (obsCount == expectedObs) ? "OK" : $"MISMATCH (saw {obsCount})";
            string okAct = (actionSize == hub.agent.maxOffersToConsider) ? "OK" : $"MISMATCH (saw {actionSize})";

            Debug.Log($"[Ω] ML seat {ml.gameObject.name}: obs={obsCount}→{okObs}, branch={actionSize}→{okAct}");
        }

        // Confirm per-seat control modes
        for (int i = 0; i < hub.player_count; i++)
            Debug.Log($"[Ω] Seat {i}: {hub.playerControl[i]}");

        Debug.Log($"[Ω] ====== End Boot Summary ======");

#endif

        // === Phase B: compose the initial snapshot & push to BoardView ===
        snapshotComposer = new GameSnapshotComposer(
            geometry,   // local variable from your builder call
            board,      // BoardModel
            gameState,  // Game.Core.GameState
            pieces      // Pieces (CSV-driven)
        );

        currentSnapshot = snapshotComposer.GetSnapshot();

        if (boardView != null)
        {
            boardView.ApplySnapshot(currentSnapshot);
        }

        else
        {
            Debug.LogWarning("[Ω] BoardView not assigned in Bootstrapper (Phase B).");
        }


        // Rebuild/push snapshot after every successful action
        gameState.OnActionExecuted += () =>
        {
            currentSnapshot = snapshotComposer.GetSnapshot(); if (boardView != null) boardView.ApplySnapshot(currentSnapshot);
        };





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

                bool auto = (config != null && config.autoRestartOnGameOver);
                bool underCap = (config == null) || (config.maxAutoGames <= 0) || (_completedGamesCount < config.maxAutoGames);

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
        if (hub.playerControl[cur] == GameConfigHub.ControlMode.Heuristic)
        {
            var agent = heuristicControllers[cur];
            if (agent != null) agent.Tick();
        }
        // ML seats: driven by ML-Agents components; Human seats: idle in Phase A
    }

    private void OnDestroy()
    {
        DbLoggingConfig.EndLoggingSession(commit: true);
    }

    private void RestartMatch()
    {
        DbLoggingConfig.prepDimGame();
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
                bool active = i < hub.player_count;
                ps[i].applyBotSurcharges = active && hub.player_applyBotSurcharges[i];
                ps[i].applyStartOfTurnBudgetDecrease = active && hub.player_applyStartOfTurnBudgetDecrease[i];
                ps[i].isAI = active && hub.player_isAI[i];
                ps[i].team = active ? hub.player_team[i] : i;
                ps[i].name = active ? hub.player_name[i] : $"P{i}";
            }

            // Reset GameState (reuse same instance so controllers keep references)
            gameState.Initialize(in hub, board, GameBootstrapper.PiecesData, cost, ps, startingPlayer);

            // Push a fresh snapshot to the view
            if (snapshotComposer != null)
            {
                currentSnapshot = snapshotComposer.GetSnapshot();
                if (boardView != null) boardView.ApplySnapshot(currentSnapshot);
            }

            // Reset game-over gate
            _gameOverHandled = false;
        }
        finally
        {
            _restartInProgress = false;
        }
    }
}


