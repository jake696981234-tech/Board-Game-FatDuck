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
public partial class Pieces
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
        Factory = 11,
        Sanctuary = 12,
        Custom1 = 13,
        Custom2 = 14
    }
    public enum TargetKind : byte { None = 0, Cell = 1, Piece = 2 }

    // ====== Type registry (dense indices 0..typeCount-1) ======
    public int typeCount;

    // Per-type fields
    public bool[] isBuildingByType;      // [type] -> true => Building, false => Soldier (kept as bool for existing callers)
    public byte[] buildableByType;       // [type] -> 0/1 flag; default 1
    public int[] buildCostByType;       // [type] -> cost to Create this type
    public short[] maxHPByType;           // [type] -> max HP
    public int[][] codeDigitsByType;      // [type] -> prerequisite digits (optional)

    // --- Digits (Plan B): per-type grant; per-type requirement already lives in codeDigitsByType ---
    public sbyte[] grantsDigitByType; // [type] -> -1 = none, else 0..9

    // Human-only (UI/debug/tooling)
    public string[] idByType;             // [type] -> stable id (tooling)
    public string[] displayNameByType;
    public string[] factionNameByType;
    public string[] spritePathByType;
    public string[] moveUIColorHexByType;
    public string[] shootUIColorHexByType;
    public bool[] hasConnectorsByType;      // [type] -> true if this type uses connector/wall sides
    public bool[] connectorNeedsCapital;    // [type] -> true if placement requires capital connectivity
    public bool[] connectorIsCapital;       // [type] -> true if this type counts as a capital
    public int[] connectorCapitalHealth;   // [type] -> capital health contribution for connected component
    public ulong[] connectorAllowedMasks;    // [type] -> bitmask of allowed 6-bit side configs (bit i -> config i allowed)
    public bool[] groupBuildEnabled;        // [type] -> can this type perform group build
    public int[] groupBuildTargetType;     // [type] -> type id to create
    public int[] groupBuildRequireNumber;  // [type] -> required count in cluster
    public bool[] groupBuildDeletion;       // [type] -> delete contributors on build
    public bool[] upgradeEnabled;           // [type] -> can perform upgrade
    public int[] upgradeTargetType;        // [type] -> replace with this type
    public int[] launcher_inputRange;      // [ability] -> range to pick a piece
    public int[] launcher_outputRange;     // [ability] -> range from launcher to drop target
    public bool[] launcher_friendlyFire;    // [ability] -> can launch friendlies
    public bool[] launcher_enemyFire;       // [ability] -> can launch enemies
    public bool[] push_TargetsBuildings;    // [ability] -> push can target buildings
    public bool[] push_TargetsSoldiers;     // [ability] -> push can target soldiers
    public int[] push_rangeMax;            // [ability] -> input range for push
    public int[] push_PushAmount;          // [ability] -> displacement distance
    public bool[] push_pull;                // [ability] -> invert direction
    public bool[] push_FriendlyFire;        // [ability] -> allow friendlies
    public int[] push_damage;              // [ability] -> damage on push
    public int[] spawn_pieceAmount;        // [ability] -> how many pieces to create
    public int[] spawn_targetType;         // [ability] -> type to create
    public int[] spawn_range;              // [ability] -> spawn range
    public bool[] spawn_onlyOncePerTurn;    // [ability] -> once-per-turn gate
    public bool[] multiCreate_enabledByType; // [type] -> multi-create hook
    public int[] multiCreate_amountByType;  // [type] -> how many total (including primary)
    public bool[] multiCreate_boarderingByType; // [type] -> require new pieces to border each other
    public int[] factory_amount;           // [ability] -> payout amount
    public bool[] factory_roundMultiplier;  // [ability] -> multiply by round number
    public bool[] factory_group;            // [ability] -> requires groups
    public int[] factory_groupAmount;      // [ability] -> size of each group
    public bool[] sanctuary_enabled;
    public int[] Sanctuary_range;
    public bool[] conversionFactory_coreHealth; // [ability] -> convert to core health
    public bool[] conversionFactory_vp;         // [ability] -> convert to VP
    public int[] conversionFactory_amount;      // [ability] -> amount converted
    public int[] conversionFactory_botSurcharge; // [ability] -> bot surcharge

    // Name maps (optional)
    public Dictionary<string, int> typeIndexByName;
    public string[] typeNameByIndex;

    // ====== Ability catalog (dense indices 0..abilityCount-1) ======
    public int abilityCount;

    public AbilityKind[] abilityKind;     // [abilityId]
    public TargetKind[] targetKind;      // [abilityId]

    // Generic params (unused = 0)
    public int[] rangeMin;                // [abilityId]
    public int[] rangeMax;                // [abilityId]
    public int[] areaRadius;              // [abilityId]
    public int[] damage;                  // [abilityId]
    public int[] customParam;             // [abilityId]
    public int[] sacrificeFactory_amount; // [abilityId] -> amount to add when sacrificing

    // NOTE: In Plan B, Create is NOT an ability. We keep buildTypeId only for legacy reads;
    // new OfferProvider should not depend on it for Create.
    public int[] buildTypeId;             // [abilityId] -> type index (legacy; not used for Create in Plan B)

    // Pricing surcharges (bot-only in Plan B)
    public int[] baseSurcharge;           // legacy; ignored by AbilitySurcharge()
    public int[] botThinkSurcharge;       // [abilityId] -> bot-only surcharge

    // Ability name maps (optional)
    public Dictionary<string, int> abilityIndexByName;
    public string[] abilityNameByIndex;

    // ====== Fixed-width ability slots per TYPE ======
    public int maxAbilitySlots = 7;       // slot 0: Move, 1: Shoot, 2: CaptureVP, 3: CoreDamage, others for custom
    public int[] abilityIdByTypeSlot;     // [type * maxAbilitySlots + slot] -> abilityId or -1
    public int[] abilitySlotCount;        // [type] -> # valid slots (0..maxAbilitySlots)

    // --------- Accessors / helpers (O(1), zero-alloc) ----------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GrantsDigit(byte type)
    {
        return (type < grantsDigitByType.Length) ? (int)grantsDigitByType[type] : -1;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetRequiredDigit(byte type)
    {
        var arr = codeDigitsByType[type];
        return (arr != null && arr.Length > 0) ? arr[0] : -1; // -1 = none
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AbilitySlotCount(byte type) => abilitySlotCount[type];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AbilityIdAtSlot(byte type, int slot)
    {
        if ((uint)slot >= (uint)maxAbilitySlots) return -1;
        return abilityIdByTypeSlot[type * maxAbilitySlots + slot];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetSlotIndexForAbility(byte type, int abilityId, out int slot)
    {
        int baseIdx = type * maxAbilitySlots;
        int limit = abilitySlotCount[type];
        for (int s = 0; s < limit; s++)
        {
            if (abilityIdByTypeSlot[baseIdx + s] == abilityId) { slot = s; return true; }
        }
        slot = -1; return false;
    }

    public bool HasAbilityKind(byte type, AbilityKind kind)
    {
        int limit = AbilitySlotCount(type);
        for (int s = 0; s < limit; s++)
        {
            int aid = AbilityIdAtSlot(type, s);
            if (aid < 0) continue;
            if (AbilityKindOf(aid) == kind) return true;
        }
        return false;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AbilityKind AbilityKindOf(int abilityId) => abilityKind[abilityId];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TargetKind TargetKindOf(int abilityId) => targetKind[abilityId];

    // ——— Added wrapper for OfferProvider convenience (type,slot) → AbilityKind as byte
    public const byte AbilityKindInvalid = byte.MaxValue;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetAbilityKind(byte type, int slot)
    {
        if ((uint)slot >= (uint)maxAbilitySlots) return AbilityKindInvalid;
        int abilityId = AbilityIdAtSlot(type, slot);
        if (abilityId < 0) return AbilityKindInvalid;
        return (byte)abilityKind[abilityId];
    }

    // ——— Plan B: Create is decoupled — per-type build info
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetBuildCost(byte type) => buildCostByType[type];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBuildable(byte type)
    {
        return type >= 0 && type < buildableByType.Length && buildableByType[type] != 0;
    }

    // ——— Legacy helper: expose created type from an ability id (not used for Create in Plan B)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetCreatedPieceType(int abilityId)
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
    public int AbilitySurcharge(int abilityId, bool applyBotSurcharges)
    {
        if (!applyBotSurcharges) return 0;
        return (abilityId >= 0 && abilityId < botThinkSurcharge.Length) ? botThinkSurcharge[abilityId] : 0;
    }



    // ====== Metadata helpers ======
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBuilding(byte type)
    {
        return type >= 0 && type < isBuildingByType.Length && isBuildingByType[type];
    }

    // ====== Connector helpers ======
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasConnectors(byte type)
    {
        return type < hasConnectorsByType.Length && hasConnectorsByType[type];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConnectorNeedsCapital(byte type)
    {
        return type < connectorNeedsCapital.Length && connectorNeedsCapital[type];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConnectorIsCapital(byte type)
    {
        return type < connectorIsCapital.Length && connectorIsCapital[type];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ConnectorCapitalHealth(byte type)
    {
        return type < connectorCapitalHealth.Length ? connectorCapitalHealth[type] : 0;
    }

    /// <summary>
    /// Returns true if the given 0..63 configuration index is allowed for this type.
    /// If mask is 0 and the type has connectors, treat it as "no configs allowed".
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConnectorConfigAllowed(byte type, int configIndex)
    {
        if (configIndex < 0 || configIndex >= 64) return false;
        if (type >= connectorAllowedMasks.Length) return false;
        ulong mask = connectorAllowedMasks[type];
        return (mask & (1UL << configIndex)) != 0;
    }

    // =====================================================================
    // ML-FRIENDLY LEGALITY KERNELS (Create is NOT an ability in Plan B)
    // No allocations; caller supplies buffers; return full counts (may exceed capacity).
    // =====================================================================

    /// <summary>
    /// MOVE structural legality: empty-only reachability; melee-on-move targets among enemies adjacent to reachable cells.
    /// Uses BoardModel's zero-alloc helpers (EnumerateReachableEmpty, GetNeighbors, etc.).
    /// </summary>
    public int GetLegalTargets_Move(BoardModel bm, int actorPieceId, int abilityId, int[] outTargets)
    {
        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return 0;

        int rmin = (abilityId >= 0 && abilityId < rangeMin.Length) ? rangeMin[abilityId] : 0;
        int rmax = (abilityId >= 0 && abilityId < rangeMax.Length) ? rangeMax[abilityId] : 0;
        if (rmax < rmin) { int t = rmax; rmax = rmin; rmin = t; }

        int count = 0;
        int cap = outTargets != null ? outTargets.Length : 0;

        // Reachable empty cells up to rmax
        int[] tmpReachable = bm.GetScratchCellBuffer();
        int reachCount = bm.EnumerateReachableEmpty(originCell, rmax, tmpReachable);

        // Emit EMPTY destinations (distance-filtered)
        for (int i = 0; i < reachCount; i++)
        {
            int cell = tmpReachable[i];
            int d = bm.Distance(originCell, cell);
            if (d >= rmin && d <= rmax)
            {
                if (count < cap) outTargets[count] = cell;
                count++;
            }
        }

        // Emit MELEE targets (enemy cells adjacent to any reachable empty approach cell)
        int meleeStart = count;

        // Direct adjacent melee (one-step onto enemy) when [rmin,rmax] includes 1
        if (rmin <= 1 && 1 <= rmax)
        {
            int[] neigh0 = bm.GetScratchNeighborBuffer();
            int n0 = bm.GetNeighbors(originCell, neigh0);
            int actorOwner0 = bm.GetPieceOwner(actorPieceId);
            for (int n = 0; n < n0; n++)
            {
                int tgt = neigh0[n];
                int pid = bm.GetCellOccupant(tgt);
                if (pid < 0) continue;
                if (bm.GetPieceOwner(pid) == actorOwner0) continue;
                // de-dup within melee segment
                bool seen = false;
                for (int k = meleeStart; k < count && k < cap; k++) { if (outTargets[k] == tgt) { seen = true; break; } }
                if (seen) continue;
                if (count < cap) outTargets[count] = tgt;
                count++;
            }
        }
        int[] neigh = bm.GetScratchNeighborBuffer();
        int actorOwner = bm.GetPieceOwner(actorPieceId);
        for (int i = 0; i < reachCount; i++)
        {
            int approach = tmpReachable[i];
            int steps = bm.Distance(originCell, approach);
            int nCount = bm.GetNeighbors(approach, neigh);
            for (int n = 0; n < nCount; n++)
            {
                int tgtCell = neigh[n];
                int pid = bm.GetCellOccupant(tgtCell);
                if (pid < 0) continue;
                if (bm.GetPieceOwner(pid) == actorOwner) continue;
                int total = steps + 1;
                if (total < rmin || total > rmax) continue;

                // de-dup within melee segment
                bool seen = false;
                for (int k = meleeStart; k < count && k < cap; k++) { if (outTargets[k] == tgtCell) { seen = true; break; } }
                if (seen) continue;

                if (count < cap) outTargets[count] = tgtCell;
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// SHOOT structural legality: enemy-only, range-filtered, LOS required; single-target only.
    /// </summary>
    public int GetLegalTargets_Shoot(BoardModel bm, int actorPieceId, int abilityId, int[] outTargets)
    {
        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return 0;
        int actorOwner = bm.GetPieceOwner(actorPieceId);

        int rmin = (abilityId >= 0 && abilityId < rangeMin.Length) ? rangeMin[abilityId] : 0;
        int rmax = (abilityId >= 0 && abilityId < rangeMax.Length) ? rangeMax[abilityId] : 0;
        if (rmax < rmin) { int t = rmax; rmax = rmin; rmin = t; }

        int cap = outTargets != null ? outTargets.Length : 0;
        int count = 0;

        int cellCount = bm.GetCellCount();
        for (int c = 0; c < cellCount; c++)
        {
            int pid = bm.GetCellOccupant(c);
            if (pid < 0) continue;
            if (bm.GetPieceOwner(pid) == actorOwner) continue;

            int d = bm.Distance(originCell, c);
            if (d < rmin || d > rmax) continue;
            if (!bm.LineOfSightClear(originCell, c)) continue;

            if (count < cap) outTargets[count] = pid; // pieceId target
            count++;
        }
        return count;
    }

    public int GetLegalTargets_SacrificeFactory(BoardModel bm, int actorPieceId, int abilityId, int[] outTargets)
    {
        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return 0;
        int actorOwner = bm.GetPieceOwner(actorPieceId);

        int rmin = (abilityId >= 0 && abilityId < rangeMin.Length) ? rangeMin[abilityId] : 0;
        int rmax = (abilityId >= 0 && abilityId < rangeMax.Length) ? rangeMax[abilityId] : 0;
        if (rmax < rmin) { int t = rmax; rmax = rmin; rmin = t; }

        int cap = outTargets != null ? outTargets.Length : 0;
        int count = 0;

        int cellCount = bm.GetCellCount();
        for (int c = 0; c < cellCount; c++)
        {
            int pid = bm.GetCellOccupant(c);
            if (pid < 0) continue;
            if (bm.GetPieceOwner(pid) != actorOwner) continue;

            int d = bm.Distance(originCell, c);
            if (d < rmin || d > rmax) continue;
            if (!bm.LineOfSightClear(originCell, c)) continue;

            if (count < cap) outTargets[count] = pid; // pieceId target
            count++;
        }
        return count;
    }


    public bool IsLegal_ConversionFactory(int PlayersVPAmount)
    {
        if (PlayersVPAmount <= 0) return false;
        return true;
    }



    /// <summary>
    /// CAPTURE VP (targetless): legal if actor stands on VP cell.
    /// </summary>
    public bool IsLegal_CaptureVP(BoardModel bm, int actorPieceId, int abilityId)
    {
        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return false;
        return originCell == bm.GetVictoryPointCellId();
    }

    /// <summary>
    /// CORE DAMAGE (targetless): legal if actor stands on an ENEMY core cell.
    /// </summary>
    public bool IsLegal_CoreDamage(BoardModel bm, int actorPieceId, int abilityId)
    {
        int originCell = bm.GetPieceCell(actorPieceId);
        if (originCell < 0) return false;
        int owner = bm.GetPieceOwner(actorPieceId);
        return bm.IsEnemyCoreCell(originCell, owner);
    }



    // ====== Minimal validation (call after CSV compile) ======
    public string ValidateBasic()
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
    public void Allocate(int typeCount, int abilityCount, int maxSlots = 4)
    {
        this.typeCount = typeCount;
        this.abilityCount = abilityCount;
        this.maxAbilitySlots = Math.Max(1, maxSlots);

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
        abilityIdByTypeSlot = new int[typeCount * this.maxAbilitySlots];
        for (int i = 0; i < abilityIdByTypeSlot.Length; i++) abilityIdByTypeSlot[i] = -1;
        abilitySlotCount = new int[typeCount];

        grantsDigitByType = new sbyte[typeCount];
        for (int i = 0; i < typeCount; i++) grantsDigitByType[i] = -1;
    }

    public void DefineType(int type, string stableName)
    {
        typeNameByIndex[type] = stableName;
        typeIndexByName[stableName] = type;
    }

    public void DefineAbility(int abilityId, string stableName, AbilityKind kind, TargetKind tkind)
    {
        abilityNameByIndex[abilityId] = stableName;
        abilityIndexByName[stableName] = abilityId;
        abilityKind[abilityId] = kind;
        targetKind[abilityId] = tkind;
        // numeric params set by importer after this
    }

    public void AddAbilitySlot(byte type, int abilityId)
    {
        int count = abilitySlotCount[type];
        if (count >= maxAbilitySlots) throw new InvalidOperationException($"Type {type} already has {count} slots (max {maxAbilitySlots}).");
        abilityIdByTypeSlot[type * maxAbilitySlots + count] = abilityId;
        abilitySlotCount[type] = count + 1;
    }
}
