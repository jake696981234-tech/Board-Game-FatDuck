using UnityEngine;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;

public static class Info
{
    // [Header("General Players Config")]
    public static readonly bool useMLAgents;

    // [Header("Player Control")]
    public static readonly int playerCount;

    // [Header("What Controls Player")]
    public static readonly ControlMode[] playerControl;

    // [Header("If is, What Dumb Bot")]
    public static readonly PolicyKind[] playerPolicy;

    // [Header("Dumb Greg Policy Tuning")]
    public static readonly DumbGregAuthoring dumbGreg;

    // [Header("ML Settings")]
    // [Header("ML Behavior Parameters (auto-injected)")]
    public static readonly string behaviorName;
    public static readonly bool useChildSensors;
    public static readonly int vectorObservationSize;
    public static readonly int actionBranchSize;

    // [Header("Caps")]
        public static readonly int capMaxActionsPerTurn;
        public static readonly int capMaxVP;
        public static readonly float capMaxBudget;
        public static readonly int capMaxVPPool;
        public static readonly int capMaxCoreHealth;
    // [Header("Agent (global)")]
    public static readonly int maxOffersToConsider;  // e.g. 64
    public static readonly int rolloutDepth;         // e.g. 2
    public static readonly int thinkBudgetMs;        // e.g. 5

    // [Header("Observations (Phase A schema)")]
    public static readonly int maxCells;     // e.g., 217 (set below)
    public static readonly int maxDistance;  // e.g., 16  (set below)

    // [Header("ML Rewards Tuning")]
    public static readonly float rewardWin;
    public static readonly float rewardLoss;
    public static readonly float rewardDraw;
    public static readonly float rewardCaptureVP;
    public static readonly float rewardCoreDamage;
    public static readonly float moveTowardVpScale;   // multiplied by (distBefore - distAfter)
    public static readonly float costPenaltyScale;    // multiplied by normalized cost (0..1)
    public static readonly float stepPenalty;         // applied each action
    public static readonly float endTurnPenalty;      // additional penalty if EndTurn
    // [Header("ML Uses Frozen Brain? (per player)")]
    public static readonly PlayerBehaviorConfig[] playerBehaviorOverrides;


    // [Header("Auto Simulation")]
    public static readonly bool autoRestartOnGameOver;
    public static readonly int maxAutoGames;

    public static readonly int gamesToRun;
    public static readonly bool inspectGame;

    // [Header("Game Play")]
    // [Header("Players")]
    public static readonly PlayerConfig[] Players;



    // [Header("Match Defaults")]
    public static readonly float[] startingBudgetPerRound;
    public static readonly int numberOfRounds;
    public static readonly int startOfTurnBudgetDecrease;
    public static readonly int startCenterVP;
    public static readonly int startCoreHp;

    // [Header("Cost Tuning")]
    public static readonly int baseActionCost;
    public static readonly float actionGrowthFactor;


    // [Header("Rewards / Economy")]
    public static readonly int budgetBonusForVP;
    public static readonly int budgetBonusForCoreDamage;


    // [Header("Board")]
    public static readonly byte radius;
    public static readonly short totalCells;
    public static readonly int invalidId;
    public static readonly int victoryPointCellId;      // e.g., center
    public static readonly int[] coreCellIdByPlayer; // set per map
    public static readonly short[] firstCoreCellAxialByPlayer;
    public static readonly short[] secondCoreCellAxialByPlayer;
    public static readonly (short q, short r)[] PlayerCoreAxialCord;
    public static readonly short firstVpAxial;
    public static readonly short secoundVpAxial;
    
    
    public static (short q, short r) VpAxial => (firstVpAxial, secoundVpAxial);

    public static readonly bool shuffleCoreCellsPerGame;
    public static readonly int coreShuffleSeed;


    // [Header("Wall config")]
    public static readonly bool ContiguousWalls;
    public static readonly bool AdjecentWallContiguous;

    // ---- NEW: Control & ML authoring ----
    // [Header("DB Logging Tuning")]
    public static readonly DbLoggingAuthoring dbLogging;

