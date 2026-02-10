// Assets/Scripts/Core/Config.cs
using UnityEngine;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;
using Action = Game.Core.Action;
using System.Collections.Generic;



[CreateAssetMenu(fileName = "Config", menuName = "Game/Config", order = 0)]
public sealed class Config : ScriptableObject
{
    [Header("----------Players Config-----------")]
    public bool useMLAgents = false;

    [Header("How Mnay Players")]
    [Range(1, 4)] public int playerCount = 4;

    [Header("What Controls Players")]
    public Info.ControlMode[] playerControl = new Info.ControlMode[4] {
    Info.ControlMode.DumbGreg,
    Info.ControlMode.DumbGreg,
    Info.ControlMode.DumbGreg,
    Info.ControlMode.DumbGreg
    };

    [Header("Players GamePlay")]
    public PlayerConfig[] players = new PlayerConfig[4] {
    new PlayerConfig{ name="P0", isAI=false, applyBotSurcharges=false, applyStartOfTurnBudgetDecrease=false, startingBudgetOverride=-1, team=0 },
    new PlayerConfig{ name="P1", isAI=false, applyBotSurcharges=false, applyStartOfTurnBudgetDecrease=false, startingBudgetOverride=-1, team=1 },
    new PlayerConfig{ name="P2", isAI=true,  applyBotSurcharges=true,  applyStartOfTurnBudgetDecrease=false, startingBudgetOverride=-1, team=2 },
    new PlayerConfig{ name="P3", isAI=true,  applyBotSurcharges=true,  applyStartOfTurnBudgetDecrease=false, startingBudgetOverride=-1, team=3 },
    };

    // [Header("If is, What Dumb Bot")]
    // public Info.PolicyKind[] playerPolicy = new Info.PolicyKind[4] {
    //     Info.PolicyKind.Heuristic,
    //     Info.PolicyKind.Heuristic,
    //     Info.PolicyKind.Heuristic,
    //     Info.PolicyKind.Heuristic
    // };

    [Header("Dumb Policy Tuning")]
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

    public DumbBobActionProbilitys[] dumbBob;
    public DumbBobAuthoring dumbBobAuthoring;


    public Dictionary<Piece.AbilityKind, int>[] giveMeDumbBobDictionary()
    {
        int enumCount = System.Enum.GetValues(typeof(Piece.AbilityKind)).Length;
        var actionWeights = new Dictionary<Piece.AbilityKind, int>[enumCount];

        for (int aPath = 0; aPath < dumbBob.Length; aPath++)
        {
            int slot = (int)dumbBob[aPath].kind;
            // allocate if needed
            if (actionWeights[slot] == null) actionWeights[slot] = new Dictionary<Piece.AbilityKind, int>();
            int count = dumbBob[aPath].WeightKey.Length;
            for (int i = 0; i < count; i++) actionWeights[slot][dumbBob[aPath].WeightKey[i]] = dumbBob[aPath].weight[i];
        }
        return actionWeights;
    }



    [Header("---------ML Settings---------")]
    [Header("ML Behavior Parameters (auto-injected)")]
    public BehaviorParametersAuthoring behaviorParams = new BehaviorParametersAuthoring
    {
        behaviorName = "OmegaPPO",
        useChildSensors = false,
        vectorObservationSize = 21 + 12 * 217, // default for 217-cell board
        actionBranchSize = 64
    };

    [Header("Caps")]
    public CapsAuthoring caps = new CapsAuthoring { capMaxActionsPerTurn = 30, capMaxVP = 30, capMaxBudget = 150f, capMaxVPPool = 5, capMaxPieceHP = 8, capMaxCoreHealth = 3 };

    [Header("Agent (global)")]
    public AgentAuthoring agent = new AgentAuthoring
    {
        maxOffersToConsider = 64,
        rolloutDepth = 0,
        thinkBudgetMs = 5
    };

    // [Header("Observations (Phase A schema)")]
    // public ObservationAuthoring observations = new ObservationAuthoring //to do- need to could get rid of this, to make it dynamic
    // {
    //     maxCells = 217,
    //     maxDistance = 16
    // };

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

