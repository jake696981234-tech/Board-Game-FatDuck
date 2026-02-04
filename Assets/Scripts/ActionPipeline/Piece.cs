
using static Piece.AbilityKind;

public static class Piece
{
    public enum AbilityKind : byte
    {
        Move = 0,
        Shoot = 1,
        CaptureVP = 2,
        CoreDamage = 3,
        Push = 4,
        GroupBuild = 5,
        Upgrade = 6,
        Launcher = 7,
        Spawner = 8,
        SacrificeFactory = 9,
        ConversionFactory = 10,
        Explosive = 11,
        Sniper = 12,
        NecroSpawn = 13,
        WorkYard = 14,
        Create = 14,
        EndTurn = 15,
        Factory = 16,
        Sanctuary = 17,
        Eat = 18,
        SacrificeCost = 19,
        FeedingGround = 20,
        Zombie = 21,
        Invalid = 22,
    }

    public const int ActiveAbilityCount = 16;
    public static bool[,] ActiveAbilitesEnabledFromType = new bool[typeCount , ActiveAbilityCount]; 

    public static void SetActiveAbilitesEnabledFromType()
    {
        ActiveAbilitesEnabledFromType = new bool[typeCount , ActiveAbilityCount]; 
        for (int type = 0; type < typeCount; type++)
        {
            ActiveAbilitesEnabledFromType[type, (int)Move] = move_enabled[type];
            ActiveAbilitesEnabledFromType[type, (int)Shoot] = shoot_enabled[type];
            ActiveAbilitesEnabledFromType[type, (int)CaptureVP] = captureVP_enabled[type];
            ActiveAbilitesEnabledFromType[type, (int)CoreDamage] = coreDamage_enabled[type];
            ActiveAbilitesEnabledFromType[type, (int)GroupBuild] = groupBuild_enabled[type];
            ActiveAbilitesEnabledFromType[type, (int)Upgrade] = upgrade_enabled[type];
            ActiveAbilitesEnabledFromType[type, (int)Launcher] = launcher_enabled[type];
            ActiveAbilitesEnabledFromType[type, (int)Spawner] = spawn_enabled[type];
            ActiveAbilitesEnabledFromType[type, (int)SacrificeFactory] = sacrificeFactory_enabled[type];
            ActiveAbilitesEnabledFromType[type, (int)ConversionFactory] = conversionFactory_enabled[type];
            ActiveAbilitesEnabledFromType[type, (int)Explosive] = explosive_enabled[type];
            ActiveAbilitesEnabledFromType[type, (int)Sniper] = sniper_enabled[type];
            ActiveAbilitesEnabledFromType[type, (int)NecroSpawn] = necroSpawn_enabled[type];
        }
    }

    public static int typeCount;
    #region UI
    public static string[] name;
    public static string[] factionName;
    public static string[] spritePath;
    #endregion
    #region General Fields
    public static bool[] isBuilding;
    public static bool[] isBuildable;
    public static int[] BuildCost;
    public static short[] maxHP;
    #endregion
    #region Digits
    public static int[] requiredDigit;
    public static int[] digitItGives;
    #endregion
    #region Connectors
    public static bool[] connectors_enabled;
    public static bool[] connector_needsCapital;
    public static bool[] connector_isCapital;
    public static int[] connector_capitalHealth;
    public static ulong[] connector_allowedMasks;
    #endregion
    #region Group Build
    public static bool[] groupBuild_enabled;
    public static int[] groupBuild_target;
    public static int[] groupBuild_requireNumber;
    public static bool[] groupBuild_deletion;
    public static int[] groupBuild_botSurcharge;
    #endregion
    #region Upgrade
    public static bool[] upgrade_enabled;
    public static int[] upgrade_target;
    public static bool[] upgrade_isGoalKills;
    public static int[] upgrade_killsNeeded;
    public static int[] upgrade_botSurcharge;
    #endregion
    #region launcher
    public static bool[] launcher_enabled;
    public static int[] launcher_inputRange;
    public static int[] launcher_outputRange;
    public static bool[] launcher_isfriendlyFire;
    public static bool[] launcher_isEnemyFire;
    public static int[] launcher_botSurcharge;
    #endregion
    #region Push
    public static bool[] push_enabled;
    public static bool[] push_IsTargetsBuildings;
    public static bool[] push_isTargetsSoldiers;
    public static int[] push_rangeMax;
    public static int[] push_pushAmount;
    public static bool[] push_isPull;
    public static bool[] push_isFriendlyFire;
    public static int[] push_damage;
    #endregion
    #region spawn
    public static bool[] spawn_enabled;
    public static int[] spawn_pieceAmount;
    public static int[] spawn_targetType;
    public static int[] spawn_range;
    public static bool[] spawn_isOnlyOncePerTurn;
    public static int[] spawn_botSurcharge;
    #endregion
    #region multiCreate
    // public static bool[] multiCreate_enabledByType;
    // public static int[] multiCreate_amountByType;
    // public static bool[] multiCreate_isBoardering;
    #endregion
    #region factory
    public static bool[] factory_enabled;
    public static int[] factory_amount;
    public static bool[] factory_isRoundMultiplier;
    public static bool[] factory_isGroup;
    public static int[] factory_groupAmount;
    public static bool[] factory_isInstantPayOut;
    public static int[] factory_instantPayOutAmount;
    public static bool[] factory_isKillPenalty;
    public static int[] factory_killsNeeded;
    public static int[] factory_killsPunishment;
    