    static Info()
    {
        var config = Resources.Load<Config>("Config");

        useMLAgents = config.useMLAgents;
        playerCount = config.playerCount;
        playerControl = (ControlMode[])config.playerControl.Clone();
        playerPolicy = (PolicyKind[])config.playerPolicy.Clone();
        dumbGreg = config.dumbGreg;

        behaviorName = config.behaviorParams.behaviorName;
        useChildSensors = config.behaviorParams.useChildSensors;
        vectorObservationSize = config.behaviorParams.vectorObservationSize;
        actionBranchSize = config.behaviorParams.actionBranchSize;

        capMaxActionsPerTurn = config.caps.capMaxActionsPerTurn;
        capMaxVP = config.caps.capMaxVP;
        capMaxBudget = config.caps.capMaxBudget;
        capMaxVPPool = config.caps.capMaxVPPool;
        capMaxCoreHealth = config.caps.capMaxCoreHealth;

        maxOffersToConsider = config.agent.maxOffersToConsider;
        rolloutDepth = config.agent.rolloutDepth;
        thinkBudgetMs = config.agent.thinkBudgetMs;

        maxCells = config.observations.maxCells;
        maxDistance = config.observations.maxDistance;

        rewardWin = config.mlRewards.rewardWin;
        rewardLoss = config.mlRewards.rewardLoss;
        rewardDraw = config.mlRewards.rewardDraw;
        rewardCaptureVP = config.mlRewards.rewardCaptureVP;
        rewardCoreDamage = config.mlRewards.rewardCoreDamage;
        moveTowardVpScale = config.mlRewards.moveTowardVpScale;
        costPenaltyScale = config.mlRewards.costPenaltyScale;
        stepPenalty = config.mlRewards.stepPenalty;
        endTurnPenalty = config.mlRewards.endTurnPenalty;

        playerBehaviorOverrides = (PlayerBehaviorConfig[])config.playerBehaviorOverrides.Clone();

        autoRestartOnGameOver = config.autoSim.autoRestartOnGameOver;
        maxAutoGames = config.autoSim.maxAutoGames;

        gamesToRun = config.gamesToRun;
        inspectGame = config.inspectGame;

        Players = (PlayerConfig[])config.players.Clone();

        startingBudgetPerRound = (float[])config.match.startingBudgetPerRound.Clone();
        numberOfRounds = config.match.numberOfRounds;
        startOfTurnBudgetDecrease = config.match.startOfTurnBudgetDecrease;
        startCenterVP = config.match.startCenterVP;
        startCoreHp = config.match.startCoreHp;

        baseActionCost = config.costs.baseActionCost;
        actionGrowthFactor = config.costs.actionGrowthFactor;

        budgetBonusForVP = config.rewards.budgetBonusForVP;
        budgetBonusForCoreDamage = config.rewards.budgetBonusForCoreDamage;

        radius = config.board.radius;
        totalCells = (short)(1 + 3 * radius * (radius + 1));
        invalidId = config.board.invalidId;
        victoryPointCellId = config.board.victoryPointCellId;
        coreCellIdByPlayer = (int[])config.board.coreCellIdByPlayer.Clone();
        firstCoreCellAxialByPlayer = (short[])config.board.firstCoreCellAxialByPlayer.Clone();
        secondCoreCellAxialByPlayer = (short[])config.board.secondCoreCellAxialByPlayer.Clone();
        firstVpAxial = config.board.firstVpAxial;
        secoundVpAxial = config.board.secoundVpAxial;
        
        PlayerCoreAxialCord =  new (short q, short r)[]
        {
            (firstCoreCellAxialByPlayer[0], secondCoreCellAxialByPlayer[0]),
            (firstCoreCellAxialByPlayer[1], secondCoreCellAxialByPlayer[1]),
            (firstCoreCellAxialByPlayer[2], secondCoreCellAxialByPlayer[2]),
            (firstCoreCellAxialByPlayer[3], secondCoreCellAxialByPlayer[3]),
        };
        shuffleCoreCellsPerGame = config.board.shuffleCoreCellsPerGame;
        coreShuffleSeed = config.board.coreShuffleSeed;

        ContiguousWalls = config.ContiguousWalls;
        AdjecentWallContiguous = config.AdjecentWallContiguous;
        dbLogging = config.dbLogging;
    }

    public enum ControlMode : byte { Human = 0, DumbBot = 1, ML = 2 }
    public enum PolicyKind : byte { Heuristic = 0, DumbGreg = 1 }
}
