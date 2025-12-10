// Assets/Scripts/Model/Pieces.cs (Plan B — Create decoupled from abilities)
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

/// <summary>
/// Data-only registry for piece TYPES and ABILITIES (no Unity references).
/// - Dense integer indices for types & abilities (ML-friendly).
/// - Fixed-width ability slots per type for fast masking.
/// - Parameters used by legality kernels and cost calculation.
/// - Plan B: Create is NOT an ability; OfferProvider enumerates Create structurally.
/// </summary>
public static class Pieces
{
    // ====== Public enums (compact; persist order once you ship) ======
    // Align numeric order with ActionKind (0..10). Extras appended after.
    // Note: EndTurn is included only to keep indices aligned; do not assign it to ability slots.
    public enum AbilityKind : byte
    {
        Move = 0,
        Shoot = 1,
        CaptureVP = 2,
        CoreDamage = 3,
        Create = 4,
        EndTurn = 5, // placeholder for alignment with ActionKind
        Push = 6,
        GroupBuild = 7,
        Upgrade = 8,
        Launcher = 9,
        Spawner = 10,
        SacrificeFactory = 11,
        ConversionFactory = 12,
        Factory = 13,
        Sanctuary = 14,
        Eat = 15,
        Custom2 = 16,
    }
    public enum TargetKind : byte { None = 0, Cell = 1, Piece = 2 }

    // ====== Type registry (dense indices 0..typeCount-1) ======
    // public static int typeCount;

    // // Per-type fields
    // public static bool[] isBuildingByType;      // [type] -> true => Building, false => Soldier (kept as bool for existing callers)
    // public static byte[] buildableByType;       // [type] -> 0/1 flag; default 1
    // public static int[] buildCostByType;       // [type] -> cost to Create this type
    // public static short[] maxHPByType;           // [type] -> max HP
    // public static int[][] codeDigitsByType;      // [type] -> prerequisite digits (optional)

    // // --- Digits (Plan B): per-type grant; per-type requirement already lives in codeDigitsByType ---
    // public static sbyte[] grantsDigitByType; // [type] -> -1 = none, else 0..9

    // // Human-only (UI/debug/tooling)
    // public static string[] idByType;             // [type] -> stable id (tooling)
    // public static string[] displayNameByType;
    // public static string[] factionNameByType;
    // public static string[] spritePathByType;
    // public static string[] moveUIColorHexByType;
    // public static string[] shootUIColorHexByType;
    // public static bool[] hasConnectorsByType;      // [type] -> true if this type uses connector/wall sides
    // public static bool[] connectorNeedsCapital;    // [type] -> true if placement requires capital connectivity
    // public static bool[] connectorIsCapital;       // [type] -> true if this type counts as a capital
    // public static int[] connectorCapitalHealth;   // [type] -> capital health contribution for connected component
    // public static ulong[] connectorAllowedMasks;    // [type] -> bitmask of allowed 6-bit side configs (bit i -> config i allowed)
    // public static bool[] groupBuildEnabled;        // [type] -> can this type perform group build
    // public static int[] groupBuildTargetType;     // [type] -> type id to create
    // public static int[] groupBuildRequireNumber;  // [type] -> required count in cluster
    // public static bool[] groupBuildDeletion;       // [type] -> delete contributors on build
    // public static bool[] upgradeEnabled;           // [type] -> can perform upgrade
    // public static int[] upgradeTargetType;        // [type] -> replace with this type
    // public static int[] launcher_inputRange;      // [ability] -> range to pick a piece
    // public static int[] launcher_outputRange;     // [ability] -> range from launcher to drop target
    // public static bool[] launcher_friendlyFire;    // [ability] -> can launch friendlies
    // public static bool[] launcher_enemyFire;       // [ability] -> can launch enemies
    // public static bool[] push_TargetsBuildings;    // [ability] -> push can target buildings
    // public static bool[] push_TargetsSoldiers;     // [ability] -> push can target soldiers
    // public static int[] push_rangeMax;            // [ability] -> input range for push
    // public static int[] push_PushAmount;          // [ability] -> displacement distance
    // public static bool[] push_pull;                // [ability] -> invert direction
    // public static bool[] push_FriendlyFire;        // [ability] -> allow friendlies
    // public static int[] push_damage;              // [ability] -> damage on push
    // public static int[] spawn_pieceAmount;        // [ability] -> how many pieces to create
    // public static int[] spawn_targetType;         // [ability] -> type to create
    // public static int[] spawn_range;              // [ability] -> spawn range
    // public static bool[] spawn_onlyOncePerTurn;    // [ability] -> once-per-turn gate
    // public static bool[] multiCreate_enabledByType; // [type] -> multi-create hook
    // public static int[] multiCreate_amountByType;  // [type] -> how many total (including primary)
    // public static bool[] multiCreate_boarderingByType; // [type] -> require new pieces to border each other
    // public static int[] factory_amount;           // [ability] -> payout amount
    // public static bool[] factory_roundMultiplier;  // [ability] -> multiply by round number
    // public static bool[] factory_group;            // [ability] -> requires groups
    // public static int[] factory_groupAmount;      // [ability] -> size of each group
    // public static bool[] sanctuary_enabled;
    // public static int[] Sanctuary_range;
    // public static bool[] conversionFactory_coreHealth; // [ability] -> convert to core health
    // public static bool[] conversionFactory_vp;         // [ability] -> convert to VP
    // public static int[] conversionFactory_amount;      // [ability] -> amount converted
    // public static int[] conversionFactory_botSurcharge; // [ability] -> bot surcharge
    // public static bool[] eat_enabled;                  // [ability] -> eat passive enabled
    // public static int[] eat_amount;                    // [ability] -> eat amount

