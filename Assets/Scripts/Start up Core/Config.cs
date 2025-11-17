// Assets/Scripts/Core/Config.cs
using UnityEngine;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;


[CreateAssetMenu(fileName = "Config", menuName = "Game/Config", order = 0)]
public sealed class Config : ScriptableObject
{


    [Header("Agent (global)")]
    public AgentAuthoring agent = new AgentAuthoring
    {
        maxOffersToConsider = 64,
        rolloutDepth = 0,
        thinkBudgetMs = 5
    };

    [Header("Observations (Phase A schema)")]
    public ObservationAuthoring observations = new ObservationAuthoring
    {
        maxCells = 217,
        maxDistance = 16
    };


    // ---- NEW: Control & ML authoring ----
    [Header("Control")]
    public GameConfigHub.ControlMode[] playerControl = new GameConfigHub.ControlMode[4] {
    GameConfigHub.ControlMode.Heuristic,
    GameConfigHub.ControlMode.Heuristic,
    GameConfigHub.ControlMode.Heuristic,
    GameConfigHub.ControlMode.Heuristic
};

    [Header("Bot Policies (for Heuristic seats)")]
    public GameConfigHub.PolicyKind[] playerPolicy = new GameConfigHub.PolicyKind[4] {
        GameConfigHub.PolicyKind.Heuristic,
        GameConfigHub.PolicyKind.Heuristic,
        GameConfigHub.PolicyKind.Heuristic,
        GameConfigHub.PolicyKind.Heuristic
    };

    [System.Serializable]
    public struct DbLoggingAuthoring
    {
        public bool enabled;
        public int simID;
        public string simName;
        public string ruleVersion;
        public string notes;
        public bool useSharedSession;
        public bool transactionalSession;
        [Min(1)] public int batchSize;
        [Min(1)] public int flushIntervalMs;
        [Min(1)] public int maxQueue;
        [Min(0)] public int maxRetries;
        [Min(0)] public int retryBackoffMs;
    }

    [Header("DB Logging Tuning")]
    public DbLoggingAuthoring dbLogging = new DbLoggingAuthoring
    {
        enabled = true,
        simID = 4,
        simName = "Does it work this way though?",
        ruleVersion = "v1",
        notes = "No notes",
        useSharedSession = true,
        transactionalSession = false,
        batchSize = 200,
        flushIntervalMs = 250,
        maxQueue = 10000,
        maxRetries = 3,
        retryBackoffMs = 250
    };

    [System.Serializable]
    public struct DumbGregAuthoring
    {
        [Range(0f, 1f)] public float endTurnAfterFirstPct; // chance to end turn after first action
        [Range(0f, 1f)] public float shootInsteadPct;       // chance to shoot instead within tiers
        [Range(0f, 1f)] public float moveAnotherPct;        // chance to pick second-best move
        [Range(0f, 1f)] public float moveBuildingPct;       // chance to move building instead
        [Range(0f, 1f)] public float createInsteadPct;      // chance to create instead within tiers

        public int seedBase;          // base seed used for RNG (combine with seat)
        public bool seedBySeat;       // if true, actual seed = seedBase + seat
    }

    [Header("Dumb Greg Policy Tuning")]
    public DumbGregAuthoring dumbGreg = new DumbGregAuthoring
    {
        endTurnAfterFirstPct = 0.08f,
        shootInsteadPct = 0.12f,
        moveAnotherPct = 0.10f,
        moveBuildingPct = 0.05f,
        createInsteadPct = 0.10f,
        seedBase = 12345,
        seedBySeat = true
    };

    [Header("ML Behavior Parameters (auto-injected)")]
    public BehaviorParametersAuthoring behaviorParams = new BehaviorParametersAuthoring
    {
        behaviorName = "OmegaPPO",
        useChildSensors = false,
        vectorObservationSize = 21 + 12 * 217, // default for 217-cell board
        actionBranchSize = 64
    };

    public int gamesToRun;
    public bool inspectGame = false;
    public bool useMLAgents = false;


    [System.Serializable]
    public struct MLRewardsAuthoring
    {
        public float rewardWin;
        public float rewardLoss;
        public float rewardDraw;
        public float rewardCaptureVP;
        public float rewardCoreDamage;
        public float moveTowardVpScale;   // multiplied by (distBefore - distAfter)
        public float costPenaltyScale;    // multiplied by normalized cost (0..1)
        public float stepPenalty;         // applied each action
        public float endTurnPenalty;      // additional penalty if EndTurn
    }

    [Header("ML Rewards Tuning")]
    public MLRewardsAuthoring mlRewards = new MLRewardsAuthoring
    {
        rewardWin = 10f,
        rewardLoss = -10f,
        rewardDraw = 0f,
        rewardCaptureVP = 1.0f,
        rewardCoreDamage = 0.5f,
        moveTowardVpScale = 0.0f,
        costPenaltyScale = 0.0f,
        stepPenalty = 0.0f,
        endTurnPenalty = 0.0f
    };


