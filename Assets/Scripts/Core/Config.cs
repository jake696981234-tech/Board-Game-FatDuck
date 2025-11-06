// Assets/Scripts/Core/Config.cs
using UnityEngine;


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

    [Header("ML Behavior Parameters (auto-injected)")]
    public BehaviorParametersAuthoring behaviorParams = new BehaviorParametersAuthoring
    {
        behaviorName = "OmegaPPO",
        useChildSensors = false,
        vectorObservationSize = 21 + 12 * 217, // default for 217-cell board
        actionBranchSize = 64
    };
    

    public bool useMLAgents = false;


    [Header("Auto Simulation")]
    [Tooltip("When checked, automatically start a new game when one ends.")]
    public bool autoRestartOnGameOver = false;

    [Tooltip("Maximum number of games to auto-play. 0 = unlimited.")]
    [Min(0)] public int maxAutoGames = 1;



    [Header("Board")]
    [Range(1, 10)] public byte boardRadius = 8;
    public int boardInvalidId = -1;
    public int victoryPointCellId = 0;      // e.g., center
    public int[] coreCellIdByPlayer = new int[4]; // set per map

    [Header("Match Defaults")]
    public float startingBudgetPerPlayer = 100f;
    public int numberOfRounds = 5;
    public int startOfTurnBudgetDecrease = 5;
    public int startCenterVP = 5;
    public int startCoreHp = 3;

    [Header("Cost Tuning")]
    public int   baseActionCost = 10;
    public float actionGrowthFactor = 1.5f;

    [Header("Rewards / Economy")]
    public int budgetBonusForVP = 5;
    public int budgetBonusForCoreDamage = 5;

    [Header("Caps")]
    public int   capMaxActionsPerTurn = 30;
    public int   capMaxVP = 30;
    public float capMaxBudget = 150f;
    public int   capMaxVPPool = 5;
    public int capMaxCoreHealth = 3;

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

    





    [Header("Players")]
    [Range(1,4)] public int playerCount = 4;
    public PlayerConfig[] players = new PlayerConfig[4] {
    new PlayerConfig{ name="P0", isAI=false, applyBotSurcharges=false, applyStartOfTurnBudgetDecrease=false, startingBudgetOverride=-1, team=0 },
    new PlayerConfig{ name="P1", isAI=false, applyBotSurcharges=false, applyStartOfTurnBudgetDecrease=false, startingBudgetOverride=-1, team=1 },
    new PlayerConfig{ name="P2", isAI=true,  applyBotSurcharges=true,  applyStartOfTurnBudgetDecrease=false, startingBudgetOverride=-1, team=2 },
    new PlayerConfig{ name="P3", isAI=true,  applyBotSurcharges=true,  applyStartOfTurnBudgetDecrease=false, startingBudgetOverride=-1, team=3 },
};



    public GameConfigHub BuildHub()
    {
        short totalCells = (short)(1 + 3 * boardRadius * (boardRadius + 1));

        int N = Mathf.Clamp(playerCount, 1, 4);

        var p_applyBot  = new bool[N];
        var p_applyDec  = new bool[N];
        var p_budget    = new float[N];
        var p_isAI      = new bool[N];
        var p_team      = new int[N];
        var p_name = new string[N];
        
        int mlObsSize = 21 + 12 * observations.maxCells;

        for (int i = 0; i < N; i++)
        {
            var pc = (players != null && i < players.Length) ? players[i] : default;

            p_applyBot[i] = pc.applyBotSurcharges;
            p_applyDec[i] = pc.applyStartOfTurnBudgetDecrease;
            p_budget[i] = (pc.startingBudgetOverride >= 0f) ? pc.startingBudgetOverride : startingBudgetPerPlayer;

            p_isAI[i] = pc.isAI;
            p_team[i] = pc.team;
            p_name[i] = string.IsNullOrEmpty(pc.name) ? $"P{i}" : pc.name;
        }



        return new GameConfigHub(
            board_radius: boardRadius,
            board_totalCells: totalCells,
            board_invalidCellId: boardInvalidId,
            board_vpCellId: victoryPointCellId,
            board_coreCellIdByPlayer: (int[])coreCellIdByPlayer.Clone(),

            match_startingBudgetPerPlayer: startingBudgetPerPlayer,
            match_numberOfRounds: numberOfRounds,
            match_startOfTurnBudgetDecrease: startOfTurnBudgetDecrease,
            match_startCenterVP: startCenterVP,
            match_startCoreHp: startCoreHp,

            cost_baseActionCost: baseActionCost,
            cost_actionGrowthFactor: actionGrowthFactor,

            reward_budgetBonusForVP: budgetBonusForVP,
            reward_budgetBonusForCoreDamage: budgetBonusForCoreDamage,

            cap_maxActionsPerTurn: capMaxActionsPerTurn,
            cap_maxVP: capMaxVP,
            cap_maxBudget: capMaxBudget,
            cap_maxVPPool: capMaxVPPool,
            cap_maxCoreHealth: capMaxCoreHealth,
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