    [Header("ML Uses Frozen Brain? (per player)")]
    public PlayerBehaviorConfig[] playerBehaviorOverrides = new PlayerBehaviorConfig[4]
    {
        new PlayerBehaviorConfig { modelAsset = null, deterministicInference = false, behaviorType = BehaviorType.Default },
        new PlayerBehaviorConfig { modelAsset = null, deterministicInference = false, behaviorType = BehaviorType.Default },
        new PlayerBehaviorConfig { modelAsset = null, deterministicInference = false, behaviorType = BehaviorType.Default },
        new PlayerBehaviorConfig { modelAsset = null, deterministicInference = false, behaviorType = BehaviorType.Default }
    };



    [Header("Auto Simulation")]
    public AutoSimAuthoring autoSim = new AutoSimAuthoring { autoRestartOnGameOver = false, maxAutoGames = 1 };
    public int gamesToRun;
    public bool inspectGame = false;

    [Header("---------Game Play---------")]

    [Header("Match Defaults")]
    public MatchAuthoring match = new MatchAuthoring { startingBudgetPerRound = new float[4], numberOfRounds = 5, startOfTurnBudgetDecrease = 5, startCenterVP = 5, startCoreHp = 3 };

    [Header("Cost Tuning")]
    public CostAuthoring costs = new CostAuthoring { baseActionCost = 10, actionGrowthFactor = 1.5f };

    [Header("Budget Rewards")]
    public RewardAuthoring rewards = new RewardAuthoring { budgetBonusForVP = 5, budgetBonusForCoreDamage = 5 };

    [Header("Board")]
    public BoardAuthoring board = new BoardAuthoring { radius = 8, invalidId = -1, victoryPointCellId = 108, firstCoreCellAxialByPlayer = new short[4], secondCoreCellAxialByPlayer = new short[4], shuffleCoreCellsPerGame = false, coreShuffleSeed = 0 };

    [Header("Wall config")]
    public bool ContiguousWalls = false;
    public bool AdjecentWallContiguous = false;

    public bool AbilitysCanSeperatePiecesWithWalls = false;
    public bool ConnectorsInvalidIfBoarderingEdge = true;


    // ---- NEW: Control & ML authoring ----
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






    // public GameConfigHub BuildHub()
    // {
    //     short totalCells = (short)(1 + 3 * board.radius * (board.radius + 1));

    //     int N = Mathf.Clamp(playerCount, 1, 4);

    //     var p_applyBot = new bool[N];
    //     var p_applyDec = new bool[N];
    //     var p_budget = new float[N];
    //     var p_isAI = new bool[N];
    //     var p_team = new int[N];
    //     var p_name = new string[N];

    //     int mlObsSize = 21 + 12 * observations.maxCells;

    //     for (int i = 0; i < N; i++)
    //     {
    //         var pc = (players != null && i < players.Length) ? players[i] : default;

    //         p_applyBot[i] = pc.applyBotSurcharges;
    //         p_applyDec[i] = pc.applyStartOfTurnBudgetDecrease;

    //         p_isAI[i] = pc.isAI;
    //         p_team[i] = pc.team;
    //         p_name[i] = string.IsNullOrEmpty(pc.name) ? $"P{i}" : pc.name;
    //     }



    //     return new GameConfigHub(
    //         AdjecentWallContiguous: AdjecentWallContiguous,
    //         ContiguousWalls: ContiguousWalls,
    //         board_radius: board.radius,
    //         board_totalCells: totalCells,
    //         board_invalidCellId: board.invalidId,
    //         // board_vpCellId: board.victoryPointCellId,
    //         board_vpAxial: board.VpAxial,
    //         // board_coreCellIdByPlayer: (int[])board.coreCellIdByPlayer.Clone(),
    //         board_firstCoreCellAxialByPlayer: board.firstCoreCellAxialByPlayer,
    //         board_secondCoreCellAxialByPlayer:board.secondCoreCellAxialByPlayer,
    //         match_startingBudgetPerRound: (float[])match.startingBudgetPerRound.Clone(),
    //         match_numberOfRounds: match.numberOfRounds,
    //         match_startOfTurnBudgetDecrease: match.startOfTurnBudgetDecrease,
    //         match_startCenterVP: match.startCenterVP,
    //         match_startCoreHp: match.startCoreHp,

    //         cost_baseActionCost: costs.baseActionCost,
    //         cost_actionGrowthFactor: costs.actionGrowthFactor,

    //         reward_budgetBonusForVP: rewards.budgetBonusForVP,
    //         reward_budgetBonusForCoreDamage: rewards.budgetBonusForCoreDamage,

