// Assets/Scripts/Core/GameConfigHub.cs
using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct GameConfigHub
{
    public readonly BehaviorParametersConfig mlBehavior;

    public readonly struct BehaviorParametersConfig
    {
        public readonly string name;
        public readonly bool useChildSensors;
        public readonly int obsSize;
        public readonly int actionBranchSize;

        public BehaviorParametersConfig(string name, bool useChildSensors, int obsSize, int actionBranchSize)
        {
            this.name = name;
            this.useChildSensors = useChildSensors;
            this.obsSize = obsSize;
            this.actionBranchSize = actionBranchSize;
        }
    }


    // GameConfigHub.cs  (inside the struct)
    public readonly int player_count;
    public readonly bool[] player_applyBotSurcharges;             // len = player_count
    public readonly bool[] player_applyStartOfTurnBudgetDecrease; // len = player_count
    public readonly float[] player_startingBudget;                 // len = player_count

    // Optional (handy for UI/AI wiring; also len = player_count)
    public readonly bool[] player_isAI;
    public readonly int[] player_team;
    public readonly string[] player_name;



    // --- Board topology & anchors (cell IDs only; no hex at runtime) ---
    public readonly byte board_radius;            // R
    public readonly short board_totalCells;        // 1 + 3R(R+1)
    public readonly int board_invalidCellId;     // usually -1
    public readonly int board_vpCellId;          // e.g., center cell id
    public readonly int[] board_coreCellIdByPlayer;// len = playerCount

    // --- Match defaults (GameState uses these to seed live counters) ---
    public readonly float match_startingBudgetPerPlayer;
    public readonly int match_numberOfRounds;
    public readonly int match_startOfTurnBudgetDecrease;
    public readonly int match_startCenterVP;
    public readonly int match_startCoreHp;

    // --- Cost tuning (CostEngine) ---
    public readonly int cost_baseActionCost;
    public readonly float cost_actionGrowthFactor;

    // --- Rewards/Economy (EndRound payout) ---
    public readonly int reward_budgetBonusForVP;
    public readonly int reward_budgetBonusForCoreDamage;

    // --- Caps (clamping + agent normalization) ---
    public readonly int cap_maxActionsPerTurn;
    public readonly int cap_maxVP;
    public readonly float cap_maxBudget;
    public readonly int cap_maxVPPool;
    public readonly int cap_maxCoreHealth;
    public readonly bool pieceLimitEnabled;
    public readonly int pieceLimitPerPlayer;

    // NEW: Observation schema constants (training-time or fixed per build)
    // Used for building fixed-length observation vectors with zero padding.
    public readonly int obs_maxCells;     // e.g., 217 for R=8; or your chosen upper bound
    public readonly int obs_maxDistance;  // e.g., 2 * board_radius for safe normalization

    // NEW: agent config (global, immutable)
    public readonly AgentConfig agent;

    // ---- CONTROL & ML (NEW) ----
    public enum ControlMode : byte { Human = 0, Heuristic = 1, ML = 2 }

    // Bot policy kinds for heuristic seats
    public enum PolicyKind : byte { Heuristic = 0, DumbGreg = 1 }

    // Per-seat control mode; length = player_count
    public readonly ControlMode[] playerControl;
    // Per-seat bot policy (used when ControlMode == Heuristic); length = player_count
    public readonly PolicyKind[] playerPolicy;

    // Global ML flag (enable/disable ML Agents in this build/scene)
    public readonly bool enableMLAgents;

    // Fixed observation size for Phase A: 21 + 12 * obs_maxCells
    public readonly int mlObservationSize;


    public readonly struct AgentConfig
    {
        public readonly int maxOffersToConsider;
        public readonly int rolloutDepth;
        public readonly int thinkBudgetMs;

        public AgentConfig(int maxOffersToConsider, int rolloutDepth, int thinkBudgetMs)
        {
            this.maxOffersToConsider = maxOffersToConsider;
            this.rolloutDepth = rolloutDepth;
            this.thinkBudgetMs = thinkBudgetMs;
        }
    }




    public GameConfigHub(
        byte board_radius,
        short board_totalCells,
        int board_invalidCellId,
        int board_vpCellId,
        int[] board_coreCellIdByPlayer,
        float match_startingBudgetPerPlayer,
        int match_numberOfRounds,
        int match_startOfTurnBudgetDecrease,
        int match_startCenterVP,
        int match_startCoreHp,
        int cost_baseActionCost,
        float cost_actionGrowthFactor,
        int reward_budgetBonusForVP,
        int reward_budgetBonusForCoreDamage,
        int cap_maxActionsPerTurn,
        int cap_maxVP,
        float cap_maxBudget,
        int cap_maxVPPool,
        int cap_maxCoreHealth,
        bool pieceLimitEnabled,
        int pieceLimitPerPlayer,
        int player_count,
        bool[] player_applyBotSurcharges,             // len = player_count
        bool[] player_applyStartOfTurnBudgetDecrease, // len = player_count
        float[] player_startingBudget,                 // len = player_count
        bool[] player_isAI,
        int[] player_team,
        string[] player_name,
        // NEW: observation schema constants
        int obs_maxCells,
        int obs_maxDistance,
        AgentConfig agent,
        // ---- CONTROL & ML (NEW) ----
        ControlMode[] playerControl,
        PolicyKind[] playerPolicy,
        bool enableMLAgents,
        int mlObservationSize,
        BehaviorParametersConfig mlBehavior
    )
    {
        this.board_radius = board_radius;
        this.board_totalCells = board_totalCells;
        this.board_invalidCellId = board_invalidCellId;
        this.board_vpCellId = board_vpCellId;
        this.board_coreCellIdByPlayer = board_coreCellIdByPlayer;

        this.match_startingBudgetPerPlayer = match_startingBudgetPerPlayer;
        this.match_numberOfRounds = match_numberOfRounds;
        this.match_startOfTurnBudgetDecrease = match_startOfTurnBudgetDecrease;
        this.match_startCenterVP = match_startCenterVP;
        this.match_startCoreHp = match_startCoreHp;

        this.cost_baseActionCost = cost_baseActionCost;
        this.cost_actionGrowthFactor = cost_actionGrowthFactor;

        this.reward_budgetBonusForVP = reward_budgetBonusForVP;
        this.reward_budgetBonusForCoreDamage = reward_budgetBonusForCoreDamage;

        this.cap_maxActionsPerTurn = cap_maxActionsPerTurn;
        this.cap_maxVP = cap_maxVP;
        this.cap_maxBudget = cap_maxBudget;
        this.cap_maxVPPool = cap_maxVPPool;
        this.cap_maxCoreHealth = cap_maxCoreHealth;
        this.pieceLimitEnabled = pieceLimitEnabled;
        this.pieceLimitPerPlayer = pieceLimitPerPlayer;

        this.player_count = player_count;
        this.player_applyBotSurcharges = player_applyBotSurcharges;
        this.player_applyStartOfTurnBudgetDecrease = player_applyStartOfTurnBudgetDecrease;
        this.player_startingBudget = player_startingBudget;

        this.player_isAI = player_isAI;
        this.player_team = player_team;
        this.player_name = player_name;

        this.obs_maxCells = obs_maxCells;
        this.obs_maxDistance = obs_maxDistance;

        this.agent = agent;

        this.playerControl = playerControl;
        this.playerPolicy = playerPolicy;
        this.enableMLAgents = enableMLAgents;
        this.mlObservationSize = mlObservationSize;
        this.mlBehavior = mlBehavior;
    }
}



