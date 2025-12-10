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
// buildable,maxHP,sanctuary_enabled,Sanctuary_range,
// SacrificeFactory_enabled,SacrificeFactory_MinRange,SacrificeFactory_MaxRange,SacrificeFactory_Amount,SacrificeFactory_BotSurcharges,
// ConversionFactory_enabled,ConversionFactory_CoreHealth,ConversionFactory_VP,ConversionFactory_Amount,ConversionFactory_BotSurcharge,
// eat_enabled,eat_amount
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
    public static void Import(string pathToPiecesCsv, Action<int, string, bool, string, int> onTypeDefined = null)
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

        bool[] sanctuaryEnabledByType = new bool[typeCount];
        int[] sanctuaryRangeByType = new int[typeCount];

        bool[] sacrificeFactoryEnabledByType = new bool[typeCount];
        int[] sacrificeFactoryRangeMinByType = new int[typeCount];
        int[] sacrificeFactoryRangeMaxByType = new int[typeCount];
        int[] sacrificeFactoryAmountByType = new int[typeCount];
        int[] sacrificeFactoryBotSurchargeByType = new int[typeCount];

        bool[] conversionFactoryEnabledByType = new bool[typeCount];
        bool[] conversionFactoryCoreHealthByType = new bool[typeCount];
        bool[] conversionFactoryVpByType = new bool[typeCount];
        int[] conversionFactoryAmountByType = new int[typeCount];
        int[] conversionFactoryBotSurchargeByType = new int[typeCount];
        bool[] eatEnabledByType = new bool[typeCount];
        int[] eatAmountByType = new int[typeCount];

        // Worst-case: each row can enable many abilities (Move,Shoot,Capture,Core,Push,GroupBuild,Upgrade,Launcher,Spawner,Factory,Sanctuary, etc.)
        int abilityEstimate = Math.Max(10, typeCount * 10);

        // 2) Allocate Pieces registry
        Pieces.Allocate(typeCount, abilityEstimate, /*maxSlots*/10);

        // We will SYNTHESIZE abilities; keep a moving cursor for the next id.
        int nextAbilityId = 0; // grows as we define abilities; finalized into Pieces.abilityCount at the end

        // Header map for name→index
        var H = BuildHeaderIndex(headers);

        // Canonical slot order (by availability): Move(0), Shoot(1), CaptureVP(2), DamageCore(3)

        // 3) Define types first (stable typeId = row index)
        for (int typeId = 0; typeId < typeCount; typeId++)
        {
            var cols = rows[typeId];
            string name = Get(cols, H, "name", required: true);
            Pieces.DefineType(typeId, name);

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
            bool sanctuaryEnabled = GetBool(cols, H, "sanctuary_enabled", false);
            int sanctuaryRange = GetInt(cols, H, "Sanctuary_range", 0);
            bool sacrificeFactoryEnabled = GetBool(cols, H, "SacrificeFactory_enabled", false);
            int sacrificeFactoryMinRange = GetInt(cols, H, "SacrificeFactory_MinRange", 1);
            int sacrificeFactoryMaxRange = GetInt(cols, H, "SacrificeFactory_MaxRange", 1);
            int sacrificeFactoryAmount = GetInt(cols, H, "SacrificeFactory_Amount", 0);
            int sacrificeFactoryBotSurcharge = GetInt(cols, H, "SacrificeFactory_BotSurcharges", 0);
            bool conversionFactoryEnabled = GetBool(cols, H, "ConversionFactory_enabled", false);
            bool conversionFactoryCoreHealth = GetBool(cols, H, "ConversionFactory_CoreHealth", false);
            bool conversionFactoryVp = GetBool(cols, H, "ConversionFactory_VP", false);
            int conversionFactoryAmount = GetInt(cols, H, "ConversionFactory_Amount", 0);
            int conversionFactoryBotSurcharge = GetInt(cols, H, "ConversionFactory_BotSurcharge", 0);
            bool eatEnabled = GetBool(cols, H, "eat_enabled", GetBool(cols, H, "EatEnabled", false));
            int eatAmount = GetInt(cols, H, "eat_amount", GetInt(cols, H, "Eat_Amount", 0));

            // Map to backing arrays if present in schema
            if (typeId < Pieces.idByType.Length) Pieces.idByType[typeId] = name;
            if (typeId < Pieces.grantsDigitByType.Length) Pieces.grantsDigitByType[typeId] = (sbyte)grantsDigit;
            if (typeId < Pieces.displayNameByType.Length) Pieces.displayNameByType[typeId] = name; // default display = name
            if (typeId < Pieces.factionNameByType.Length) Pieces.factionNameByType[typeId] = faction;
            if (typeId < Pieces.spritePathByType.Length) Pieces.spritePathByType[typeId] = spritePath;
            if (typeId < Pieces.moveUIColorHexByType.Length) Pieces.moveUIColorHexByType[typeId] = moveColor;
            if (typeId < Pieces.shootUIColorHexByType.Length) Pieces.shootUIColorHexByType[typeId] = shootColor;
            if (typeId < Pieces.isBuildingByType.Length) Pieces.isBuildingByType[typeId] = isBuilding;
            if (typeId < Pieces.buildCostByType.Length) Pieces.buildCostByType[typeId] = buildCost;
            if (typeId < Pieces.maxHPByType.Length) Pieces.maxHPByType[typeId] = maxHP;
            if (typeId < Pieces.buildableByType.Length) Pieces.buildableByType[typeId] = (byte)(buildable ? 1 : 0);
            if (typeId < Pieces.hasConnectorsByType.Length) Pieces.hasConnectorsByType[typeId] = hasConn;
            if (typeId < Pieces.connectorNeedsCapital.Length) Pieces.connectorNeedsCapital[typeId] = needsCap;
            if (typeId < Pieces.connectorIsCapital.Length) Pieces.connectorIsCapital[typeId] = isCap;
            if (typeId < Pieces.connectorCapitalHealth.Length) Pieces.connectorCapitalHealth[typeId] = capHp;
            if (typeId < Pieces.connectorAllowedMasks.Length)
            {
                Pieces.connectorAllowedMasks[typeId] =
                    (hasConn && allowedMask == 0UL) ? ulong.MaxValue : allowedMask;
            }
            if (gbEnabled && typeId < Pieces.groupBuildEnabled.Length)
            {
                Pieces.groupBuildEnabled[typeId] = true;
                Pieces.groupBuildRequireNumber[typeId] = Math.Max(2, gbRequire);
                Pieces.groupBuildDeletion[typeId] = gbDeletion;
                // target type resolved later after all types defined
                if (typeId < Pieces.groupBuildTargetType.Length) Pieces.groupBuildTargetType[typeId] = -1;
                if (!string.IsNullOrWhiteSpace(gbTargetName) && Pieces.typeIndexByName != null && Pieces.typeIndexByName.TryGetValue(gbTargetName, out var tgtId))
                    Pieces.groupBuildTargetType[typeId] = tgtId;
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
            sanctuaryEnabledByType[typeId] = sanctuaryEnabled;
            sanctuaryRangeByType[typeId] = sanctuaryRange;
            sacrificeFactoryEnabledByType[typeId] = sacrificeFactoryEnabled;
            sacrificeFactoryRangeMinByType[typeId] = sacrificeFactoryMinRange;
            sacrificeFactoryRangeMaxByType[typeId] = sacrificeFactoryMaxRange;
            sacrificeFactoryAmountByType[typeId] = sacrificeFactoryAmount;
            sacrificeFactoryBotSurchargeByType[typeId] = sacrificeFactoryBotSurcharge;
            conversionFactoryEnabledByType[typeId] = conversionFactoryEnabled;
            conversionFactoryCoreHealthByType[typeId] = conversionFactoryCoreHealth;
            conversionFactoryVpByType[typeId] = conversionFactoryVp;
            conversionFactoryAmountByType[typeId] = conversionFactoryAmount;
            conversionFactoryBotSurchargeByType[typeId] = conversionFactoryBotSurcharge;
            eatEnabledByType[typeId] = eatEnabled;
            eatAmountByType[typeId] = eatAmount;

            // digitsRequired → store as single-element codeDigits list if your schema expects int[]
            if (typeId < Pieces.codeDigitsByType.Length)
            {
                if (digitsReq > 0)
                    Pieces.codeDigitsByType[typeId] = new int[1] { digitsReq };
                else
                    Pieces.codeDigitsByType[typeId] = Array.Empty<int>();
            }
            // Emit to caller (pieceID, pieceName, isBuilding, faction, buildCost)
            onTypeDefined?.Invoke(typeId, name, isBuilding, faction, buildCost);
        }

        // 4) Synthesize abilities from row flags and wire into canonical slots
        for (int typeId = 0; typeId < typeCount; typeId++)
        {
            var cols = rows[typeId];
            string typeName = Pieces.idByType[typeId];
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
            bool sanctuaryEnabled = sanctuaryEnabledByType[typeId];
            int sanctuaryRange = sanctuaryRangeByType[typeId];
            bool sacrificeFactoryEnabled = sacrificeFactoryEnabledByType[typeId];
            int sacrificeFactoryMinRange = sacrificeFactoryRangeMinByType[typeId];
            int sacrificeFactoryMaxRange = sacrificeFactoryRangeMaxByType[typeId];
            int sacrificeFactoryAmount = sacrificeFactoryAmountByType[typeId];
            int sacrificeFactoryBotSurcharge = sacrificeFactoryBotSurchargeByType[typeId];
            bool conversionFactoryEnabled = conversionFactoryEnabledByType[typeId];
            bool conversionFactoryCoreHealth = conversionFactoryCoreHealthByType[typeId];
            bool conversionFactoryVp = conversionFactoryVpByType[typeId];
            int conversionFactoryAmount = conversionFactoryAmountByType[typeId];
            int conversionFactoryBotSurcharge = conversionFactoryBotSurchargeByType[typeId];
            bool eatEnabled = eatEnabledByType[typeId];
            int eatAmount = eatAmountByType[typeId];

            // MOVE
            if (GetBool(cols, H, "move_enabled", false))
            {
                int rMin = GetInt(cols, H, "move_rangeMin", 1);
                int rMax = GetInt(cols, H, "move_rangeMax", 1);
                int mDmg = GetInt(cols, H, "move_damage", 0); // optional; defaults to 0
                int abilityId = DefineSynthAbility_Move(typeName, rMin, rMax, mDmg,
                    ref nextAbilityId,
                    botSurcharge: GetInt(cols, H, "botThinkSurcharge_move", 0));
                Pieces.AddAbilitySlot((byte)typeId, abilityId); // slot 0 by order
            }

            // SHOOT
            if (GetBool(cols, H, "shoot_enabled", false))
            {
                int rMin = GetInt(cols, H, "shoot_rangeMin", 1);
                int rMax = GetInt(cols, H, "shoot_rangeMax", 1);
                int dmg = GetInt(cols, H, "shoot_damage", 1);
                int abilityId = DefineSynthAbility_Shoot(typeName, rMin, rMax, dmg,
                    ref nextAbilityId,
                    botSurcharge: GetInt(cols, H, "botThinkSurcharge_shoot", 0));
                Pieces.AddAbilitySlot((byte)typeId, abilityId); // slot 1 by order
            }

            // CAPTURE VP (standing on VP tile)
            if (GetBool(cols, H, "capture_enabled", false))
            {
                int abilityId = DefineSynthAbility_Capture(typeName,
                    ref nextAbilityId,
                    botSurcharge: GetInt(cols, H, "botThinkSurcharge_capture", 0));
                Pieces.AddAbilitySlot((byte)typeId, abilityId); // slot 2 by order
            }

            // CORE DAMAGE (standing on enemy core)
            if (GetBool(cols, H, "core_enabled", false))
            {
                int abilityId = DefineSynthAbility_CoreDamage(typeName,
                    ref nextAbilityId,
                    botSurcharge: GetInt(cols, H, "botThinkSurcharge_core", 0));
                Pieces.AddAbilitySlot((byte)typeId, abilityId); // slot 3 by order
            }

            // PUSH (piece-targeted)
            if (pushEnabled)
            {
                int abilityId = DefineSynthAbility_Push(
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
                Pieces.AddAbilitySlot((byte)typeId, abilityId); // next slot by order
            }

            // GROUP BUILD (non-targeted; create target type)
            if (gbEnabled)
            {
                int abilityId = DefineSynthAbility_GroupBuild(
                    typeName,
                    ref nextAbilityId,
                    botSurcharge: GetInt(cols, H, "botThinkSurcharge_groupBuild", 0));
                Pieces.AddAbilitySlot((byte)typeId, abilityId);
            }

            // UPGRADE (self replace)
            if (GetBool(cols, H, "upgrade_enabled", false))
            {
                string upName = Get(cols, H, "upgrade_pieceName", defaultValue: string.Empty);
                int botS = GetInt(cols, H, "botThinkSurcharge_upgrade", 0);
                int abilityId = DefineSynthAbility_Upgrade(typeName, ref nextAbilityId, botS);
                Pieces.AddAbilitySlot((byte)typeId, abilityId);
                if (!string.IsNullOrWhiteSpace(upName) && Pieces.typeIndexByName != null && Pieces.typeIndexByName.TryGetValue(upName, out var upTarget))
                {
                    Pieces.upgradeEnabled[typeId] = true;
                    Pieces.upgradeTargetType[typeId] = upTarget;
                }
            }

            // LAUNCHER
            if (launcherEnabled)
            {
                int abilityId = DefineSynthAbility_Launcher(
                    typeName,
                    launcherInput,
                    launcherOutput,
                    launcherFriendly,
                    launcherEnemy,
                    ref nextAbilityId,
                    launcherBotS);
                Pieces.AddAbilitySlot((byte)typeId, abilityId);
            }

            // SPAWNER
            if (spawnEnabled)
            {
                int abilityId = DefineSynthAbility_Spawner(
                    typeName,
                    spawnAmount,
                    spawnRange,
                    spawnOncePerTurn,
                    ref nextAbilityId,
                    spawnBotS);
                Pieces.AddAbilitySlot((byte)typeId, abilityId);
                if (!string.IsNullOrWhiteSpace(spawnPieceName) && Pieces.typeIndexByName != null && Pieces.typeIndexByName.TryGetValue(spawnPieceName, out var spawnTgt))
                {
                    Pieces.spawn_targetType[abilityId] = spawnTgt;
                }
                else
                {
                    Pieces.spawn_targetType[abilityId] = -1;
                }
                Pieces.spawn_pieceAmount[abilityId] = Math.Max(1, spawnAmount);
                Pieces.spawn_range[abilityId] = Math.Max(1, spawnRange);
                Pieces.spawn_onlyOncePerTurn[abilityId] = spawnOncePerTurn;
            }

            // FACTORY (passive)
            if (factoryEnabled)
            {
                int abilityId = DefineSynthAbility_Factory(
                    typeName,
                    factoryAmount,
                    factoryRoundMul,
                    factoryGroup,
                    factoryGroupAmount,
                    ref nextAbilityId,
                    botSurcharge: factoryBotS);
                Pieces.AddAbilitySlot((byte)typeId, abilityId);
            }

            // EAT (passive)
            if (eatEnabled)
            {
                int abilityId = DefineSynthAbility_Eat(
                    typeName,
                    eatAmount,
                    ref nextAbilityId);
                Pieces.AddAbilitySlot((byte)typeId, abilityId);
            }

            // SANCTUARY
            if (sanctuaryEnabled)
            {
                int abilityId = DefineSynthAbility_Sanctuary(
                    typeName,
                    sanctuaryRange,
                    ref nextAbilityId);
                Pieces.AddAbilitySlot((byte)typeId, abilityId);
            }

            // SACRIFICE FACTORY (active)
            if (sacrificeFactoryEnabled)
            {
                int abilityId = DefineSynthAbility_SacrificeFactory(
                    typeName,
                    sacrificeFactoryMinRange,
                    sacrificeFactoryMaxRange,
                    sacrificeFactoryAmount,
                    ref nextAbilityId,
                    sacrificeFactoryBotSurcharge);
                Pieces.AddAbilitySlot((byte)typeId, abilityId);
            }

            // CONVERSION FACTORY (active)
            if (conversionFactoryEnabled)
            {
                int abilityId = DefineSynthAbility_ConversionFactory(
                    typeName,
                    conversionFactoryCoreHealth,
                    conversionFactoryVp,
                    conversionFactoryAmount,
                    ref nextAbilityId,
                    conversionFactoryBotSurcharge);
                Pieces.AddAbilitySlot((byte)typeId, abilityId);
            }

            // MULTI CREATE (type-level flag)
            if (mcEnabled && typeId < Pieces.multiCreate_enabledByType.Length)
            {
                Pieces.multiCreate_enabledByType[typeId] = true;
                Pieces.multiCreate_amountByType[typeId] = Math.Max(1, mcAmount);
                Pieces.multiCreate_boarderingByType[typeId] = mcBorder;
            }
        }

        // Finalize ability count to actual number synthesized
        Pieces.abilityCount = nextAbilityId;

        // 5) Finalize / validate
        string warn = Pieces.ValidateBasic();
        if (!string.IsNullOrEmpty(warn))
            Log($"[PiecesCsvImporter] ValidateBasic warning: {warn}");

        // Build lookup maps if Pieces expects them post-define
        if (Pieces.typeIndexByName != null)
        {
            for (int t = 0; t < Pieces.typeCount; t++)
                Pieces.typeIndexByName[Pieces.idByType[t]] = t;
        }
        if (Pieces.abilityIndexByName != null)
        {
            for (int a = 0; a < Pieces.abilityCount; a++)
                Pieces.abilityIndexByName[Pieces.abilityNameByIndex[a]] = a;
        }

        Log($"[PiecesCsvImporter] Loaded {Pieces.typeCount} types, {Pieces.abilityCount} abilities (single-file).");
    }

    // ---- Ability synthesizers ------------------------------------------------
    private static int DefineSynthAbility_Move(string typeName, int rangeMin, int rangeMax, int damage, ref int nextA, int botSurcharge)
    {
        int a = nextA++; // next synthesized id
        string name = $"Move@{typeName}";
        Pieces.DefineAbility(a, name, Pieces.AbilityKind.Move, Pieces.TargetKind.None);
        if (a < Pieces.rangeMin.Length) Pieces.rangeMin[a] = rangeMin;
        if (a < Pieces.rangeMax.Length) Pieces.rangeMax[a] = rangeMax;
        if (a < Pieces.areaRadius.Length) Pieces.areaRadius[a] = 0;
        if (a < Pieces.damage.Length) Pieces.damage[a] = damage;
        if (a < Pieces.customParam.Length) Pieces.customParam[a] = 0;
        if (a < Pieces.baseSurcharge.Length) Pieces.baseSurcharge[a] = 0;
        if (a < Pieces.botThinkSurcharge.Length) Pieces.botThinkSurcharge[a] = botSurcharge;
        if (a < Pieces.buildTypeId.Length) Pieces.buildTypeId[a] = -1; // not used
        return a;
    }

    private static int DefineSynthAbility_Shoot(string typeName, int rangeMin, int rangeMax, int damage, ref int nextA, int botSurcharge)
    {
        int a = nextA++; // next synthesized id
        string name = $"Shoot@{typeName}";
        Pieces.DefineAbility(a, name, Pieces.AbilityKind.Shoot, Pieces.TargetKind.Piece);
        if (a < Pieces.rangeMin.Length) Pieces.rangeMin[a] = rangeMin;
        if (a < Pieces.rangeMax.Length) Pieces.rangeMax[a] = rangeMax;
        if (a < Pieces.areaRadius.Length) Pieces.areaRadius[a] = 0;
        if (a < Pieces.damage.Length) Pieces.damage[a] = damage;
        if (a < Pieces.customParam.Length) Pieces.customParam[a] = 0;
        if (a < Pieces.baseSurcharge.Length) Pieces.baseSurcharge[a] = 0;
        if (a < Pieces.botThinkSurcharge.Length) Pieces.botThinkSurcharge[a] = botSurcharge;
        if (a < Pieces.buildTypeId.Length) Pieces.buildTypeId[a] = -1; // not used
        return a;
    }

    private static int DefineSynthAbility_Capture(string typeName, ref int nextA, int botSurcharge)
    {
        int a = nextA++; // use the moving cursor
        string name = $"CaptureVP@{typeName}";
        Pieces.DefineAbility(a, name, Pieces.AbilityKind.CaptureVP, Pieces.TargetKind.Cell);
        if (a < Pieces.rangeMin.Length) Pieces.rangeMin[a] = 0;
        if (a < Pieces.rangeMax.Length) Pieces.rangeMax[a] = 0;
        if (a < Pieces.areaRadius.Length) Pieces.areaRadius[a] = 0;
        if (a < Pieces.damage.Length) Pieces.damage[a] = 0;
        if (a < Pieces.customParam.Length) Pieces.customParam[a] = 0;
        if (a < Pieces.baseSurcharge.Length) Pieces.baseSurcharge[a] = 0;
        if (a < Pieces.botThinkSurcharge.Length) Pieces.botThinkSurcharge[a] = botSurcharge;
        if (a < Pieces.buildTypeId.Length) Pieces.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_CoreDamage(string typeName, ref int nextA, int botSurcharge)
    {
        int a = nextA++; // use the moving cursor
        string name = $"CoreDamage@{typeName}";
        Pieces.DefineAbility(a, name, Pieces.AbilityKind.CoreDamage, Pieces.TargetKind.Cell);
        if (a < Pieces.rangeMin.Length) Pieces.rangeMin[a] = 0;
        if (a < Pieces.rangeMax.Length) Pieces.rangeMax[a] = 0;
        if (a < Pieces.areaRadius.Length) Pieces.areaRadius[a] = 0;
        if (a < Pieces.damage.Length) Pieces.damage[a] = 0; // effect is contextual at kernel
        if (a < Pieces.customParam.Length) Pieces.customParam[a] = 0;
        if (a < Pieces.baseSurcharge.Length) Pieces.baseSurcharge[a] = 0;
        if (a < Pieces.botThinkSurcharge.Length) Pieces.botThinkSurcharge[a] = botSurcharge;
        if (a < Pieces.buildTypeId.Length) Pieces.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_GroupBuild(string typeName, ref int nextA, int botSurcharge)
    {
        int a = nextA++;
        string name = $"GroupBuild@{typeName}";
        Pieces.DefineAbility(a, name, Pieces.AbilityKind.GroupBuild, Pieces.TargetKind.Cell);
        if (a < Pieces.rangeMin.Length) Pieces.rangeMin[a] = 0;
        if (a < Pieces.rangeMax.Length) Pieces.rangeMax[a] = 0;
        if (a < Pieces.damage.Length) Pieces.damage[a] = 0;
        if (a < Pieces.botThinkSurcharge.Length) Pieces.botThinkSurcharge[a] = botSurcharge;
        if (a < Pieces.buildTypeId.Length) Pieces.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_Push(
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
        Pieces.DefineAbility(a, name, Pieces.AbilityKind.Push, Pieces.TargetKind.Piece);
        if (a < Pieces.rangeMin.Length) Pieces.rangeMin[a] = 1;
        if (a < Pieces.rangeMax.Length) Pieces.rangeMax[a] = rangeMax;
        if (a < Pieces.damage.Length) Pieces.damage[a] = damage;
        if (a < Pieces.push_TargetsBuildings.Length) Pieces.push_TargetsBuildings[a] = targetsBuildings;
        if (a < Pieces.push_TargetsSoldiers.Length) Pieces.push_TargetsSoldiers[a] = targetsSoldiers;
        if (a < Pieces.push_rangeMax.Length) Pieces.push_rangeMax[a] = rangeMax;
        if (a < Pieces.push_PushAmount.Length) Pieces.push_PushAmount[a] = pushAmount;
        if (a < Pieces.push_pull.Length) Pieces.push_pull[a] = pull;
        if (a < Pieces.push_FriendlyFire.Length) Pieces.push_FriendlyFire[a] = friendlyFire;
        if (a < Pieces.push_damage.Length) Pieces.push_damage[a] = damage;
        if (a < Pieces.botThinkSurcharge.Length) Pieces.botThinkSurcharge[a] = botSurcharge;
        if (a < Pieces.buildTypeId.Length) Pieces.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_Upgrade(string typeName, ref int nextA, int botSurcharge)
    {
        int a = nextA++;
        string name = $"Upgrade@{typeName}";
        Pieces.DefineAbility(a, name, Pieces.AbilityKind.Upgrade, Pieces.TargetKind.Cell);
        if (a < Pieces.rangeMin.Length) Pieces.rangeMin[a] = 0;
        if (a < Pieces.rangeMax.Length) Pieces.rangeMax[a] = 0;
        if (a < Pieces.damage.Length) Pieces.damage[a] = 0;
        if (a < Pieces.botThinkSurcharge.Length) Pieces.botThinkSurcharge[a] = botSurcharge;
        if (a < Pieces.buildTypeId.Length) Pieces.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_Launcher(
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
        Pieces.DefineAbility(a, name, Pieces.AbilityKind.Launcher, Pieces.TargetKind.Piece);
        if (a < Pieces.rangeMin.Length) Pieces.rangeMin[a] = 1;
        if (a < Pieces.rangeMax.Length) Pieces.rangeMax[a] = Math.Max(inputRange, outputRange);
        if (a < Pieces.launcher_inputRange.Length) Pieces.launcher_inputRange[a] = inputRange;
        if (a < Pieces.launcher_outputRange.Length) Pieces.launcher_outputRange[a] = outputRange;
        if (a < Pieces.launcher_friendlyFire.Length) Pieces.launcher_friendlyFire[a] = friendlyFire;
        if (a < Pieces.launcher_enemyFire.Length) Pieces.launcher_enemyFire[a] = enemyFire;
        if (a < Pieces.botThinkSurcharge.Length) Pieces.botThinkSurcharge[a] = botSurcharge;
        if (a < Pieces.buildTypeId.Length) Pieces.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_Spawner(
        string typeName,
        int pieceAmount,
        int range,
        bool oncePerTurn,
        ref int nextA,
        int botSurcharge)
    {
        int a = nextA++;
        string name = $"Spawner@{typeName}";
        Pieces.DefineAbility(a, name, Pieces.AbilityKind.Spawner, Pieces.TargetKind.Cell);
        if (a < Pieces.rangeMin.Length) Pieces.rangeMin[a] = 1;
        if (a < Pieces.rangeMax.Length) Pieces.rangeMax[a] = range;
        if (a < Pieces.spawn_pieceAmount.Length) Pieces.spawn_pieceAmount[a] = pieceAmount;
        if (a < Pieces.spawn_range.Length) Pieces.spawn_range[a] = range;
        if (a < Pieces.spawn_onlyOncePerTurn.Length) Pieces.spawn_onlyOncePerTurn[a] = oncePerTurn;
        if (a < Pieces.botThinkSurcharge.Length) Pieces.botThinkSurcharge[a] = botSurcharge;
        if (a < Pieces.buildTypeId.Length) Pieces.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_Factory(
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
        Pieces.DefineAbility(a, name, Pieces.AbilityKind.Factory, Pieces.TargetKind.None);
        if (a < Pieces.factory_amount.Length) Pieces.factory_amount[a] = amount;
        if (a < Pieces.factory_roundMultiplier.Length) Pieces.factory_roundMultiplier[a] = roundMul;
        if (a < Pieces.factory_group.Length) Pieces.factory_group[a] = group;
        if (a < Pieces.factory_groupAmount.Length) Pieces.factory_groupAmount[a] = Math.Max(1, groupAmount);
        if (a < Pieces.botThinkSurcharge.Length) Pieces.botThinkSurcharge[a] = botSurcharge;
        if (a < Pieces.buildTypeId.Length) Pieces.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_Eat(
        string typeName,
        int amount,
        ref int nextA)
    {
        int a = nextA++;
        string name = $"Eat@{typeName}";
        Pieces.DefineAbility(a, name, Pieces.AbilityKind.Eat, Pieces.TargetKind.None);
        if (a < Pieces.rangeMin.Length) Pieces.rangeMin[a] = 0;
        if (a < Pieces.rangeMax.Length) Pieces.rangeMax[a] = 0;
        if (a < Pieces.eat_enabled.Length) Pieces.eat_enabled[a] = true;
        if (a < Pieces.eat_amount.Length) Pieces.eat_amount[a] = amount;
        if (a < Pieces.botThinkSurcharge.Length) Pieces.botThinkSurcharge[a] = 0;
        if (a < Pieces.buildTypeId.Length) Pieces.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_Sanctuary(
        string typeName,
        int rangeMax,
        ref int nextA)
    {
        int a = nextA++;
        string name = $"Sanctuary@{typeName}";
        Pieces.DefineAbility(a, name, Pieces.AbilityKind.Sanctuary, Pieces.TargetKind.None);
        if (a < Pieces.rangeMin.Length) Pieces.rangeMin[a] = 0;
        if (a < Pieces.rangeMax.Length) Pieces.rangeMax[a] = Math.Max(0, rangeMax);
        if (a < Pieces.areaRadius.Length) Pieces.areaRadius[a] = 0;
        if (a < Pieces.damage.Length) Pieces.damage[a] = 0;
        if (a < Pieces.customParam.Length) Pieces.customParam[a] = 0;
        if (a < Pieces.sanctuary_enabled.Length) Pieces.sanctuary_enabled[a] = true;
        if (a < Pieces.Sanctuary_range.Length) Pieces.Sanctuary_range[a] = Math.Max(0, rangeMax);
        if (a < Pieces.botThinkSurcharge.Length) Pieces.botThinkSurcharge[a] = 0;
        if (a < Pieces.buildTypeId.Length) Pieces.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_SacrificeFactory(
        string typeName,
        int rangeMin,
        int rangeMax,
        int amount,
        ref int nextA,
        int botSurcharge)
    {
        int a = nextA++;
        string name = $"SacrificeFactory@{typeName}";
        Pieces.DefineAbility(a, name, Pieces.AbilityKind.SacrificeFactory, Pieces.TargetKind.Piece);
        if (a < Pieces.rangeMin.Length) Pieces.rangeMin[a] = Math.Max(0, rangeMin);
        if (a < Pieces.rangeMax.Length) Pieces.rangeMax[a] = Math.Max(rangeMin, rangeMax);
        if (a < Pieces.sacrificeFactory_amount.Length) Pieces.sacrificeFactory_amount[a] = amount;
        if (a < Pieces.botThinkSurcharge.Length) Pieces.botThinkSurcharge[a] = botSurcharge;
        if (a < Pieces.buildTypeId.Length) Pieces.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_ConversionFactory(
        string typeName,
        bool toCoreHealth,
        bool toVP,
        int amount,
        ref int nextA,
        int botSurcharge)
    {
        int a = nextA++;
        string name = $"ConversionFactory@{typeName}";
        Pieces.DefineAbility(a, name, Pieces.AbilityKind.ConversionFactory, Pieces.TargetKind.None);
        if (a < Pieces.rangeMin.Length) Pieces.rangeMin[a] = 0;
        if (a < Pieces.rangeMax.Length) Pieces.rangeMax[a] = 0;
        if (a < Pieces.conversionFactory_coreHealth.Length) Pieces.conversionFactory_coreHealth[a] = toCoreHealth;
        if (a < Pieces.conversionFactory_vp.Length) Pieces.conversionFactory_vp[a] = toVP;
        if (a < Pieces.conversionFactory_amount.Length) Pieces.conversionFactory_amount[a] = amount;
        if (a < Pieces.botThinkSurcharge.Length) Pieces.botThinkSurcharge[a] = botSurcharge;
        if (a < Pieces.conversionFactory_botSurcharge.Length) Pieces.conversionFactory_botSurcharge[a] = botSurcharge;
        if (a < Pieces.buildTypeId.Length) Pieces.buildTypeId[a] = -1;
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
