using UnityEngine;

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
    public static readonly BehaviorParametersAuthoring behaviorParams;

    // [Header("Caps")]
    public static readonly CapsAuthoring caps;

    // [Header("Agent (global)")]
    public static readonly AgentAuthoring agent;

    // [Header("Observations (Phase A schema)")]
    public static readonly ObservationAuthoring observations;

    // [Header("ML Rewards Tuning")]
    public static readonly MLRewardsAuthoring mlRewards;

    // [Header("ML Uses Frozen Brain? (per player)")]
    public static readonly PlayerBehaviorConfig[] playerBehaviorOverrides;

    // [Header("Auto Simulation")]
    public static readonly AutoSimAuthoring autoSim;
    public static readonly int gamesToRun;
    public static readonly bool inspectGame;

    // [Header("Game Play")]
    // [Header("Players")]
    public static readonly PlayerConfig[] players;

    // [Header("Match Defaults")]
    public static readonly MatchAuthoring match;

    // [Header("Cost Tuning")]
    public static readonly CostAuthoring costs;

    // [Header("Rewards / Economy")]
    public static readonly RewardAuthoring rewards;

    // [Header("Board")]
    public static readonly BoardAuthoring board;

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
        behaviorParams = config.behaviorParams;
        caps = config.caps;
        agent = config.agent;
        observations = config.observations;
        mlRewards = config.mlRewards;
        playerBehaviorOverrides = (PlayerBehaviorConfig[])config.playerBehaviorOverrides.Clone();
        autoSim = config.autoSim;
        gamesToRun = config.gamesToRun;
        inspectGame = config.inspectGame;
        players = (PlayerConfig[])config.players.Clone();
        match = new MatchAuthoring
        {
            startingBudgetPerRound = (float[])config.match.startingBudgetPerRound.Clone(),
            numberOfRounds = config.match.numberOfRounds,
            startOfTurnBudgetDecrease = config.match.startOfTurnBudgetDecrease,
            startCenterVP = config.match.startCenterVP,
            startCoreHp = config.match.startCoreHp
        };
        costs = config.costs;
        rewards = config.rewards;
        board = new BoardAuthoring
        {
            radius = config.board.radius,
            invalidId = config.board.invalidId,
            victoryPointCellId = config.board.victoryPointCellId,
            coreCellIdByPlayer = (int[])config.board.coreCellIdByPlayer.Clone(),
            firstCoreCellAxialByPlayer = (short[])config.board.firstCoreCellAxialByPlayer.Clone(),
            secondCoreCellAxialByPlayer = (short[])config.board.secondCoreCellAxialByPlayer.Clone(),
            firstVpAxial = config.board.firstVpAxial,
            secoundVpAxial = config.board.secoundVpAxial,
            shuffleCoreCellsPerGame = config.board.shuffleCoreCellsPerGame,
            coreShuffleSeed = config.board.coreShuffleSeed
        };
        ContiguousWalls = config.ContiguousWalls;
        AdjecentWallContiguous = config.AdjecentWallContiguous;
        dbLogging = config.dbLogging;
    }

    public enum ControlMode : byte { Human = 0, DumbBot = 1, ML = 2 }
    public enum PolicyKind : byte { Heuristic = 0, DumbGreg = 1 }
}