    [System.Serializable]
    public struct AutoSimAuthoring
    {
        [Tooltip("When checked, automatically start a new game when one ends.")]
        public bool autoRestartOnGameOver;
        [Tooltip("Maximum number of games to auto-play. 0 = unlimited.")]
        [Min(0)] public int maxAutoGames;
    }

    [Header("Auto Simulation")]
    public AutoSimAuthoring autoSim = new AutoSimAuthoring { autoRestartOnGameOver = false, maxAutoGames = 1 };



    [System.Serializable]
    public struct BoardAuthoring
    {
        [Range(1, 10)] public byte radius;
        public int invalidId;
        public int victoryPointCellId;      // e.g., center
        public int[] coreCellIdByPlayer; // set per map
        [Tooltip("When enabled, shuffle the 4 core cell ids each game so seats spawn at different cores.")]
        public bool shuffleCoreCellsPerGame;
        [Tooltip("Optional seed for core shuffling. 0 = non-deterministic per match.")]
        public int coreShuffleSeed;
    }

    [Header("Board")]
    public BoardAuthoring board = new BoardAuthoring { radius = 8, invalidId = -1, victoryPointCellId = 108, coreCellIdByPlayer = new int[4], shuffleCoreCellsPerGame = false, coreShuffleSeed = 0 };

    [System.Serializable]
    public struct MatchAuthoring
    {
        public float startingBudgetPerPlayer;
        public int numberOfRounds;
        public int startOfTurnBudgetDecrease;
        public int startCenterVP;
        public int startCoreHp;
    }

    [Header("Match Defaults")]
    public MatchAuthoring match = new MatchAuthoring { startingBudgetPerPlayer = 100f, numberOfRounds = 5, startOfTurnBudgetDecrease = 5, startCenterVP = 5, startCoreHp = 3 };

    [System.Serializable]
    public struct CostAuthoring
    {
        public int baseActionCost;
        public float actionGrowthFactor;
    }

    [Header("Cost Tuning")]
    public CostAuthoring costs = new CostAuthoring { baseActionCost = 10, actionGrowthFactor = 1.5f };

    [System.Serializable]
    public struct RewardAuthoring
    {
        public int budgetBonusForVP;
        public int budgetBonusForCoreDamage;
    }

    [Header("Rewards / Economy")]
    public RewardAuthoring rewards = new RewardAuthoring { budgetBonusForVP = 5, budgetBonusForCoreDamage = 5 };

    [System.Serializable]
    public struct CapsAuthoring
    {
        public int capMaxActionsPerTurn;
        public int capMaxVP;
        public float capMaxBudget;
        public int capMaxVPPool;
        public int capMaxCoreHealth;
    }

    [Header("Caps")]
    public CapsAuthoring caps = new CapsAuthoring { capMaxActionsPerTurn = 30, capMaxVP = 30, capMaxBudget = 150f, capMaxVPPool = 5, capMaxCoreHealth = 3 };

    [System.Serializable]
    public struct PieceLimitAuthoring
    {
        public bool enablePieceLimit;
        [Min(1)] public int maxPiecesPerPlayer;
    }

    [Header("Piece Limits")]
    public PieceLimitAuthoring pieceLimit = new PieceLimitAuthoring { enablePieceLimit = false, maxPiecesPerPlayer = 50 };

    // Config.cs  (inside the class)
    [System.Serializable]
    public struct PlayerConfig
    {
        public string name;
        public bool isAI;
        public bool applyBotSurcharges;
        public bool applyStartOfTurnBudgetDecrease;
        public float startingBudgetOverride;   // < 0 => use global default
        public int team;
    }

    [System.Serializable]
    public struct ObservationAuthoring
    {
        [Min(1)] public int maxCells;     // e.g., 217 (set below)
        [Min(1)] public int maxDistance;  // e.g., 16  (set below)
    }


    [System.Serializable]
    public struct AgentAuthoring
    {
        [Min(1)] public int maxOffersToConsider;  // e.g. 64
        [Min(0)] public int rolloutDepth;         // e.g. 2
        [Min(0)] public int thinkBudgetMs;        // e.g. 5
    }

    [System.Serializable]
    public struct BehaviorParametersAuthoring
    {
        public string behaviorName;
        public bool useChildSensors;
        [Min(1)] public int vectorObservationSize;
        [Min(1)] public int actionBranchSize;
    }

    [System.Serializable]
    public struct PlayerBehaviorConfig
    {
        public ModelAsset modelAsset;                  // Drag/drop imported ONNX (ModelAsset)
        public bool deterministicInference;
        public BehaviorType behaviorType;              // Default | HeuristicOnly | InferenceOnly
    }