    // // Name maps (optional)
    // public static Dictionary<string, int> typeIndexByName;
    // public static string[] typeNameByIndex;

    // // ====== Ability catalog (dense indices 0..abilityCount-1) ======
    // public static int abilityCount;

    // public static AbilityKind[] abilityKind;     // [abilityId]
    // public static TargetKind[] targetKind;      // [abilityId]

    // // Generic params (unused = 0)
    // public static int[] rangeMin;                // [abilityId]
    // public static int[] rangeMax;                // [abilityId]
    // public static int[] areaRadius;              // [abilityId]
    // public static int[] damage;                  // [abilityId]
    // public static int[] customParam;             // [abilityId]
    // public static int[] sacrificeFactory_amount; // [abilityId] -> amount to add when sacrificing

    // // NOTE: In Plan B, Create is NOT an ability. We keep buildTypeId only for legacy reads;
    // // new OfferProvider should not depend on it for Create.
    // public static int[] buildTypeId;             // [abilityId] -> type index (legacy; not used for Create in Plan B)

    // // Pricing surcharges (bot-only in Plan B)
    // public static int[] baseSurcharge;           // legacy; ignored by AbilitySurcharge()
    // public static int[] botThinkSurcharge;       // [abilityId] -> bot-only surcharge

    // // Ability name maps (optional)
    // public static Dictionary<string, int> abilityIndexByName;
    // public static string[] abilityNameByIndex;

    // // ====== Fixed-width ability slots per TYPE ======
    // public static int maxAbilitySlots = 7;       // slot 0: Move, 1: Shoot, 2: CaptureVP, 3: CoreDamage, others for custom
    // public static int[] abilityIdByTypeSlot;     // [type * maxAbilitySlots + slot] -> abilityId or -1
    // public static int[] abilitySlotCount;        // [type] -> # valid slots (0..maxAbilitySlots)

