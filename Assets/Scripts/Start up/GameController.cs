using UnityEngine;
using Game.Core;
using System;
using Unity.MLAgents.Policies;

public class GameController : MonoBehaviour
{
    #region Game Flow
    public void GameEnd()
    {
        playerManager.onGameEnd(gameIndex);
        _completedGamesCount++;
        if (_completedGamesCount >= Info.maxAutoGames) return;
        
        curriculumCheck();
        if (inspectGame && Info.dbLogging.enabled)
        {
            DbLog.prepDimGame();
            DbLog.logRoundVersion();
        }
        // Clear the board
        board.SetPlayerCoreCells(BuildCoreCellsForNextMatch());
        board.RemoveAllPieces();
        // Re-seed player state from hub
        // Reset GameState (reuse same instance so controllers keep references)
        gameState.Initialize(board, playerManager.CreateAndSeedThePlayerStructs(), startingPlayer, eventManager, this, playerManager, gameIndex);
        GameRegistry.Register(gameIndex, gameState, board, eventManager, this);
        if (!inspectGame) return;
        currentSnapshot = snapshotComposer.GetSnapshot();
        UIBridge.ApplySnapshot(currentSnapshot);  
    }
    #endregion

    #region Set Per Game
    private int[] BuildCoreCellsForNextMatch()
    {
        int[] baseIds = new int[4];
        // (int[])GameBootstrapper.hub.board_coreCellIdByPlayer.Clone();
        baseIds[0] = geos.idByAxial[Info.PlayerCoreAxialCord[0]];
        baseIds[1] = geos.idByAxial[Info.PlayerCoreAxialCord[1]];
        baseIds[2] = geos.idByAxial[Info.PlayerCoreAxialCord[2]];
        baseIds[3] = geos.idByAxial[Info.PlayerCoreAxialCord[3]];
        if (!Info.shuffleCoreCellsPerGame)
        {
            _matchIndex++;
            return baseIds;
        }
        int seed = Info.coreShuffleSeed;
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

    #endregion

    #region Set Once
    public void Start()
    {
        if (gameBootstrapper == null)
        {
            Debug.LogError("GameController missing GameBootstrapper reference.");
            return;
        }
        curriculumCheck();
        eventManager = new EventManager();
        setGameConfigValues();
        BuildTheBoard();
        var players = playerManager.SetPlayers(gameIndex);
        setGameState(players);
        SetDataBaseLogging();
        setInspectGame();
        gameState.BeginTurn();
        gameState.TickPlayer();
    }
    private void setGameConfigValues()
    {
        PieceLimitEnabled = perGameConfig.pieceLimitEnabled;
        PieceLimitPerPlayer = perGameConfig.pieceLimit;
    }

    private void setGameState(PlayerState[] players)
    {
        gameState = new GameState();
        gameState.Initialize(board, players, startingPlayer, eventManager, this, playerManager, gameIndex);
        GameRegistry.Register(gameIndex, gameState, board, eventManager, this);
    }
    
    
    public void BuildTheBoard()
    {
        var geometry = GeometryBuilder.Build();
        geos = geometry;
        board = new BoardModel();
        var coreCells = BuildCoreCellsForNextMatch();
        board.Init(in geometry, coreCellIdOverride: coreCells);
    }

    public void SetDataBaseLogging()
    {
        if (inspectGame && Info.dbLogging.enabled)
        {
            DbLog.InitializeLoggingValues(eventManager);
            // Apply runtime logging tuning from Config
            DbLog.ApplyConfig(in Info.dbLogging);
            DbLog.DeleteConflictingSimIdRows();
            DbLog.logDimSim();
            DbLog.logDimActionType();
            DbLog.logDimPiece();
            DbLog.logPlayerVersion();
            DbLog.logWinTypeVersion();
            DbLog.prepDimGame();
            DbLog.logRoundVersion();
            if (Info.dbLogging.useSharedSession)
                DbLog.StartLoggingSession(transactional: Info.dbLogging.transactionalSession);
        }
    }

    private void setInspectGame()
    {
        if (!inspectGame) return;
        
        var hic = FindFirstObjectByType<HumanInteractionController>();
        
        // pick the first seat marked Human
        byte humanSeat = 0;
        for (byte seat = 0; seat < Info.playerCount; seat++)
        {
            if (Info.playerControl[seat] == Info.ControlMode.Human) { humanSeat = seat; break; }
        }

        UIBridge.Init(hic, gameIndex, humanSeat);

        // === Phase B: compose the initial snapshot & push to BoardView ===
        snapshotComposer = new GameSnapshotComposer(geos, gameIndex);

        // currentSnapshot = snapshotComposer.GetSnapshot();

        // UIBridge.ApplySnapshot(currentSnapshot);

        // Rebuild/push snapshot after every successful action
        gameState.OnActionExecuted += () =>
        {
            currentSnapshot = snapshotComposer.GetSnapshot();
            UIBridge.ApplySnapshot(currentSnapshot);
        };
        
    }

     private void OnDestroy()
    {
        if (Info.dbLogging.enabled && Info.dbLogging.useSharedSession) DbLog.EndLoggingSession(commit: true);
    }

    #endregion

    #region Fields
    PlayerManager playerManager = new PlayerManager();
    public GameBootstrapper gameBootstrapper;
    public int gameIndex;
    public PerGameConfig perGameConfig;
    public EventManager eventManager;
    public bool inspectGame = false;

    public int _completedGamesCount = 0;
    // private bool _restartInProgress = false;
    // private bool _gameOverHandled = false;
    // private bool _resetPendingFromML = false;
    public BoardModel board;
    [Range(0, 3)] public byte startingPlayer = 0;
    private int _matchIndex = 0;
    public bool PieceLimitEnabled;
    public int PieceLimitPerPlayer;
    public bool twoPlayerHurdle;
    public int learningAim = 50; //to do- set up this functionality
    public GameState gameState;
    public GameSnapshot currentSnapshot;   // latest snapshot (read-only for views)
    public GameSnapshotComposer snapshotComposer;   // snapshot builder
    private BoardGeometry geos;

    #endregion

    #region GraveYard

    // private void OnAgentEpisodeBegin(byte seat)
    // {
    //     if (!_resetPendingFromML || _restartInProgress) return;
    //     _resetPendingFromML = false;
    //     _gameOverHandled = false;
    //     RestartMatch();
    // }

    // private bool HasAnyMLControllers()
    // {
    //     for (int i = 0; i < mlControllers.Length; i++)
    //     {
    //         if (mlControllers[i] != null) return true;
    //     }
    //     return false;
    // }

    

     // void Update()
    // {
    //     // Auto-restart flow: detect game end once and optionally restart
    //     if (gameState != null && gameState.IsGameOver)
    //     {
    //         if (!_gameOverHandled)
    //         {
    //             _gameOverHandled = true;
    //             _completedGamesCount++;
    //             _resetPendingFromML = HasAnyMLControllers(); // set before broadcasting so ML callbacks can trigger restart
    //             BroadcastTerminalRewards();

    //             bool auto = config != null && config.autoSim.autoRestartOnGameOver;
    //             bool underCap = (config == null) || (config.autoSim.maxAutoGames <= 0) || (_completedGamesCount < config.autoSim.maxAutoGames);

    //             if (!_resetPendingFromML && auto && underCap && !_restartInProgress)
    //             {
    //                 RestartMatch();
    //                 return; // let the new match tick next frame
    //             }
    //         }
    //         // If not auto-restarting, stop ticking
    //         return;
    //     }

    //     byte cur = gameState.CurrentPlayerId;
    //     if (GameBootstrapper.hub.playerControl[cur] == GameConfigHub.ControlMode.Heuristic)
    //     {
    //         var agent = heuristicControllers[cur];
    //         if (agent != null) agent.Tick();
    //     }
    //     // ML seats: driven by ML-Agents components; Human seats: idle in Phase A
    // }

    
    // private void RestartMatch()
    // {
    //     curriculumCheck();
    //     if (inspectGame && config.dbLogging.enabled)
    //     {
    //         DbLoggingConfig.prepDimGame();
    //         DbLoggingConfig.logRoundVersion();
    //     }
    //     _restartInProgress = true;
    //     try
    //     {
    //         // Clear the board
    //         var coreCells = BuildCoreCellsForNextMatch();
    //         if (board != null)
    //         {
    //             board.SetPlayerCoreCells(coreCells);
    //             board.RemoveAllPieces();
    //         }

    //         // Re-seed player state from hub
    //         var ps = new PlayerState[4];
    //         for (byte i = 0; i < 4; i++)
    //         {
    //             ps[i] = new PlayerState();
    //             bool active = i < GameBootstrapper.hub.player_count;
    //             ps[i].applyBotSurcharges = active && GameBootstrapper.hub.player_applyBotSurcharges[i];
    //             ps[i].applyStartOfTurnBudgetDecrease = active && GameBootstrapper.hub.player_applyStartOfTurnBudgetDecrease[i];
    //             ps[i].isAI = active && GameBootstrapper.hub.player_isAI[i];
    //             ps[i].team = active ? GameBootstrapper.hub.player_team[i] : i;
    //             ps[i].name = active ? GameBootstrapper.hub.player_name[i] : $"P{i}";
    //         }

    //         // Reset GameState (reuse same instance so controllers keep references)
    //         gameState.Initialize(board, ps, startingPlayer, eventManager, this, gameIndex);
    //         GameRegistry.Register(gameIndex, gameState, board, eventManager, this);


    //         if (inspectGame)
    //         {
    //             // Push a fresh snapshot to the view
    //             if (snapshotComposer != null)
    //             {
    //                 currentSnapshot = snapshotComposer.GetSnapshot();
    //                 UIBridge.ApplySnapshot(currentSnapshot);
    //             }
    //         }

    //         // Reset game-over gate
    //         _gameOverHandled = false;
    //     }
    //     finally
    //     {
    //         _restartInProgress = false;
    //     }
    // }
     #endregion
}