    [Header("ML Behavior Overrides (per player)")]
    public PlayerBehaviorConfig[] playerBehaviorOverrides = new PlayerBehaviorConfig[4]
    {
        new PlayerBehaviorConfig { modelAsset = null, deterministicInference = false, behaviorType = BehaviorType.Default },
        new PlayerBehaviorConfig { modelAsset = null, deterministicInference = false, behaviorType = BehaviorType.Default },
        new PlayerBehaviorConfig { modelAsset = null, deterministicInference = false, behaviorType = BehaviorType.Default },
        new PlayerBehaviorConfig { modelAsset = null, deterministicInference = false, behaviorType = BehaviorType.Default }
    };







    [Header("Players")]
    [Range(1, 4)] public int playerCount = 4;
    public PlayerConfig[] players = new PlayerConfig[4] {
    new PlayerConfig{ name="P0", isAI=false, applyBotSurcharges=false, applyStartOfTurnBudgetDecrease=false, startingBudgetOverride=-1, team=0 },
    new PlayerConfig{ name="P1", isAI=false, applyBotSurcharges=false, applyStartOfTurnBudgetDecrease=false, startingBudgetOverride=-1, team=1 },
    new PlayerConfig{ name="P2", isAI=true,  applyBotSurcharges=true,  applyStartOfTurnBudgetDecrease=false, startingBudgetOverride=-1, team=2 },
    new PlayerConfig{ name="P3", isAI=true,  applyBotSurcharges=true,  applyStartOfTurnBudgetDecrease=false, startingBudgetOverride=-1, team=3 },
};



    public GameConfigHub BuildHub()
    {
        short totalCells = (short)(1 + 3 * board.radius * (board.radius + 1));

        int N = Mathf.Clamp(playerCount, 1, 4);

        var p_applyBot = new bool[N];
        var p_applyDec = new bool[N];
        var p_budget = new float[N];
        var p_isAI = new bool[N];
        var p_team = new int[N];
        var p_name = new string[N];

        int mlObsSize = 21 + 12 * observations.maxCells;

        for (int i = 0; i < N; i++)
        {
            var pc = (players != null && i < players.Length) ? players[i] : default;

            p_applyBot[i] = pc.applyBotSurcharges;
            p_applyDec[i] = pc.applyStartOfTurnBudgetDecrease;
            p_budget[i] = (pc.startingBudgetOverride >= 0f) ? pc.startingBudgetOverride : match.startingBudgetPerPlayer;

            p_isAI[i] = pc.isAI;
            p_team[i] = pc.team;
            p_name[i] = string.IsNullOrEmpty(pc.name) ? $"P{i}" : pc.name;
        }



        return new GameConfigHub(
            board_radius: board.radius,
            board_totalCells: totalCells,
            board_invalidCellId: board.invalidId,
            board_vpCellId: board.victoryPointCellId,
            board_coreCellIdByPlayer: (int[])board.coreCellIdByPlayer.Clone(),

            match_startingBudgetPerPlayer: match.startingBudgetPerPlayer,
            match_numberOfRounds: match.numberOfRounds,
            match_startOfTurnBudgetDecrease: match.startOfTurnBudgetDecrease,
            match_startCenterVP: match.startCenterVP,
            match_startCoreHp: match.startCoreHp,

            cost_baseActionCost: costs.baseActionCost,
            cost_actionGrowthFactor: costs.actionGrowthFactor,

            reward_budgetBonusForVP: rewards.budgetBonusForVP,
            reward_budgetBonusForCoreDamage: rewards.budgetBonusForCoreDamage,

            cap_maxActionsPerTurn: caps.capMaxActionsPerTurn,
            cap_maxVP: caps.capMaxVP,
            cap_maxBudget: caps.capMaxBudget,
            cap_maxVPPool: caps.capMaxVPPool,
            cap_maxCoreHealth: caps.capMaxCoreHealth,
            pieceLimitEnabled: pieceLimit.enablePieceLimit,
            pieceLimitPerPlayer: pieceLimit.maxPiecesPerPlayer,
            player_count: N,
                player_applyBotSurcharges: p_applyBot,
                player_applyStartOfTurnBudgetDecrease: p_applyDec,
                player_startingBudget: p_budget,
                player_isAI: p_isAI,
                player_team: p_team,
                player_name: p_name,
                obs_maxCells: observations.maxCells,
                obs_maxDistance: observations.maxDistance,
                agent: new GameConfigHub.AgentConfig(
                agent.maxOffersToConsider,
                agent.rolloutDepth,
                agent.thinkBudgetMs
     ),
                playerControl: (playerControl != null && playerControl.Length >= N)
                ? playerControl[..N]
                : new GameConfigHub.ControlMode[N],
                playerPolicy: (playerPolicy != null && playerPolicy.Length >= N)
                ? playerPolicy[..N]
                : new GameConfigHub.PolicyKind[N],
                enableMLAgents: useMLAgents,
                mlObservationSize: mlObsSize,
                mlBehavior: new GameConfigHub.BehaviorParametersConfig(
                behaviorParams.behaviorName,
                behaviorParams.useChildSensors,
                mlObsSize,                       // 21 + 12 * observations.maxCells
                agent.maxOffersToConsider        // single discrete branch size
             )
        );
    }
}
