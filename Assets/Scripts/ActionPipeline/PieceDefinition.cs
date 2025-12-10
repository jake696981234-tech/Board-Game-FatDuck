

public static class PieceDefinition
{
    public static int typeCount;


    #region UI
    public static string[] displayNameByType;
    public static string[] factionNameByType;
    public static string[] spritePathByType;


    #endregion
    #region General Fields
    public static bool[] isBuildingByType;      // [type] -> true => Building, false => Soldier (kept as bool for existing callers)
    public static bool[] buildableByType;       // [type] -> 0/1 flag; default 1
    public static int[] buildCostByType;       // [type] -> cost to Create this type
    public static short[] maxHPByType;           // [type] -> max HP

    #endregion
    #region Digits
    public static int[] codeDigitsByType;      // [type] -> prerequisite digits (optional)
    public static int[] grantsDigitByType; // [type] -> -1 = none, else 0..9

    #endregion
    #region Connectors

    public static bool[] connectors_enabled;      // [type] -> true if this type uses connector/wall sides
    public static bool[] connectorNeedsCapital;    // [type] -> true if placement requires capital connectivity
    public static bool[] connectorIsCapital;       // [type] -> true if this type counts as a capital
    public static int[] connectorCapitalHealth;   // [type] -> capital health contribution for connected component
    public static ulong[] connectorAllowedMasks;    // [type] -> bitmask of allowed 6-bit side configs (bit i -> config i allowed)

    #endregion
    #region Group Build
    public static bool[] groupBuild_enabled;        // [type] -> can this type perform group build
    public static int[] groupBuildTargetType;     // [type] -> type id to create
    public static int[] groupBuildRequireNumber;  // [type] -> required count in cluster
    public static bool[] groupBuildDeletion;       // [type] -> delete contributors on build
    public static int[] groupBuild_botSurcharge;

    #endregion
    #region Upgrade

    public static bool[] upgradeEnabled;           // [type] -> can perform upgrade
    public static int[] upgradeTargetType;        // [type] -> replace with this type
    public static int[] upgrade_botSurcharge;
    #endregion
    #region launcher

    public static bool[] launcher_enabled; //Addtion
    public static int[] launcher_inputRange;      // [ability] -> range to pick a piece
    public static int[] launcher_outputRange;     // [ability] -> range from launcher to drop target
    public static bool[] launcher_friendlyFire;    // [ability] -> can launch friendlies
    public static bool[] launcher_enemyFire;       // [ability] -> can launch enemies
    public static int[] launcher_botSurcharge;


    #endregion
    #region Push

    public static bool[] push_enabled; //addition
    public static bool[] push_TargetsBuildings;    // [ability] -> push can target buildings
    public static bool[] push_TargetsSoldiers;     // [ability] -> push can target soldiers
    public static int[] push_rangeMax;            // [ability] -> input range for push
    public static int[] push_PushAmount;          // [ability] -> displacement distance
    public static bool[] push_pull;                // [ability] -> invert direction
    public static bool[] push_FriendlyFire;        // [ability] -> allow friendlies
    public static int[] push_damage;              // [ability] -> damage on push

    #endregion
    #region spawn

    public static bool[] spawn_enabled; //addition
    public static int[] spawn_pieceAmount;        // [ability] -> how many pieces to create
    public static int[] spawn_targetType;         // [ability] -> type to create
    public static int[] spawn_range;              // [ability] -> spawn range
    public static bool[] spawn_onlyOncePerTurn;    // [ability] -> once-per-turn gate
    public static int[] spawn_botSurcharge;

    #endregion
    #region multiCreate

    public static bool[] multiCreate_enabledByType; // [type] -> multi-create hook
    public static int[] multiCreate_amountByType;  // [type] -> how many total (including primary)
    public static bool[] multiCreate_boarderingByType; // [type] -> require new pieces to border each other

    #endregion
    #region factory

    public static bool[] factory_enabled; //addition
    public static int[] factory_amount;           // [ability] -> payout amount
    public static bool[] factory_roundMultiplier;  // [ability] -> multiply by round number
    public static bool[] factory_group;            // [ability] -> requires groups
    public static int[] factory_groupAmount;      // [ability] -> size of each group

    #endregion
    #region sanctuary

    public static bool[] sanctuary_enabled;
    public static int[] sanctuary_range;


    #endregion
    #region conversion Factory
    // may add this to the Factory region

    public static bool[] conversionFactory_enabled; //addition
    public static bool[] conversionFactory_coreHealth; // [ability] -> convert to core health
    public static bool[] conversionFactory_vp;         // [ability] -> convert to VP
    public static int[] conversionFactory_amount;      // [ability] -> amount converted
    public static int[] conversionFactory_botSurcharge; // [ability] -> bot surcharge

    #endregion
    #region eat

    public static bool[] eat_enabled;                  // [ability] -> eat passive enabled
    public static int[] eat_amount;                    // [ability] -> eat amount

    #endregion
    #region shoot
    // this needs to re done- used to be Generic params
    //this is all addition
    public static bool[] shoot_enabled;
    public static int[] shoot_rangeMin;                // [abilityId]
    public static int[] shoot_rangeMax;                // [abilityId]
    public static int[] shoot_damage;                  // [abilityId]
    public static int[] shoot_botSurcharge;

    #endregion
    #region Move
    public static bool[] move_enabled; //addition
    public static int[] move_rangeMin;                // [abilityId]
    public static int[] move_rangeMax;                // [abilityId]
    public static int[] move_damage;                  // [abilityId]

    public static int[] move_botSurcharge;

    #endregion
    #region sacrifice Factory
    public static bool[] sacrificeFactory_enabled;
    public static int[] sacrificeFactory_amount; // [abilityId] -> amount to add when sacrificing
    public static int[] sacrificeFactory_rangeMin;
    public static int[] sacrificeFactory_rangeMax;
    public static int[] sacrificeFactory_botSurcharge;

    #endregion
    #region capture

    public static bool[] captureVP_enabled; //addition
    public static int[] captureVP_botSurcharge;

    public static bool[] coreDamage_enabled; //addition
    public static int[] coreDamage_damage; //addition
    public static int[] coreDamage_botSurcharge;

    #endregion

}