public readonly struct AgentCaps
{
    public readonly int maxVP;
    public readonly int maxCoreHP;
    public readonly int maxActionsPerTurn;
    public readonly int maxDistance;      // e.g., 2*board_radius (safe upper bound)
    public readonly float maxBudget;

    // precomputed inverses for fast normalization
    public readonly float invMaxVP, invMaxCoreHP, invMaxActions, invMaxDistance, invMaxBudget;

    public AgentCaps(int maxVP, int maxCoreHP, int maxActionsPerTurn, int maxDistance, float maxBudget)
    {
        this.maxVP = maxVP; this.maxCoreHP = maxCoreHP; this.maxActionsPerTurn = maxActionsPerTurn;
        this.maxDistance = maxDistance; this.maxBudget = maxBudget;
        invMaxVP = maxVP > 0 ? 1f / maxVP : 0f;
        invMaxCoreHP = maxCoreHP > 0 ? 1f / maxCoreHP : 0f;
        invMaxActions = maxActionsPerTurn > 0 ? 1f / maxActionsPerTurn : 0f;
        invMaxDistance = maxDistance > 0 ? 1f / maxDistance : 0f;
        invMaxBudget = maxBudget > 0 ? 1f / maxBudget : 0f;
    }
}

public readonly struct AgentPlayerParams
{
    public readonly byte playerId;
    public readonly bool applyBotSurcharges;
    public readonly bool applyStartOfTurnBudgetDecrease;
    public readonly bool isAI;
    public readonly int team;
    public readonly string name;

    public AgentPlayerParams(byte playerId, bool applyBotSurcharges, bool applyStartOfTurnBudgetDecrease, bool isAI, int team, string name)
    { this.playerId = playerId; this.applyBotSurcharges = applyBotSurcharges; this.applyStartOfTurnBudgetDecrease = applyStartOfTurnBudgetDecrease; this.isAI = isAI; this.team = team; this.name = name; }
}

public readonly struct AgentAnchors
{
    public readonly int vpCellId;
    public readonly int myCoreCellId;

    public AgentAnchors(int vpCellId, int myCoreCellId)
    { this.vpCellId = vpCellId; this.myCoreCellId = myCoreCellId; }
}