    //         cap_maxActionsPerTurn: caps.capMaxActionsPerTurn,
    //         cap_maxVP: caps.capMaxVP,
    //         cap_maxBudget: caps.capMaxBudget,
    //         cap_maxVPPool: caps.capMaxVPPool,
    //         cap_maxCoreHealth: caps.capMaxCoreHealth,
    //         player_count: N,
    //             player_applyBotSurcharges: p_applyBot,
    //             player_applyStartOfTurnBudgetDecrease: p_applyDec,
    //             player_startingBudget: p_budget,
    //             player_isAI: p_isAI,
    //             player_team: p_team,
    //             player_name: p_name,
    //             obs_maxCells: observations.maxCells,
    //             obs_maxDistance: observations.maxDistance,
    //             agent: new GameConfigHub.AgentConfig(
    //             agent.maxOffersToConsider,
    //             agent.rolloutDepth,
    //             agent.thinkBudgetMs
    //  ),
    //             playerControl: (playerControl != null && playerControl.Length >= N)
    //             ? playerControl[..N]
    //             : new GameConfigHub.ControlMode[N],
    //             playerPolicy: (playerPolicy != null && playerPolicy.Length >= N)
    //             ? playerPolicy[..N]
    //             : new GameConfigHub.PolicyKind[N],
    //             enableMLAgents: useMLAgents,
    //             mlObservationSize: mlObsSize,
    //             mlBehavior: new GameConfigHub.BehaviorParametersConfig(
    //             behaviorParams.behaviorName,
    //             behaviorParams.useChildSensors,
    //             mlObsSize,                       // 21 + 12 * observations.maxCells
    //             agent.maxOffersToConsider        // single discrete branch size
    //          )
    //     );
    // }


}



#region structs

[System.Serializable]
public struct PlayerBehaviorConfig
{
    public ModelAsset modelAsset;                  // Drag/drop imported ONNX (ModelAsset)
    public bool deterministicInference;
    public BehaviorType behaviorType;              // Default | HeuristicOnly | InferenceOnly
}


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

[System.Serializable]
public struct DumbBobActionProbilitys
{
    public Piece.AbilityKind kind;
    public Piece.AbilityKind[] WeightKey;
    public int[] weight;
}
[System.Serializable]
public struct DumbBobAuthoring
{
    public Piece.AbilityKind[] Priority;
    public int[] PayOutAimByRound;
}





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

[System.Serializable]
public struct AutoSimAuthoring
{
    [Tooltip("When checked, automatically start a new game when one ends.")]
    public bool autoRestartOnGameOver;
    [Tooltip("Maximum number of games to auto-play. 0 = unlimited.")]
    [Min(0)] public int maxAutoGames;
}

[System.Serializable]
public struct BoardAuthoring
{
    [Range(1, 10)] public byte radius;
    public int invalidId;
    public int victoryPointCellId;      // e.g., center
                                        // public int[] coreCellIdByPlayer; // set per map
    public short[] firstCoreCellAxialByPlayer;
    public short[] secondCoreCellAxialByPlayer;
    public short firstVpAxial;
    public short secoundVpAxial;
    public (short q, short r) VpAxial => (firstVpAxial, secoundVpAxial);

    [Tooltip("When enabled, shuffle the 4 core cell ids each game so seats spawn at different cores.")]
    public bool shuffleCoreCellsPerGame;
    [Tooltip("Optional seed for core shuffling. 0 = non-deterministic per match.")]
    public int coreShuffleSeed;
}

[System.Serializable]
public struct MatchAuthoring
{
    public float[] startingBudgetPerRound;
    public int numberOfRounds;
    public int startOfTurnBudgetDecrease;
    public int startCenterVP;
    public int startCoreHp;
}

[System.Serializable]
public struct CostAuthoring
{
    public int baseActionCost;
    public float actionGrowthFactor;
}

[System.Serializable]
public struct RewardAuthoring
{
    public int budgetBonusForVP;
    public int budgetBonusForCoreDamage;
}

[System.Serializable]
public struct CapsAuthoring
{
    public int capMaxActionsPerTurn;
    public int capMaxVP;
    public float capMaxBudget;
    public int capMaxVPPool;
    public int capMaxPieceHP;
    public int capMaxCoreHealth;
}

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

// [System.Serializable]
// public struct ObservationAuthoring
// {
//     [Min(1)] public int maxCells;     // e.g., 217 (set below)
//     [Min(1)] public int maxDistance;  // e.g., 16  (set below)
// }


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
#endregion

