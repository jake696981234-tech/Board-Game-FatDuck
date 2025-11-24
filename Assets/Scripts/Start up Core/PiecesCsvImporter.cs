// Assets/Scripts/Model/PiecesCsvImporter.cs (single-file importer)
//
// Purpose
// -------
// Import a SINGLE CSV (pieces.csv), where each row describes one piece type
// and toggles up to four canonical abilities (Move, Shoot, CaptureVP, DamageCore).
// The importer synthesizes ability records from the row parameters and wires
// them into fixed slots per type in a deterministic order (Move→Shoot→CaptureVP→DamageCore).
//
// Design notes
// ------------
// • Deterministic: IDs are assigned in CSV row order; synthesized abilities
//   are appended in the same pass, preserving ordering.
// • Zero allocations at runtime (only during import).
// • "Create" is DECOUPLED: no Create ability rows are emitted. Creation will
//   be handled by OfferProvider via (cell, pieceType) without a dedicated abilityId.
// • Optional columns are supported (e.g., maxHP, buildable). Missing columns
//   fall back to sensible defaults.
// • Minimal CSV parser: comma split, no quotes/escapes; supports comments (#...)
//   and trimming.
//
// Expected columns (header-based; order is flexible):
// name,faction,isBuilding,buildCost,digitsRequired,spritePath,moveColor,shootColor,
// move_enabled,move_rangeMin,move_rangeMax,
// shoot_enabled,shoot_rangeMin,shoot_rangeMax,shoot_damage,
// capture_enabled,core_enabled,
// botThinkSurcharge_move,botThinkSurcharge_shoot,botThinkSurcharge_capture,botThinkSurcharge_core,
// buildable,maxHP
//
// Public API
// ----------
//    public static Pieces Import(string pathToPiecesCsv,
//                               Action<int,string,bool,string,int>? onTypeDefined = null)
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public static class PiecesCsvImporter
{
    public static Pieces Import(string pathToPiecesCsv, Action<int, string, bool, string, int> onTypeDefined = null)
    {
        if (!File.Exists(pathToPiecesCsv))
            throw new FileNotFoundException($"pieces.csv not found: {pathToPiecesCsv}");

        // 1) First pass: read all rows (skipping comments/blanks) and capture headers
        var rows = new List<string[]>();
        string[] headers = Array.Empty<string>();
        foreach (var cols in ReadCsv(pathToPiecesCsv))
        {
            if (headers.Length == 0)
            {
                headers = cols; // first non-comment, non-empty line is the header
                continue;
            }
            rows.Add(cols);
        }

        int typeCount = rows.Count;
        // Per-type staging for non-canonical abilities/flags
        bool[] pushEnabledByType = new bool[typeCount];
        int[] pushRangeByType = new int[typeCount];
        int[] pushAmountByType = new int[typeCount];
        bool[] pushTargetsBuildingsByType = new bool[typeCount];
        bool[] pushTargetsSoldiersByType = new bool[typeCount];
        bool[] pushPullByType = new bool[typeCount];
        bool[] pushFriendlyByType = new bool[typeCount];
        int[] pushDamageByType = new int[typeCount];
        int[] pushBotSurchargeByType = new int[typeCount];

        bool[] gbEnabledByType = new bool[typeCount];
        string[] gbTargetNameByType = new string[typeCount];
        int[] gbRequireByType = new int[typeCount];
        bool[] gbDeletionByType = new bool[typeCount];

        bool[] launcherEnabledByType = new bool[typeCount];
        int[] launcherInputByType = new int[typeCount];
        int[] launcherOutputByType = new int[typeCount];
        bool[] launcherFriendlyByType = new bool[typeCount];
        bool[] launcherEnemyByType = new bool[typeCount];
        int[] launcherBotSurchargeByType = new int[typeCount];

        bool[] spawnEnabledByType = new bool[typeCount];
        string[] spawnPieceNameByType = new string[typeCount];
        int[] spawnAmountByType = new int[typeCount];
        int[] spawnRangeByType = new int[typeCount];
        bool[] spawnOncePerTurnByType = new bool[typeCount];
        int[] spawnBotSurchargeByType = new int[typeCount];

        bool[] factoryEnabledByType = new bool[typeCount];
        int[] factoryAmountByType = new int[typeCount];
        bool[] factoryRoundMulByType = new bool[typeCount];
        bool[] factoryGroupByType = new bool[typeCount];
        int[] factoryGroupAmountByType = new int[typeCount];
        int[] factoryBotSurchargeByType = new int[typeCount];

        bool[] mcEnabledByType = new bool[typeCount];
        int[] mcAmountByType = new int[typeCount];
        bool[] mcBorderByType = new bool[typeCount];

        // Worst-case: each row can enable up to 7 abilities (Move,Shoot,Capture,Core,Push/GroupBuild/Factory/etc.)
        int abilityEstimate = Math.Max(7, typeCount * 7);

        // 2) Allocate Pieces registry
        var pcs = new Pieces();
        pcs.Allocate(typeCount, abilityEstimate, /*maxSlots*/7);

        // We will SYNTHESIZE abilities; keep a moving cursor for the next id.
        int nextAbilityId = 0; // grows as we define abilities; finalized into pcs.abilityCount at the end

        // Header map for name→index
        var H = BuildHeaderIndex(headers);

        // Canonical slot order (by availability): Move(0), Shoot(1), CaptureVP(2), DamageCore(3)

        // 3) Define types first (stable typeId = row index)
        for (int typeId = 0; typeId < typeCount; typeId++)
        {
            var cols = rows[typeId];
            string name = Get(cols, H, "name", required: true);
            pcs.DefineType(typeId, name);

            // Basic descriptive fields (optional where noted)
            string faction = Get(cols, H, "faction", defaultValue: "");
            bool isBuilding = GetBool(cols, H, "isBuilding", false);
            int buildCost = GetInt(cols, H, "buildCost", 0);
            int digitsReq = GetInt(cols, H, "digitsRequired", 0);
            string spritePath = Get(cols, H, "spritePath", defaultValue: "");
            string moveColor = Get(cols, H, "moveColor", defaultValue: "");
            string shootColor = Get(cols, H, "shootColor", defaultValue: "");
            bool buildable = GetBool(cols, H, "buildable", true);
            int grantsDigit = GetInt(cols, H, "grantsDigit", -1);
            short maxHP = (short)ClampToShort(GetInt(cols, H, "maxHP", 10));
            bool hasConn = GetBool(cols, H, "hasConnectors", false);
            bool needsCap = GetBool(cols, H, "connector_needsCapital", false);
            bool isCap = GetBool(cols, H, "connector_isCapital", false);
            int capHp = GetInt(cols, H, "connector_capitalHealth", 0);
            ulong allowedMask = ParseConnectorMask(Get(cols, H, "connector_masks", defaultValue: string.Empty));
            bool pushEnabled = GetBool(cols, H, "push_enabled", false);
            int pushRange = GetInt(cols, H, "push_rangeMax", 1);
            int pushAmount = GetInt(cols, H, "push_pushAmount", 1);
            bool pushTargetsBuildings = GetBool(cols, H, "push_targetsBuildings", false);
            bool pushTargetsSoldiers = GetBool(cols, H, "push_targetsSoldiers", true);
            bool pushPull = GetBool(cols, H, "push_pull", false);
            bool pushFriendly = GetBool(cols, H, "push_friendlyFire", false);
            int pushDamage = GetInt(cols, H, "push_damage", 0);
            int pushBotSurcharge = GetInt(cols, H, "botThinkSurcharge_push", 0);
            bool gbEnabled = GetBool(cols, H, "groupBuild_enabled", false);
            string gbTargetName = Get(cols, H, "groupBuild_name", defaultValue: string.Empty);
            int gbRequire = GetInt(cols, H, "groupBuild_requireNumber", 0);
            bool gbDeletion = GetBool(cols, H, "groupBuild_deletion", false);
            bool launcherEnabled = GetBool(cols, H, "launcher_enabled", false);
            int launcherInput = GetInt(cols, H, "launcher_inputRange", 1);
            int launcherOutput = GetInt(cols, H, "launcher_outputRange", 1);
            bool launcherFriendly = GetBool(cols, H, "launcher_friendlyFire", false);
            bool launcherEnemy = GetBool(cols, H, "launcher_enemyFire", true);
            int launcherBotS = GetInt(cols, H, "botThinkSurcharge_launcher", 0);
            bool spawnEnabled = GetBool(cols, H, "spawn_enabled", false);
            string spawnPieceName = Get(cols, H, "spawn_pieceName", defaultValue: string.Empty);
            int spawnAmount = GetInt(cols, H, "spawn_pieceAmount", 1);
            int spawnRange = GetInt(cols, H, "spawn_range", 1);
            bool spawnOncePerTurn = GetBool(cols, H, "spawn_onlyOncePerTurn", false);
            int spawnBotS = GetInt(cols, H, "botThinkSurcharge_spawn", 0);
            bool factoryEnabled = GetBool(cols, H, "factory_enabled", false);
            int factoryAmount = GetInt(cols, H, "factory_amount", 0);
            bool factoryRoundMul = GetBool(cols, H, "factory_roundMultiplier", false);
            bool factoryGroup = GetBool(cols, H, "factory_group", false);
            int factoryGroupAmount = GetInt(cols, H, "factory_groupAmount", 1);
            bool mcEnabled = GetBool(cols, H, "multiCreate_enabled", false);
            int mcAmount = GetInt(cols, H, "multiCreate_amount", 1);
            bool mcBorder = GetBool(cols, H, "multiCreate_boardering", false);

            // Map to backing arrays if present in schema
            if (typeId < pcs.idByType.Length) pcs.idByType[typeId] = name;
            if (typeId < pcs.grantsDigitByType.Length) pcs.grantsDigitByType[typeId] = (sbyte)grantsDigit;
            if (typeId < pcs.displayNameByType.Length) pcs.displayNameByType[typeId] = name; // default display = name
            if (typeId < pcs.factionNameByType.Length) pcs.factionNameByType[typeId] = faction;
            if (typeId < pcs.spritePathByType.Length) pcs.spritePathByType[typeId] = spritePath;
            if (typeId < pcs.moveUIColorHexByType.Length) pcs.moveUIColorHexByType[typeId] = moveColor;
            if (typeId < pcs.shootUIColorHexByType.Length) pcs.shootUIColorHexByType[typeId] = shootColor;
            if (typeId < pcs.isBuildingByType.Length) pcs.isBuildingByType[typeId] = isBuilding;
            if (typeId < pcs.buildCostByType.Length) pcs.buildCostByType[typeId] = buildCost;
            if (typeId < pcs.maxHPByType.Length) pcs.maxHPByType[typeId] = maxHP;
            if (typeId < pcs.buildableByType.Length) pcs.buildableByType[typeId] = (byte)(buildable ? 1 : 0);
            if (typeId < pcs.hasConnectorsByType.Length) pcs.hasConnectorsByType[typeId] = hasConn;
            if (typeId < pcs.connectorNeedsCapital.Length) pcs.connectorNeedsCapital[typeId] = needsCap;
            if (typeId < pcs.connectorIsCapital.Length) pcs.connectorIsCapital[typeId] = isCap;
            if (typeId < pcs.connectorCapitalHealth.Length) pcs.connectorCapitalHealth[typeId] = capHp;
            if (typeId < pcs.connectorAllowedMasks.Length)
            {
                pcs.connectorAllowedMasks[typeId] =
                    (hasConn && allowedMask == 0UL) ? ulong.MaxValue : allowedMask;
            }
            if (gbEnabled && typeId < pcs.groupBuildEnabled.Length)
            {
                pcs.groupBuildEnabled[typeId] = true;
                pcs.groupBuildRequireNumber[typeId] = Math.Max(2, gbRequire);
                pcs.groupBuildDeletion[typeId] = gbDeletion;
                // target type resolved later after all types defined
                if (typeId < pcs.groupBuildTargetType.Length) pcs.groupBuildTargetType[typeId] = -1;
                if (!string.IsNullOrWhiteSpace(gbTargetName) && pcs.typeIndexByName != null && pcs.typeIndexByName.TryGetValue(gbTargetName, out var tgtId))
                    pcs.groupBuildTargetType[typeId] = tgtId;
            }
            // Stage per-type configs for second pass
            pushEnabledByType[typeId] = pushEnabled;
            pushRangeByType[typeId] = pushRange;
            pushAmountByType[typeId] = pushAmount;
            pushTargetsBuildingsByType[typeId] = pushTargetsBuildings;
            pushTargetsSoldiersByType[typeId] = pushTargetsSoldiers;
            pushPullByType[typeId] = pushPull;
            pushFriendlyByType[typeId] = pushFriendly;
            pushDamageByType[typeId] = pushDamage;
            pushBotSurchargeByType[typeId] = pushBotSurcharge;

            gbEnabledByType[typeId] = gbEnabled;
            gbTargetNameByType[typeId] = gbTargetName;
            gbRequireByType[typeId] = gbRequire;
            gbDeletionByType[typeId] = gbDeletion;

            launcherEnabledByType[typeId] = launcherEnabled;
            launcherInputByType[typeId] = launcherInput;
            launcherOutputByType[typeId] = launcherOutput;
            launcherFriendlyByType[typeId] = launcherFriendly;
            launcherEnemyByType[typeId] = launcherEnemy;
            launcherBotSurchargeByType[typeId] = launcherBotS;

            spawnEnabledByType[typeId] = spawnEnabled;
            spawnPieceNameByType[typeId] = spawnPieceName;
            spawnAmountByType[typeId] = spawnAmount;
            spawnRangeByType[typeId] = spawnRange;
            spawnOncePerTurnByType[typeId] = spawnOncePerTurn;
            spawnBotSurchargeByType[typeId] = spawnBotS;

            factoryEnabledByType[typeId] = factoryEnabled;
            factoryAmountByType[typeId] = factoryAmount;
            factoryRoundMulByType[typeId] = factoryRoundMul;
            factoryGroupByType[typeId] = factoryGroup;
            factoryGroupAmountByType[typeId] = factoryGroupAmount;
            factoryBotSurchargeByType[typeId] = GetInt(cols, H, "botThinkSurcharge_factory", 0);

            mcEnabledByType[typeId] = mcEnabled;
            mcAmountByType[typeId] = mcAmount;
            mcBorderByType[typeId] = mcBorder;

            // digitsRequired → store as single-element codeDigits list if your schema expects int[]
            if (typeId < pcs.codeDigitsByType.Length)
            {
                if (digitsReq > 0)
                    pcs.codeDigitsByType[typeId] = new int[1] { digitsReq };
                else
                    pcs.codeDigitsByType[typeId] = Array.Empty<int>();
            }
            // Emit to caller (pieceID, pieceName, isBuilding, faction, buildCost)
            onTypeDefined?.Invoke(typeId, name, isBuilding, faction, buildCost);
        }

        // 4) Synthesize abilities from row flags and wire into canonical slots
        for (int typeId = 0; typeId < typeCount; typeId++)
        {
            var cols = rows[typeId];
            string typeName = pcs.idByType[typeId];
            // pull staged per-type configs
            bool pushEnabled = pushEnabledByType[typeId];
            int pushRange = pushRangeByType[typeId];
            int pushAmount = pushAmountByType[typeId];
            bool pushTargetsBuildings = pushTargetsBuildingsByType[typeId];
            bool pushTargetsSoldiers = pushTargetsSoldiersByType[typeId];
            bool pushPull = pushPullByType[typeId];
            bool pushFriendly = pushFriendlyByType[typeId];
            int pushDamage = pushDamageByType[typeId];
            int pushBotSurcharge = pushBotSurchargeByType[typeId];

            bool gbEnabled = gbEnabledByType[typeId];
            string gbTargetName = gbTargetNameByType[typeId];
            int gbRequire = gbRequireByType[typeId];
            bool gbDeletion = gbDeletionByType[typeId];

            bool launcherEnabled = launcherEnabledByType[typeId];
            int launcherInput = launcherInputByType[typeId];
            int launcherOutput = launcherOutputByType[typeId];
            bool launcherFriendly = launcherFriendlyByType[typeId];
            bool launcherEnemy = launcherEnemyByType[typeId];
            int launcherBotS = launcherBotSurchargeByType[typeId];

            bool spawnEnabled = spawnEnabledByType[typeId];
            string spawnPieceName = spawnPieceNameByType[typeId];
            int spawnAmount = spawnAmountByType[typeId];
            int spawnRange = spawnRangeByType[typeId];
            bool spawnOncePerTurn = spawnOncePerTurnByType[typeId];
            int spawnBotS = spawnBotSurchargeByType[typeId];

            bool factoryEnabled = factoryEnabledByType[typeId];
            int factoryAmount = factoryAmountByType[typeId];
            bool factoryRoundMul = factoryRoundMulByType[typeId];
            bool factoryGroup = factoryGroupByType[typeId];
            int factoryGroupAmount = factoryGroupAmountByType[typeId];
            int factoryBotS = factoryBotSurchargeByType[typeId];

            bool mcEnabled = mcEnabledByType[typeId];
            int mcAmount = mcAmountByType[typeId];
            bool mcBorder = mcBorderByType[typeId];

            // MOVE
            if (GetBool(cols, H, "move_enabled", false))
            {
                int rMin = GetInt(cols, H, "move_rangeMin", 1);
                int rMax = GetInt(cols, H, "move_rangeMax", 1);
                int mDmg = GetInt(cols, H, "move_damage", 0); // optional; defaults to 0
                int abilityId = DefineSynthAbility_Move(pcs, typeName, rMin, rMax, mDmg,
                    ref nextAbilityId,
                    botSurcharge: GetInt(cols, H, "botThinkSurcharge_move", 0));
                pcs.AddAbilitySlot((byte)typeId, abilityId); // slot 0 by order
            }

            // SHOOT
            if (GetBool(cols, H, "shoot_enabled", false))
            {
                int rMin = GetInt(cols, H, "shoot_rangeMin", 1);
                int rMax = GetInt(cols, H, "shoot_rangeMax", 1);
                int dmg = GetInt(cols, H, "shoot_damage", 1);
                int abilityId = DefineSynthAbility_Shoot(pcs, typeName, rMin, rMax, dmg,
                    ref nextAbilityId,
                    botSurcharge: GetInt(cols, H, "botThinkSurcharge_shoot", 0));
                pcs.AddAbilitySlot((byte)typeId, abilityId); // slot 1 by order
            }

            // CAPTURE VP (standing on VP tile)
            if (GetBool(cols, H, "capture_enabled", false))
            {
                int abilityId = DefineSynthAbility_Capture(pcs, typeName,
                    ref nextAbilityId,
                    botSurcharge: GetInt(cols, H, "botThinkSurcharge_capture", 0));
                pcs.AddAbilitySlot((byte)typeId, abilityId); // slot 2 by order
            }

            // CORE DAMAGE (standing on enemy core)
            if (GetBool(cols, H, "core_enabled", false))
            {
                int abilityId = DefineSynthAbility_CoreDamage(pcs, typeName,
                    ref nextAbilityId,
                    botSurcharge: GetInt(cols, H, "botThinkSurcharge_core", 0));
                pcs.AddAbilitySlot((byte)typeId, abilityId); // slot 3 by order
            }

            // PUSH (piece-targeted)
            if (pushEnabled)
            {
                int abilityId = DefineSynthAbility_Push(
                    pcs,
                    typeName,
                    pushRange,
                    pushAmount,
                    pushTargetsBuildings,
                    pushTargetsSoldiers,
                    pushPull,
                    pushFriendly,
                    pushDamage,
                    ref nextAbilityId,
                    botSurcharge: pushBotSurcharge);
                pcs.AddAbilitySlot((byte)typeId, abilityId); // next slot by order
            }

            // GROUP BUILD (non-targeted; create target type)
            if (gbEnabled)
            {
                int abilityId = DefineSynthAbility_GroupBuild(
                    pcs,
                    typeName,
                    ref nextAbilityId,
                    botSurcharge: GetInt(cols, H, "botThinkSurcharge_groupBuild", 0));
                pcs.AddAbilitySlot((byte)typeId, abilityId);
            }

            // UPGRADE (self replace)
            if (GetBool(cols, H, "upgrade_enabled", false))
            {
                string upName = Get(cols, H, "upgrade_pieceName", defaultValue: string.Empty);
                int botS = GetInt(cols, H, "botThinkSurcharge_upgrade", 0);
                int abilityId = DefineSynthAbility_Upgrade(pcs, typeName, ref nextAbilityId, botS);
                pcs.AddAbilitySlot((byte)typeId, abilityId);
                if (!string.IsNullOrWhiteSpace(upName) && pcs.typeIndexByName != null && pcs.typeIndexByName.TryGetValue(upName, out var upTarget))
                {
                    pcs.upgradeEnabled[typeId] = true;
                    pcs.upgradeTargetType[typeId] = upTarget;
                }
            }

            // LAUNCHER
            if (launcherEnabled)
            {
                int abilityId = DefineSynthAbility_Launcher(
                    pcs,
                    typeName,
                    launcherInput,
                    launcherOutput,
                    launcherFriendly,
                    launcherEnemy,
                    ref nextAbilityId,
                    launcherBotS);
                pcs.AddAbilitySlot((byte)typeId, abilityId);
            }

            // SPAWNER
            if (spawnEnabled)
            {
                int abilityId = DefineSynthAbility_Spawner(
                    pcs,
                    typeName,
                    spawnAmount,
                    spawnRange,
                    spawnOncePerTurn,
                    ref nextAbilityId,
                    spawnBotS);
                pcs.AddAbilitySlot((byte)typeId, abilityId);
                if (!string.IsNullOrWhiteSpace(spawnPieceName) && pcs.typeIndexByName != null && pcs.typeIndexByName.TryGetValue(spawnPieceName, out var spawnTgt))
                {
                    pcs.spawn_targetType[abilityId] = spawnTgt;
                }
                else
                {
                    pcs.spawn_targetType[abilityId] = -1;
                }
                pcs.spawn_pieceAmount[abilityId] = Math.Max(1, spawnAmount);
                pcs.spawn_range[abilityId] = Math.Max(1, spawnRange);
                pcs.spawn_onlyOncePerTurn[abilityId] = spawnOncePerTurn;
            }

            // FACTORY (passive)
            if (factoryEnabled)
            {
                int abilityId = DefineSynthAbility_Factory(
                    pcs,
                    typeName,
                    factoryAmount,
                    factoryRoundMul,
                    factoryGroup,
                    factoryGroupAmount,
                    ref nextAbilityId,
                    botSurcharge: factoryBotS);
                pcs.AddAbilitySlot((byte)typeId, abilityId);
            }

            // MULTI CREATE (type-level flag)
            if (mcEnabled && typeId < pcs.multiCreate_enabledByType.Length)
            {
                pcs.multiCreate_enabledByType[typeId] = true;
                pcs.multiCreate_amountByType[typeId] = Math.Max(1, mcAmount);
                pcs.multiCreate_boarderingByType[typeId] = mcBorder;
            }
        }

        // Finalize ability count to actual number synthesized
        pcs.abilityCount = nextAbilityId;

        // 5) Finalize / validate
        string warn = pcs.ValidateBasic();
        if (!string.IsNullOrEmpty(warn))
            Log($"[PiecesCsvImporter] ValidateBasic warning: {warn}");

        // Build lookup maps if Pieces expects them post-define
        if (pcs.typeIndexByName != null)
        {
            for (int t = 0; t < pcs.typeCount; t++)
                pcs.typeIndexByName[pcs.idByType[t]] = t;
        }
        if (pcs.abilityIndexByName != null)
        {
            for (int a = 0; a < pcs.abilityCount; a++)
                pcs.abilityIndexByName[pcs.abilityNameByIndex[a]] = a;
        }

        Log($"[PiecesCsvImporter] Loaded {pcs.typeCount} types, {pcs.abilityCount} abilities (single-file).");
        return pcs;
    }

    // ---- Ability synthesizers ------------------------------------------------
    private static int DefineSynthAbility_Move(Pieces pcs, string typeName, int rangeMin, int rangeMax, int damage, ref int nextA, int botSurcharge)
    {
        int a = nextA++; // next synthesized id
        string name = $"Move@{typeName}";
        pcs.DefineAbility(a, name, Pieces.AbilityKind.Move, Pieces.TargetKind.None);
        if (a < pcs.rangeMin.Length) pcs.rangeMin[a] = rangeMin;
        if (a < pcs.rangeMax.Length) pcs.rangeMax[a] = rangeMax;
        if (a < pcs.areaRadius.Length) pcs.areaRadius[a] = 0;
        if (a < pcs.damage.Length) pcs.damage[a] = damage;
        if (a < pcs.customParam.Length) pcs.customParam[a] = 0;
        if (a < pcs.baseSurcharge.Length) pcs.baseSurcharge[a] = 0;
        if (a < pcs.botThinkSurcharge.Length) pcs.botThinkSurcharge[a] = botSurcharge;
        if (a < pcs.buildTypeId.Length) pcs.buildTypeId[a] = -1; // not used
        return a;
    }

    private static int DefineSynthAbility_Shoot(Pieces pcs, string typeName, int rangeMin, int rangeMax, int damage, ref int nextA, int botSurcharge)
    {
        int a = nextA++; // next synthesized id
        string name = $"Shoot@{typeName}";
        pcs.DefineAbility(a, name, Pieces.AbilityKind.Shoot, Pieces.TargetKind.Piece);
        if (a < pcs.rangeMin.Length) pcs.rangeMin[a] = rangeMin;
        if (a < pcs.rangeMax.Length) pcs.rangeMax[a] = rangeMax;
        if (a < pcs.areaRadius.Length) pcs.areaRadius[a] = 0;
        if (a < pcs.damage.Length) pcs.damage[a] = damage;
        if (a < pcs.customParam.Length) pcs.customParam[a] = 0;
        if (a < pcs.baseSurcharge.Length) pcs.baseSurcharge[a] = 0;
        if (a < pcs.botThinkSurcharge.Length) pcs.botThinkSurcharge[a] = botSurcharge;
        if (a < pcs.buildTypeId.Length) pcs.buildTypeId[a] = -1; // not used
        return a;
    }

    private static int DefineSynthAbility_Capture(Pieces pcs, string typeName, ref int nextA, int botSurcharge)
    {
        int a = nextA++; // use the moving cursor
        string name = $"CaptureVP@{typeName}";
        pcs.DefineAbility(a, name, Pieces.AbilityKind.CaptureVP, Pieces.TargetKind.Cell);
        if (a < pcs.rangeMin.Length) pcs.rangeMin[a] = 0;
        if (a < pcs.rangeMax.Length) pcs.rangeMax[a] = 0;
        if (a < pcs.areaRadius.Length) pcs.areaRadius[a] = 0;
        if (a < pcs.damage.Length) pcs.damage[a] = 0;
        if (a < pcs.customParam.Length) pcs.customParam[a] = 0;
        if (a < pcs.baseSurcharge.Length) pcs.baseSurcharge[a] = 0;
        if (a < pcs.botThinkSurcharge.Length) pcs.botThinkSurcharge[a] = botSurcharge;
        if (a < pcs.buildTypeId.Length) pcs.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_CoreDamage(Pieces pcs, string typeName, ref int nextA, int botSurcharge)
    {
        int a = nextA++; // use the moving cursor
        string name = $"CoreDamage@{typeName}";
        pcs.DefineAbility(a, name, Pieces.AbilityKind.CoreDamage, Pieces.TargetKind.Cell);
        if (a < pcs.rangeMin.Length) pcs.rangeMin[a] = 0;
        if (a < pcs.rangeMax.Length) pcs.rangeMax[a] = 0;
        if (a < pcs.areaRadius.Length) pcs.areaRadius[a] = 0;
        if (a < pcs.damage.Length) pcs.damage[a] = 0; // effect is contextual at kernel
        if (a < pcs.customParam.Length) pcs.customParam[a] = 0;
        if (a < pcs.baseSurcharge.Length) pcs.baseSurcharge[a] = 0;
        if (a < pcs.botThinkSurcharge.Length) pcs.botThinkSurcharge[a] = botSurcharge;
        if (a < pcs.buildTypeId.Length) pcs.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_GroupBuild(Pieces pcs, string typeName, ref int nextA, int botSurcharge)
    {
        int a = nextA++;
        string name = $"GroupBuild@{typeName}";
        pcs.DefineAbility(a, name, Pieces.AbilityKind.GroupBuild, Pieces.TargetKind.Cell);
        if (a < pcs.rangeMin.Length) pcs.rangeMin[a] = 0;
        if (a < pcs.rangeMax.Length) pcs.rangeMax[a] = 0;
        if (a < pcs.damage.Length) pcs.damage[a] = 0;
        if (a < pcs.botThinkSurcharge.Length) pcs.botThinkSurcharge[a] = botSurcharge;
        if (a < pcs.buildTypeId.Length) pcs.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_Push(
        Pieces pcs,
        string typeName,
        int rangeMax,
        int pushAmount,
        bool targetsBuildings,
        bool targetsSoldiers,
        bool pull,
        bool friendlyFire,
        int damage,
        ref int nextA,
        int botSurcharge)
    {
        int a = nextA++;
        string name = $"Push@{typeName}";
        pcs.DefineAbility(a, name, Pieces.AbilityKind.Push, Pieces.TargetKind.Piece);
        if (a < pcs.rangeMin.Length) pcs.rangeMin[a] = 1;
        if (a < pcs.rangeMax.Length) pcs.rangeMax[a] = rangeMax;
        if (a < pcs.damage.Length) pcs.damage[a] = damage;
        if (a < pcs.push_TargetsBuildings.Length) pcs.push_TargetsBuildings[a] = targetsBuildings;
        if (a < pcs.push_TargetsSoldiers.Length) pcs.push_TargetsSoldiers[a] = targetsSoldiers;
        if (a < pcs.push_rangeMax.Length) pcs.push_rangeMax[a] = rangeMax;
        if (a < pcs.push_PushAmount.Length) pcs.push_PushAmount[a] = pushAmount;
        if (a < pcs.push_pull.Length) pcs.push_pull[a] = pull;
        if (a < pcs.push_FriendlyFire.Length) pcs.push_FriendlyFire[a] = friendlyFire;
        if (a < pcs.push_damage.Length) pcs.push_damage[a] = damage;
        if (a < pcs.botThinkSurcharge.Length) pcs.botThinkSurcharge[a] = botSurcharge;
        if (a < pcs.buildTypeId.Length) pcs.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_Upgrade(Pieces pcs, string typeName, ref int nextA, int botSurcharge)
    {
        int a = nextA++;
        string name = $"Upgrade@{typeName}";
        pcs.DefineAbility(a, name, Pieces.AbilityKind.Upgrade, Pieces.TargetKind.Cell);
        if (a < pcs.rangeMin.Length) pcs.rangeMin[a] = 0;
        if (a < pcs.rangeMax.Length) pcs.rangeMax[a] = 0;
        if (a < pcs.damage.Length) pcs.damage[a] = 0;
        if (a < pcs.botThinkSurcharge.Length) pcs.botThinkSurcharge[a] = botSurcharge;
        if (a < pcs.buildTypeId.Length) pcs.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_Launcher(
        Pieces pcs,
        string typeName,
        int inputRange,
        int outputRange,
        bool friendlyFire,
        bool enemyFire,
        ref int nextA,
        int botSurcharge)
    {
        int a = nextA++;
        string name = $"Launcher@{typeName}";
        pcs.DefineAbility(a, name, Pieces.AbilityKind.Launcher, Pieces.TargetKind.Piece);
        if (a < pcs.rangeMin.Length) pcs.rangeMin[a] = 1;
        if (a < pcs.rangeMax.Length) pcs.rangeMax[a] = Math.Max(inputRange, outputRange);
        if (a < pcs.launcher_inputRange.Length) pcs.launcher_inputRange[a] = inputRange;
        if (a < pcs.launcher_outputRange.Length) pcs.launcher_outputRange[a] = outputRange;
        if (a < pcs.launcher_friendlyFire.Length) pcs.launcher_friendlyFire[a] = friendlyFire;
        if (a < pcs.launcher_enemyFire.Length) pcs.launcher_enemyFire[a] = enemyFire;
        if (a < pcs.botThinkSurcharge.Length) pcs.botThinkSurcharge[a] = botSurcharge;
        if (a < pcs.buildTypeId.Length) pcs.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_Spawner(
        Pieces pcs,
        string typeName,
        int pieceAmount,
        int range,
        bool oncePerTurn,
        ref int nextA,
        int botSurcharge)
    {
        int a = nextA++;
        string name = $"Spawner@{typeName}";
        pcs.DefineAbility(a, name, Pieces.AbilityKind.Spawner, Pieces.TargetKind.Cell);
        if (a < pcs.rangeMin.Length) pcs.rangeMin[a] = 1;
        if (a < pcs.rangeMax.Length) pcs.rangeMax[a] = range;
        if (a < pcs.spawn_pieceAmount.Length) pcs.spawn_pieceAmount[a] = pieceAmount;
        if (a < pcs.spawn_range.Length) pcs.spawn_range[a] = range;
        if (a < pcs.spawn_onlyOncePerTurn.Length) pcs.spawn_onlyOncePerTurn[a] = oncePerTurn;
        if (a < pcs.botThinkSurcharge.Length) pcs.botThinkSurcharge[a] = botSurcharge;
        if (a < pcs.buildTypeId.Length) pcs.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_Factory(
        Pieces pcs,
        string typeName,
        int amount,
        bool roundMul,
        bool group,
        int groupAmount,
        ref int nextA,
        int botSurcharge)
    {
        int a = nextA++;
        string name = $"Factory@{typeName}";
        pcs.DefineAbility(a, name, Pieces.AbilityKind.Factory, Pieces.TargetKind.None);
        if (a < pcs.factory_amount.Length) pcs.factory_amount[a] = amount;
        if (a < pcs.factory_roundMultiplier.Length) pcs.factory_roundMultiplier[a] = roundMul;
        if (a < pcs.factory_group.Length) pcs.factory_group[a] = group;
        if (a < pcs.factory_groupAmount.Length) pcs.factory_groupAmount[a] = Math.Max(1, groupAmount);
        if (a < pcs.botThinkSurcharge.Length) pcs.botThinkSurcharge[a] = botSurcharge;
        if (a < pcs.buildTypeId.Length) pcs.buildTypeId[a] = -1;
        return a;
    }

    // ---- CSV helpers --------------------------------------------------------
    private static IEnumerable<string[]> ReadCsv(string path)
    {
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var sr = new StreamReader(fs, DetectEncoding(fs), true, 1 << 16))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed[0] == '#') continue; // comment

                var cols = trimmed.Split(',');
                for (int i = 0; i < cols.Length; i++) cols[i] = cols[i].Trim();
                yield return cols;
            }
        }
    }

    private static Dictionary<string, int> BuildHeaderIndex(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            string h = headers[i]?.Trim() ?? string.Empty;
            if (h.Length == 0) continue;
            map[h] = i;
        }
        return map;
    }

    private static string Get(string[] cols, Dictionary<string, int> H, string key, bool required = false, string defaultValue = "")
    {
        if (H.TryGetValue(key, out int idx) && idx >= 0 && idx < cols.Length)
        {
            return cols[idx];
        }
        if (required)
            throw new InvalidDataException($"Missing required column '{key}'");
        return defaultValue;
    }

    private static int GetInt(string[] cols, Dictionary<string, int> H, string key, int defaultValue)
    {
        string s = Get(cols, H, key, required: false, defaultValue: string.Empty);
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
        return defaultValue;
    }

    private static bool GetBool(string[] cols, Dictionary<string, int> H, string key, bool defaultValue)
    {
        string s = Get(cols, H, key, required: false, defaultValue: string.Empty);
        if (string.IsNullOrWhiteSpace(s)) return defaultValue;
        s = s.Trim();
        if (s.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        if (s == "1") return true;
        if (s == "0") return false;
        return defaultValue;
    }

    private static int ClampToShort(int v) => Math.Max(short.MinValue, Math.Min(short.MaxValue, v));

    /// <summary>
    /// Parses a comma-separated list of config indices (0-63) into a 64-bit mask.
    /// Empty/invalid input returns 0.
    /// </summary>
    private static ulong ParseConnectorMask(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0UL;
        ulong mask = 0UL;
        var parts = s.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
            {
                if (idx >= 0 && idx < 64)
                    mask |= (1UL << idx);
            }
        }
        return mask;
    }

    private static Encoding DetectEncoding(FileStream fs)
    {
        if (fs.Length >= 3)
        {
            long pos = fs.Position;
            fs.Position = 0;
            int b0 = fs.ReadByte();
            int b1 = fs.ReadByte();
            int b2 = fs.ReadByte();
            fs.Position = pos;
            if (b0 == 0xEF && b1 == 0xBB && b2 == 0xBF) return new UTF8Encoding(true);
        }
        if (fs.Length >= 2)
        {
            long pos = fs.Position;
            fs.Position = 0;
            int b0 = fs.ReadByte();
            int b1 = fs.ReadByte();
            fs.Position = pos;
            if (b0 == 0xFF && b1 == 0xFE) return Encoding.Unicode;
            if (b0 == 0xFE && b1 == 0xFF) return Encoding.BigEndianUnicode;
        }
        return new UTF8Encoding(false);
    }

    private static void Log(string msg) => Console.WriteLine(msg);
}