    #endregion
    #region sanctuary
    public static bool[] sanctuary_enabled;
    public static int[] sanctuary_range;
    #endregion
    #region conversion Factory
    // may add this to the Factory region
    public static bool[] conversionFactory_enabled;
    public static bool[] conversionFactory_isCoreHealth;
    public static bool[] conversionFactory_isVp;
    public static int[] conversionFactory_amount;
    public static int[] conversionFactory_botSurcharge;
    #endregion
    #region eat
    public static bool[] eat_enabled;
    public static int[] eat_amount;
    #endregion
    #region shoot
    public static bool[] shoot_enabled;
    public static int[] shoot_rangeMin;
    public static int[] shoot_rangeMax;
    public static int[] shoot_damage;
    public static int[] shoot_botSurcharge;
    #endregion
    #region Move
    public static bool[] move_enabled;
    public static int[] move_rangeMin;
    public static int[] move_rangeMax;
    public static int[] move_damage;
    public static int[] move_botSurcharge;
    #endregion
    #region sacrifice Factory
    public static bool[] sacrificeFactory_enabled;
    public static int[] sacrificeFactory_amount;
    public static int[] sacrificeFactory_rangeMin;
    public static int[] sacrificeFactory_rangeMax;
    public static int[] sacrificeFactory_botSurcharge;
    #endregion
    #region captureVP
    public static bool[] captureVP_enabled;
    public static int[] captureVP_botSurcharge;
    #endregion
    #region coreDamage
    public static bool[] coreDamage_enabled;
    public static int[] coreDamage_damage;
    public static int[] coreDamage_botSurcharge;
    #endregion
    #region sacrificeCost
    // public static bool[] sacrificeCost_enabled;
    // public static bool[] sacrificeCost_isNeedsSpecificPiece;
    // public static int[] sacrificeCost_specificPiece;
    // public static int[] sacrificeCost_howManyItNeeds;

    #endregion
    #region Feeding Ground
    public static bool[] feedingGround_enabled;
    public static int[] feedingGround_Range;
    public static int[] feedingGround_payOut;
    #endregion
    #region Explosive
    public static bool[] explosive_enabled;
    public static bool[] explosive_isFriendlyFire; 
    public static bool[] explosive_isKillItself; 
    public static int[] explosive_damage;
    public static int[] explosive_range;
    #endregion
    // #region piece Build
    // public static bool[] pieceBuild_enabled;
    // public static int[] pieceBuild_range;
    // public static int[][] pieceBuild_targetIds;
    // #endregion
    #region sniper
    public static bool[] sniper_enabled;
    public static int[] sniper_minRange;
    public static int[] sniper_damage;
    public static int[] sniper_maxRange;
    public static bool[] sniper_isonlySoldiers;
    public static bool[] sniper_isLineOfSight;
    public static bool[] sniper_isFriendlyFire;
    public static int[] sniper_lineLength;
    #endregion
    #region Zombie
    public static bool[] zombie_enabled;
    #endregion
    #region necroSpawn
    public static bool[] necroSpawn_enabled;
    public static int[] necroSpawn_range;
    public static int[] necroSpawn_botSurcharge;

    #endregion
    #region workYard
    public static bool[] workYard_enabled;
    public static int[] workYard_range;
    public static int[] workYard_botSurcharge;
    public static bool[] workYard_excludeBuildings;
    #endregion
    public static bool IsConnectorConfigAllowed(byte type, int configIndex)
    {
        if (configIndex < 0 || configIndex >= 64) return false;
        if (type >= connector_allowedMasks.Length) return false;
        if (!PiecesSides.AreWallsContiguous(configIndex) && Info.ContiguousWalls) return false;
        ulong mask = connector_allowedMasks[type];
        return (mask & (1UL << configIndex)) != 0;
    }

}