    // --------- Accessors / helpers (O(1), zero-alloc) ----------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GrantsDigit(byte type)
    {
        return (type < grantsDigitByType.Length) ? (int)grantsDigitByType[type] : -1;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetRequiredDigit(byte type)
    {
        var arr = codeDigitsByType[type];
        return (arr != null && arr.Length > 0) ? arr[0] : -1; // -1 = none
    }


    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static int AbilitySlotCount(byte type) => abilitySlotCount[type];

    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static int AbilityIdAtSlot(byte type, int slot)
    // {
    //     if ((uint)slot >= (uint)maxAbilitySlots) return -1;
    //     return abilityIdByTypeSlot[type * maxAbilitySlots + slot];
    // }

    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static bool TryGetSlotIndexForAbility(byte type, int abilityId, out int slot)
    // {
    //     int baseIdx = type * maxAbilitySlots;
    //     int limit = abilitySlotCount[type];
    //     for (int s = 0; s < limit; s++)
    //     {
    //         if (abilityIdByTypeSlot[baseIdx + s] == abilityId) { slot = s; return true; }
    //     }
    //     slot = -1; return false;
    // }

    // public static bool HasAbilityKind(byte type, AbilityKind kind)
    // {
    //     int limit = AbilitySlotCount(type);
    //     for (int s = 0; s < limit; s++)
    //     {
    //         int aid = AbilityIdAtSlot(type, s);
    //         if (aid < 0) continue;
    //         if (AbilityKindOf(aid) == kind) return true;
    //     }
    //     return false;
    // }


    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static AbilityKind AbilityKindOf(int abilityId) => abilityKind[abilityId];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TargetKind TargetKindOf(int abilityId) => targetKind[abilityId];

    // ——— Added wrapper for OfferProvider convenience (type,slot) → AbilityKind as byte
    public const byte AbilityKindInvalid = byte.MaxValue;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte GetAbilityKind(byte type, int slot)
    {
        if ((uint)slot >= (uint)maxAbilitySlots) return AbilityKindInvalid;
        int abilityId = AbilityIdAtSlot(type, slot);
        if (abilityId < 0) return AbilityKindInvalid;
        return (byte)abilityKind[abilityId];
    }

    // ——— Plan B: Create is decoupled — per-type build info
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBuildCost(byte type) => buildCostByType[type];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static bool IsBuildable(byte type)
    // {
    //     return type >= 0 && type < buildableByType.Length && buildableByType[type] != 0;
    // }

    // ——— Legacy helper: expose created type from an ability id (not used for Create in Plan B)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetCreatedPieceType(int abilityId)
    {
        if ((uint)abilityId >= (uint)buildTypeId.Length) return -1;
        return buildTypeId[abilityId];
    }

    // ====== Cost helpers ======
    /// <summary>
    /// Plan B pricing: only botThinkSurcharge is used. If applyBotSurcharges=false, returns 0.
    /// (baseSurcharge is ignored to avoid affecting training.)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AbilitySurcharge(int abilityId, bool applyBotSurcharges)
    {
        if (!applyBotSurcharges) return 0;
        return (abilityId >= 0 && abilityId < botThinkSurcharge.Length) ? botThinkSurcharge[abilityId] : 0;
    }



    // // ====== Metadata helpers ======
    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static bool IsBuilding(byte type)
    // {
    //     return type >= 0 && type < isBuildingByType.Length && isBuildingByType[type];
    // }

    // // ====== Connector helpers ======
    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static bool HasConnectors(byte type)
    // {
    //     return type < hasConnectorsByType.Length && hasConnectorsByType[type];
    // }

    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static bool ConnectorNeedsCapital(byte type)
    // {
    //     return type < connectorNeedsCapital.Length && connectorNeedsCapital[type];
    // }

    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static bool ConnectorIsCapital(byte type)
    // {
    //     return type < connectorIsCapital.Length && connectorIsCapital[type];
    // }

    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static int ConnectorCapitalHealth(byte type)
    // {
    //     return type < connectorCapitalHealth.Length ? connectorCapitalHealth[type] : 0;
    // }

    /// <summary>
    /// Returns true if the given 0..63 configuration index is allowed for this type.
    /// If mask is 0 and the type has connectors, treat it as "no configs allowed".
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsConnectorConfigAllowed(byte type, int configIndex)
    {
        if (configIndex < 0 || configIndex >= 64) return false;
        if (type >= connectorAllowedMasks.Length) return false;
        ulong mask = connectorAllowedMasks[type];
        return (mask & (1UL << configIndex)) != 0;
    }





    // ====== Minimal validation (call after CSV compile) ======
    public static string ValidateBasic()
    {
        if (typeCount <= 0) return "No types defined.";
        if (abilityCount < 0) return "Invalid ability count.";
        if (abilityIdByTypeSlot == null || abilityIdByTypeSlot.Length != typeCount * maxAbilitySlots)
            return "abilityIdByTypeSlot not allocated or wrong size.";
        if (buildableByType == null || buildableByType.Length != typeCount) return "buildableByType not allocated or wrong size.";
        if (hasConnectorsByType == null || hasConnectorsByType.Length != typeCount) return "hasConnectorsByType not allocated or wrong size.";
        if (connectorNeedsCapital == null || connectorNeedsCapital.Length != typeCount) return "connectorNeedsCapital not allocated or wrong size.";
        if (connectorIsCapital == null || connectorIsCapital.Length != typeCount) return "connectorIsCapital not allocated or wrong size.";
        if (connectorCapitalHealth == null || connectorCapitalHealth.Length != typeCount) return "connectorCapitalHealth not allocated or wrong size.";
        if (connectorAllowedMasks == null || connectorAllowedMasks.Length != typeCount) return "connectorAllowedMasks not allocated or wrong size.";
        if (groupBuildEnabled == null || groupBuildEnabled.Length != typeCount) return "groupBuildEnabled not allocated or wrong size.";
        if (groupBuildTargetType == null || groupBuildTargetType.Length != typeCount) return "groupBuildTargetType not allocated or wrong size.";
        if (groupBuildRequireNumber == null || groupBuildRequireNumber.Length != typeCount) return "groupBuildRequireNumber not allocated or wrong size.";
        if (groupBuildDeletion == null || groupBuildDeletion.Length != typeCount) return "groupBuildDeletion not allocated or wrong size.";
        if (upgradeEnabled == null || upgradeEnabled.Length != typeCount) return "upgradeEnabled not allocated or wrong size.";
        if (upgradeTargetType == null || upgradeTargetType.Length != typeCount) return "upgradeTargetType not allocated or wrong size.";
        if (spawn_pieceAmount == null || spawn_pieceAmount.Length != abilityCount) return "spawn_pieceAmount not allocated or wrong size.";
        if (spawn_targetType == null || spawn_targetType.Length != abilityCount) return "spawn_targetType not allocated or wrong size.";
        if (spawn_range == null || spawn_range.Length != abilityCount) return "spawn_range not allocated or wrong size.";
        if (spawn_onlyOncePerTurn == null || spawn_onlyOncePerTurn.Length != abilityCount) return "spawn_onlyOncePerTurn not allocated or wrong size.";
        if (factory_amount == null || factory_amount.Length != abilityCount) return "factory_amount not allocated or wrong size.";
        if (factory_roundMultiplier == null || factory_roundMultiplier.Length != abilityCount) return "factory_roundMultiplier not allocated or wrong size.";
        if (factory_group == null || factory_group.Length != abilityCount) return "factory_group not allocated or wrong size.";
        if (factory_groupAmount == null || factory_groupAmount.Length != abilityCount) return "factory_groupAmount not allocated or wrong size.";
        if (sanctuary_enabled == null || sanctuary_enabled.Length != abilityCount) return "sanctuary_enabled not allocated or wrong size.";
        if (Sanctuary_range == null || Sanctuary_range.Length != abilityCount) return "Sanctuary_range not allocated or wrong size.";
        if (conversionFactory_coreHealth == null || conversionFactory_coreHealth.Length != abilityCount) return "conversionFactory_coreHealth not allocated or wrong size.";
        if (conversionFactory_vp == null || conversionFactory_vp.Length != abilityCount) return "conversionFactory_vp not allocated or wrong size.";
        if (conversionFactory_amount == null || conversionFactory_amount.Length != abilityCount) return "conversionFactory_amount not allocated or wrong size.";
        if (conversionFactory_botSurcharge == null || conversionFactory_botSurcharge.Length != abilityCount) return "conversionFactory_botSurcharge not allocated or wrong size.";
        if (eat_enabled == null || eat_enabled.Length != abilityCount) return "eat_enabled not allocated or wrong size.";
        if (eat_amount == null || eat_amount.Length != abilityCount) return "eat_amount not allocated or wrong size.";
        if (push_TargetsBuildings == null || push_TargetsBuildings.Length != abilityCount) return "push_TargetsBuildings not allocated or wrong size.";
        if (push_TargetsSoldiers == null || push_TargetsSoldiers.Length != abilityCount) return "push_TargetsSoldiers not allocated or wrong size.";
        if (push_rangeMax == null || push_rangeMax.Length != abilityCount) return "push_rangeMax not allocated or wrong size.";
        if (push_PushAmount == null || push_PushAmount.Length != abilityCount) return "push_PushAmount not allocated or wrong size.";
        if (push_pull == null || push_pull.Length != abilityCount) return "push_pull not allocated or wrong size.";
        if (push_FriendlyFire == null || push_FriendlyFire.Length != abilityCount) return "push_FriendlyFire not allocated or wrong size.";
        if (push_damage == null || push_damage.Length != abilityCount) return "push_damage not allocated or wrong size.";
        if (multiCreate_enabledByType == null || multiCreate_enabledByType.Length != typeCount) return "multiCreate_enabledByType not allocated or wrong size.";
        if (multiCreate_amountByType == null || multiCreate_amountByType.Length != typeCount) return "multiCreate_amountByType not allocated or wrong size.";
        if (multiCreate_boarderingByType == null || multiCreate_boarderingByType.Length != typeCount) return "multiCreate_boarderingByType not allocated or wrong size.";
        if (spawn_pieceAmount == null || spawn_pieceAmount.Length != abilityCount) return "spawn_pieceAmount not allocated or wrong size.";
        if (spawn_targetType == null || spawn_targetType.Length != abilityCount) return "spawn_targetType not allocated or wrong size.";
        if (spawn_range == null || spawn_range.Length != abilityCount) return "spawn_range not allocated or wrong size.";
        if (spawn_onlyOncePerTurn == null || spawn_onlyOncePerTurn.Length != abilityCount) return "spawn_onlyOncePerTurn not allocated or wrong size.";
        if (upgradeEnabled == null || upgradeEnabled.Length != typeCount) return "upgradeEnabled not allocated or wrong size.";
        if (upgradeTargetType == null || upgradeTargetType.Length != typeCount) return "upgradeTargetType not allocated or wrong size.";
        for (int t = 0; t < typeCount; t++)
        {
            int sc = abilitySlotCount[t];
            if (sc < 0 || sc > maxAbilitySlots) return $"Type {t} has invalid slotCount {sc}";
            int baseIdx = t * maxAbilitySlots;
            for (int s = sc; s < maxAbilitySlots; s++)
            {
                if (abilityIdByTypeSlot[baseIdx + s] != -1) return $"Type {t} has non -1 in unused slot {s}";
            }
        }
        return null; // OK
    }

    // ====== Builder API (CSV → arrays) ======
    public static void Allocate(int _typeCount, int _abilityCount, int _maxSlots = 4)
    {
        typeCount = _typeCount;
        abilityCount = _abilityCount;
        maxAbilitySlots = Math.Max(1, _maxSlots);

        // per-type
        isBuildingByType = new bool[typeCount];
        buildableByType = new byte[typeCount];              // default 0; we'll set 1 below
        buildCostByType = new int[typeCount];
        maxHPByType = new short[typeCount];
        codeDigitsByType = new int[typeCount][];

        for (int i = 0; i < typeCount; i++) buildableByType[i] = 1; // default buildable

        idByType = new string[typeCount];
        displayNameByType = new string[typeCount];
        factionNameByType = new string[typeCount];
        spritePathByType = new string[typeCount];
        moveUIColorHexByType = new string[typeCount];
        shootUIColorHexByType = new string[typeCount];
        hasConnectorsByType = new bool[typeCount];
        connectorNeedsCapital = new bool[typeCount];
        connectorIsCapital = new bool[typeCount];
        connectorCapitalHealth = new int[typeCount];
        connectorAllowedMasks = new ulong[typeCount];
        groupBuildEnabled = new bool[typeCount];
        groupBuildTargetType = new int[typeCount];
        groupBuildRequireNumber = new int[typeCount];
        groupBuildDeletion = new bool[typeCount];
        upgradeEnabled = new bool[typeCount];
        upgradeTargetType = new int[typeCount];
        spawn_pieceAmount = new int[abilityCount];
        spawn_targetType = new int[abilityCount];
        spawn_range = new int[abilityCount];
        spawn_onlyOncePerTurn = new bool[abilityCount];
        for (int i = 0; i < spawn_targetType.Length; i++) spawn_targetType[i] = -1;

        typeIndexByName = new Dictionary<string, int>(typeCount, StringComparer.OrdinalIgnoreCase);
        typeNameByIndex = new string[typeCount];

        // per-ability (non-Create kinds)
        abilityKind = new AbilityKind[abilityCount];
        targetKind = new TargetKind[abilityCount];
        rangeMin = new int[abilityCount];
        rangeMax = new int[abilityCount];
        areaRadius = new int[abilityCount];
        damage = new int[abilityCount];
        customParam = new int[abilityCount];
        sacrificeFactory_amount = new int[abilityCount];
        buildTypeId = new int[abilityCount];    // legacy; may be left -1 for most abilities
        baseSurcharge = new int[abilityCount];    // legacy; ignored in pricing
        botThinkSurcharge = new int[abilityCount];
        launcher_inputRange = new int[abilityCount];
        launcher_outputRange = new int[abilityCount];
        launcher_friendlyFire = new bool[abilityCount];
        launcher_enemyFire = new bool[abilityCount];
        push_TargetsBuildings = new bool[abilityCount];
        push_TargetsSoldiers = new bool[abilityCount];
        push_rangeMax = new int[abilityCount];
        push_PushAmount = new int[abilityCount];
        push_pull = new bool[abilityCount];
        push_FriendlyFire = new bool[abilityCount];
        push_damage = new int[abilityCount];
        spawn_pieceAmount = new int[abilityCount];
        spawn_targetType = new int[abilityCount];
        spawn_range = new int[abilityCount];
        spawn_onlyOncePerTurn = new bool[abilityCount];
        factory_amount = new int[abilityCount];
        factory_roundMultiplier = new bool[abilityCount];
        factory_group = new bool[abilityCount];
        factory_groupAmount = new int[abilityCount];
        sanctuary_enabled = new bool[abilityCount];
        Sanctuary_range = new int[abilityCount];
        conversionFactory_coreHealth = new bool[abilityCount];
        conversionFactory_vp = new bool[abilityCount];
        conversionFactory_amount = new int[abilityCount];
        conversionFactory_botSurcharge = new int[abilityCount];
        eat_enabled = new bool[abilityCount];
        eat_amount = new int[abilityCount];
        multiCreate_enabledByType = new bool[typeCount];
        multiCreate_amountByType = new int[typeCount];
        multiCreate_boarderingByType = new bool[typeCount];
        groupBuildEnabled = new bool[typeCount];
        groupBuildTargetType = new int[typeCount];
        groupBuildRequireNumber = new int[typeCount];
        groupBuildDeletion = new bool[typeCount];
        upgradeEnabled = new bool[typeCount];
        upgradeTargetType = new int[typeCount];

        abilityIndexByName = new Dictionary<string, int>(abilityCount, StringComparer.OrdinalIgnoreCase);
        abilityNameByIndex = new string[abilityCount];

        // slots
        abilityIdByTypeSlot = new int[typeCount * maxAbilitySlots];
        for (int i = 0; i < abilityIdByTypeSlot.Length; i++) abilityIdByTypeSlot[i] = -1;
        abilitySlotCount = new int[typeCount];

        grantsDigitByType = new sbyte[typeCount];
        for (int i = 0; i < typeCount; i++) grantsDigitByType[i] = -1;
    }

    public static void DefineType(int type, string stableName)
    {
        typeNameByIndex[type] = stableName;
        typeIndexByName[stableName] = type;
    }

    public static void DefineAbility(int abilityId, string stableName, AbilityKind kind, TargetKind tkind)
    {
        abilityNameByIndex[abilityId] = stableName;
        abilityIndexByName[stableName] = abilityId;
        abilityKind[abilityId] = kind;
        targetKind[abilityId] = tkind;
        // numeric params set by importer after this
    }

    public static void AddAbilitySlot(byte type, int abilityId)
    {
        int count = abilitySlotCount[type];
        if (count >= maxAbilitySlots) throw new InvalidOperationException($"Type {type} already has {count} slots (max {maxAbilitySlots}).");
        abilityIdByTypeSlot[type * maxAbilitySlots + count] = abilityId;
        abilitySlotCount[type] = count + 1;
    }
}
