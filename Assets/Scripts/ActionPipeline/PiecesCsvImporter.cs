using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;


public static class PiecesCsvImporter
{
    public static void Import(string pathToPiecesCsv)
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


        Allocate(typeCount);

        var H = BuildHeaderIndex(headers);

        for (int typeId = 0; typeId < typeCount; typeId++)
        {
            var cols = rows[typeId];


            #region UI
            Piece.name[typeId] = Get(cols, H, "name", required: true);
            Piece.factionName[typeId] = Get(cols, H, "factionName", defaultValue: "");
            Piece.spritePath[typeId] = Get(cols, H, "spritePath", defaultValue: "");

            #endregion
            #region General Fields
            Piece.isBuilding[typeId] = GetBool(cols, H, "isBuilding", defaultValue: false);
            Piece.isBuildable[typeId] = GetBool(cols, H, "isbuildable", defaultValue: true);
            Piece.BuildCost[typeId] = GetInt(cols, H, "BuildCost", defaultValue: 0);
            Piece.maxHP[typeId] = (short)ClampToShort(GetInt(cols, H, "maxHP", defaultValue: 1));

            #endregion
            #region Digits
            Piece.requiredDigit[typeId] = GetInt(cols, H, "requiredDigit", defaultValue: -1);
            Piece.digitItGives[typeId] = GetInt(cols, H, "digitItGives", defaultValue: -1);

            #endregion
            #region Connectors
            Piece.connectors_enabled[typeId] = GetBool(cols, H, "connectors_enabled", defaultValue: false);
            Piece.connector_needsCapital[typeId] = GetBool(cols, H, "connector_needsCapital", defaultValue: false);
            Piece.connector_isCapital[typeId] = GetBool(cols, H, "connector_isCapital", defaultValue: false);
            Piece.connector_capitalHealth[typeId] = GetInt(cols, H, "connector_capitalHealth", defaultValue: 0);
            Piece.connector_allowedMasks[typeId] = ulong.MaxValue; //ParseConnectorMask(Get(cols, H, "connector_allowedMasks", defaultValue: string.Empty));

            #endregion
            #region Group Build
            Piece.groupBuild_enabled[typeId] = GetBool(cols, H, "groupBuild_enabled", defaultValue: false);
            Piece.groupBuild_target[typeId] = GetInt(cols, H, "groupBuild_target", defaultValue: -1);
            Piece.groupBuild_requireNumber[typeId] = GetInt(cols, H, "groupBuild_requireNumber", defaultValue: 0);
            Piece.groupBuild_deletion[typeId] = GetBool(cols, H, "groupBuild_deletion", defaultValue: false);
            Piece.groupBuild_botSurcharge[typeId] = GetInt(cols, H, "groupBuild_botSurcharge", defaultValue: 0);

            #endregion
            #region Upgrade
            Piece.upgrade_enabled[typeId] = GetBool(cols, H, "upgrade_enabled", defaultValue: false);
            Piece.upgrade_target[typeId] = GetInt(cols, H, "upgrade_target", defaultValue: -1);
            Piece.upgrade_killsNeeded[typeId] = GetInt(cols, H, "upgrade_killsNeeded", defaultValue: 0);
            Piece.upgrade_isGoalKills[typeId] = GetBool(cols, H, "upgrade_isGoalKills", defaultValue: false);
            Piece.upgrade_botSurcharge[typeId] = GetInt(cols, H, "upgrade_botSurcharge", defaultValue: 0);

            #endregion
            #region launcher
            Piece.launcher_enabled[typeId] = GetBool(cols, H, "launcher_enabled", defaultValue: false);
            Piece.launcher_inputRange[typeId] = GetInt(cols, H, "launcher_inputRange", defaultValue: 0);
            Piece.launcher_outputRange[typeId] = GetInt(cols, H, "launcher_outputRange", defaultValue: 0);
            Piece.launcher_isfriendlyFire[typeId] = GetBool(cols, H, "launcher_isfriendlyFire", defaultValue: false);
            Piece.launcher_isEnemyFire[typeId] = GetBool(cols, H, "launcher_isEnemyFire", defaultValue: false);
            Piece.launcher_botSurcharge[typeId] = GetInt(cols, H, "launcher_botSurcharge", defaultValue: 0);


            #endregion
            #region Push
            Piece.push_enabled[typeId] = GetBool(cols, H, "push_enabled", defaultValue: false);
            Piece.push_IsTargetsBuildings[typeId] = GetBool(cols, H, "push_IsTargetsBuildings", defaultValue: false);
            Piece.push_isTargetsSoldiers[typeId] = GetBool(cols, H, "push_isTargetsSoldiers", defaultValue: false);
            Piece.push_rangeMax[typeId] = GetInt(cols, H, "push_rangeMax", defaultValue: 0);
            Piece.push_pushAmount[typeId] = GetInt(cols, H, "push_pushAmount", defaultValue: 0);
            Piece.push_isPull[typeId] = GetBool(cols, H, "push_isPull", defaultValue: false);
            Piece.push_isFriendlyFire[typeId] = GetBool(cols, H, "push_isFriendlyFire", defaultValue: false);
            Piece.push_damage[typeId] = GetInt(cols, H, "push_damage", defaultValue: 0);

            #endregion
            #region spawn
            Piece.spawn_enabled[typeId] = GetBool(cols, H, "spawn_enabled", defaultValue: false);
            Piece.spawn_pieceAmount[typeId] = GetInt(cols, H, "spawn_pieceAmount", defaultValue: 0);
            Piece.spawn_targetType[typeId] = GetInt(cols, H, "spawn_targetType", defaultValue: -1);
            Piece.spawn_range[typeId] = GetInt(cols, H, "spawn_range", defaultValue: 0);
            Piece.spawn_isOnlyOncePerTurn[typeId] = GetBool(cols, H, "spawn_isOnlyOncePerTurn", defaultValue: false);
            Piece.spawn_botSurcharge[typeId] = GetInt(cols, H, "spawn_botSurcharge", defaultValue: 0);

            #endregion
            #region multiCreate
            // Piece.multiCreate_enabledByType[typeId] = GetBool(cols, H, "multiCreate_enabledByType", defaultValue: false);
            // Piece.multiCreate_amountByType[typeId] = GetInt(cols, H, "multiCreate_amountByType", defaultValue: 0);
            // Piece.multiCreate_isBoardering[typeId] = GetBool(cols, H, "multiCreate_isBoardering", defaultValue: false);

            #endregion
            #region factory
            Piece.factory_enabled[typeId] = GetBool(cols, H, "factory_enabled", defaultValue: false);
            Piece.factory_amount[typeId] = GetInt(cols, H, "factory_amount", defaultValue: 0);
            Piece.factory_isRoundMultiplier[typeId] = GetBool(cols, H, "factory_isRoundMultiplier", defaultValue: false);
            Piece.factory_isGroup[typeId] = GetBool(cols, H, "factory_isGroup", defaultValue: false);
            Piece.factory_groupAmount[typeId] = GetInt(cols, H, "factory_groupAmount", defaultValue: 0);

            Piece.factory_isInstantPayOut[typeId] = GetBool(cols, H, "factory_isInstantPayOut", defaultValue: false);
            Piece.factory_instantPayOutAmount[typeId] = GetInt(cols, H, "factory_instantPayOutAmount", defaultValue: 0);
            Piece.factory_isKillPenalty[typeId] = GetBool(cols, H, "factory_isKillPenalty", defaultValue: false);
            Piece.factory_killsNeeded[typeId] = GetInt(cols, H, "factory_killsNeeded", defaultValue: 0);
            Piece.factory_killsPunishment[typeId] = GetInt(cols, H, "factory_killsPunishment", defaultValue: 0);
            
            #endregion
            #region sanctuary
            Piece.sanctuary_enabled[typeId] = GetBool(cols, H, "sanctuary_enabled", defaultValue: false);
            Piece.sanctuary_range[typeId] = GetInt(cols, H, "sanctuary_range", defaultValue: 0);

            #endregion
            #region conversion Factory
            // may add this to the Factory region
            Piece.conversionFactory_enabled[typeId] = GetBool(cols, H, "conversionFactory_enabled", defaultValue: false);
            Piece.conversionFactory_isCoreHealth[typeId] = GetBool(cols, H, "conversionFactory_isCoreHealth", defaultValue: false);
            Piece.conversionFactory_isVp[typeId] = GetBool(cols, H, "conversionFactory_isVp", defaultValue: false);
            Piece.conversionFactory_amount[typeId] = GetInt(cols, H, "conversionFactory_amount", defaultValue: 0);
            Piece.conversionFactory_botSurcharge[typeId] = GetInt(cols, H, "conversionFactory_botSurcharge", defaultValue: 0);

            #endregion
            #region eat

            Piece.eat_enabled[typeId] = GetBool(cols, H, "eat_enabled", defaultValue: false);
            Piece.eat_amount[typeId] = GetInt(cols, H, "eat_amount", defaultValue: 0);
            #endregion
            #region shoot
            // this needs to re done- used to be Generic params
            //this is all addition
            Piece.shoot_enabled[typeId] = GetBool(cols, H, "shoot_enabled", defaultValue: false);
            Piece.shoot_rangeMin[typeId] = GetInt(cols, H, "shoot_rangeMin", defaultValue: 0);
            Piece.shoot_rangeMax[typeId] = GetInt(cols, H, "shoot_rangeMax", defaultValue: 0);
            Piece.shoot_damage[typeId] = GetInt(cols, H, "shoot_damage", defaultValue: 0);
            Piece.shoot_botSurcharge[typeId] = GetInt(cols, H, "shoot_botSurcharge", defaultValue: 0);

            #endregion
            #region Move
            Piece.move_enabled[typeId] = GetBool(cols, H, "move_enabled", defaultValue: false);
            Piece.move_rangeMin[typeId] = GetInt(cols, H, "move_rangeMin", defaultValue: 0);
            Piece.move_rangeMax[typeId] = GetInt(cols, H, "move_rangeMax", defaultValue: 0);
            Piece.move_damage[typeId] = GetInt(cols, H, "move_damage", defaultValue: 0);
            Piece.move_botSurcharge[typeId] = GetInt(cols, H, "move_botSurcharge", defaultValue: 0);

            #endregion
            #region sacrifice Factory
            Piece.sacrificeFactory_enabled[typeId] = GetBool(cols, H, "sacrificeFactory_enabled", defaultValue: false);
            Piece.sacrificeFactory_amount[typeId] = GetInt(cols, H, "sacrificeFactory_amount", defaultValue: 0);
            Piece.sacrificeFactory_rangeMin[typeId] = GetInt(cols, H, "sacrificeFactory_rangeMin", defaultValue: 0);
            Piece.sacrificeFactory_rangeMax[typeId] = GetInt(cols, H, "sacrificeFactory_rangeMax", defaultValue: 0);
            Piece.sacrificeFactory_botSurcharge[typeId] = GetInt(cols, H, "sacrificeFactory_botSurcharge", defaultValue: 0);

            #endregion
            #region capture

            Piece.captureVP_enabled[typeId] = GetBool(cols, H, "captureVP_enabled", defaultValue: false);
            Piece.captureVP_botSurcharge[typeId] = GetInt(cols, H, "captureVP_botSurcharge", defaultValue: 0);

            Piece.coreDamage_enabled[typeId] = GetBool(cols, H, "coreDamage_enabled", defaultValue: false);
            Piece.coreDamage_damage[typeId] = GetInt(cols, H, "coreDamage_damage", defaultValue: 0);
            Piece.coreDamage_botSurcharge[typeId] = GetInt(cols, H, "coreDamage_botSurcharge", defaultValue: 0);

            // #endregion
            // #region sacrificeCost
            // Piece.sacrificeCost_enabled[typeId] = GetBool(cols, H, "sacrificeCost_enabled", defaultValue: false);
            // Piece.sacrificeCost_isNeedsSpecificPiece[typeId] = GetBool(cols, H, "sacrificeCost_isNeedsSpecificPiece", defaultValue: false);
            // Piece.sacrificeCost_specificPiece[typeId] = GetInt(cols, H, "sacrificeCost_specificPiece", defaultValue: -1);
            // Piece.sacrificeCost_howManyItNeeds[typeId] = GetInt(cols, H, "sacrificeCost_howManyItNeeds", defaultValue: -1);
            #endregion
            #region Feeding Ground
            Piece.feedingGround_enabled[typeId] = GetBool(cols, H, "feedingGround_enabled", defaultValue: false);
            Piece.feedingGround_payOut[typeId] = GetInt(cols, H, "feedingGround_payOut", defaultValue: -1);
            Piece.feedingGround_Range[typeId] = GetInt(cols, H, "feedingGround_Range", defaultValue: -1);
            #endregion
            #region Explosive
            Piece.explosive_enabled[typeId] = GetBool(cols, H, "explosive_enabled", defaultValue: false);
            Piece.explosive_isFriendlyFire[typeId] = GetBool(cols, H, "explosive_isFriendlyFire", defaultValue: false);
            Piece.explosive_isKillItself[typeId] = GetBool(cols, H, "explosive_isKillItself", defaultValue: false);
            Piece.explosive_damage[typeId] = GetInt(cols, H, "explosive_damage", defaultValue: 0);
            Piece.explosive_range[typeId] = GetInt(cols, H, "explosive_range", defaultValue: 0);
            #endregion
            // #region build
            // Piece.pieceBuild_enabled[typeId] = GetBool(cols, H, "pieceBuild_enabled", defaultValue: false);            
            // Piece.pieceBuild_range[typeId] = GetInt(cols, H, "pieceBuild_range", defaultValue: 0);
            // Piece.pieceBuild_targetIds[typeId] = GetIntArray(cols, H, "pieceBuild_targetIds", defaultValue: 0);
            // #endregion
            #region sniper
            Piece.sniper_enabled[typeId] = GetBool(cols, H, "sniper_enabled", defaultValue: false);            
            Piece.sniper_minRange[typeId] = GetInt(cols, H, "sniper_minRange", defaultValue: 0);
            Piece.sniper_damage[typeId] = GetInt(cols, H, "sniper_damage", defaultValue: 0);
            Piece.sniper_maxRange[typeId] = GetInt(cols, H, "sniper_maxRange", defaultValue: 0);
            Piece.sniper_isonlySoldiers[typeId] = GetBool(cols, H, "sniper_isonlySoldiers", defaultValue: false);            
            Piece.sniper_isLineOfSight[typeId] = GetBool(cols, H, "sniper_isLineOfSight", defaultValue: false);            
            Piece.sniper_isFriendlyFire[typeId] = GetBool(cols, H, "sniper_isFriendlyFire", defaultValue: false);            
            Piece.sniper_lineLength[typeId] = GetInt(cols, H, "sniper_lineLength", defaultValue: 0);
            #endregion
            #region zombie
            Piece.zombie_enabled[typeId] = GetBool(cols, H, "zombie_enabled", defaultValue: false);            
            #endregion
            #region necroSpawn
            Piece.necroSpawn_enabled[typeId] = GetBool(cols, H, "necroSpawn_enabled", defaultValue: false);            
            Piece.necroSpawn_range[typeId] = GetInt(cols, H, "necroSpawn_range", defaultValue: 0);
            Piece.necroSpawn_botSurcharge[typeId] = GetInt(cols, H, "necroSpawn_botSurcharge", defaultValue: 0);
            #endregion 
            #region WorkYard
            Piece.workYard_enabled[typeId] = GetBool(cols, H, "workYard_enabled", defaultValue: false);            
            Piece.workYard_range[typeId] = GetInt(cols, H, "workYard_range", defaultValue: 0);
            Piece.workYard_botSurcharge[typeId] = GetInt(cols, H, "workYard_botSurcharge", defaultValue: 0);
            Piece.workYard_excludeBuildings[typeId] = GetBool(cols, H, "workYard_excludeBuildings", defaultValue: false);            
            #endregion 
        }
    }

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

    private static Dictionary<string, List<int>> BuildHeaderIndex(string[] headers)
    {
        var map = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            string h = headers[i]?.Trim() ?? string.Empty;
            if (h.Length == 0) continue;
            if (!map.TryGetValue(h, out var list))
            {
                list = new List<int>();
                map[h] = list;
            }
            list.Add(i);
        }
        return map;
    }

    private static string Get(string[] cols, Dictionary<string, List<int>> H, string key, bool required = false, string defaultValue = "")
    {
        if (H.TryGetValue(key, out var idxs) && idxs.Count > 0)
        {
            int idx = idxs[0];
            if (idx >= 0 && idx < cols.Length)
            return cols[idx];
        }
        if (required)
            throw new InvalidDataException($"Missing required column '{key}'");
        return defaultValue;
    }

    private static int GetInt(string[] cols, Dictionary<string, List<int>> H, string key, int defaultValue)
    {
        string s = Get(cols, H, key, required: false, defaultValue: string.Empty);
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
        return defaultValue;
    }

    private static bool GetBool(string[] cols, Dictionary<string, List<int>> H, string key, bool defaultValue)
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

    private static int[] GetIntArray(string[] cols, Dictionary<string, List<int>> H, string key, int defaultValue)
    {
        var values = new List<int>();
        if (H.TryGetValue(key, out var idxs))
        {
            for (int i = 0; i < idxs.Count; i++)
            {
                int idx = idxs[i];
                if (idx < 0 || idx >= cols.Length) continue;
                string s = cols[idx];
                if (string.IsNullOrWhiteSpace(s)) continue;
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                {
                    values.Add(v);
                }
            }
        }
        if (values.Count == 0) values.Add(defaultValue);
        return values.ToArray();
    }

    private static int ClampToShort(int v) => Math.Max(short.MinValue, Math.Min(short.MaxValue, v));

    public static void Allocate(int count)
    {
        Piece.typeCount = count;

        #region UI
        Piece.name = new string[count];
        Piece.factionName = new string[count];
        Piece.spritePath = new string[count];

        #endregion
        #region General Fields
        Piece.isBuilding = new bool[count];
        Piece.isBuildable = new bool[count];
        Piece.BuildCost = new int[count];
        Piece.maxHP = new short[count];

        #endregion
        #region Digits
        Piece.requiredDigit = new int[count];
        Piece.digitItGives = new int[count];


        #endregion
        #region Connectors
        Piece.connectors_enabled = new bool[count];
        Piece.connector_needsCapital = new bool[count];
        Piece.connector_isCapital = new bool[count];
        Piece.connector_capitalHealth = new int[count];
        Piece.connector_allowedMasks = new ulong[count];

        #endregion
        #region Group Build
        Piece.groupBuild_enabled = new bool[count];
        Piece.groupBuild_target = new int[count];
        Piece.groupBuild_requireNumber = new int[count];
        Piece.groupBuild_deletion = new bool[count];
        Piece.groupBuild_botSurcharge = new int[count];

        #endregion
        #region Upgrade
        Piece.upgrade_enabled = new bool[count];
        Piece.upgrade_target = new int[count];
        Piece.upgrade_botSurcharge = new int[count];
        Piece.upgrade_killsNeeded = new int[count];
        Piece.upgrade_isGoalKills = new bool[count];        

        #endregion
        #region launcher
        Piece.launcher_enabled = new bool[count];
        Piece.launcher_inputRange = new int[count];
        Piece.launcher_outputRange = new int[count];
        Piece.launcher_isfriendlyFire = new bool[count];
        Piece.launcher_isEnemyFire = new bool[count];
        Piece.launcher_botSurcharge = new int[count];

        #endregion
        #region Push
        Piece.push_enabled = new bool[count];
        Piece.push_IsTargetsBuildings = new bool[count];
        Piece.push_isTargetsSoldiers = new bool[count];
        Piece.push_rangeMax = new int[count];
        Piece.push_pushAmount = new int[count];
        Piece.push_isPull = new bool[count];
        Piece.push_isFriendlyFire = new bool[count];
        Piece.push_damage = new int[count];

        #endregion
        #region spawn
        Piece.spawn_enabled = new bool[count];
        Piece.spawn_pieceAmount = new int[count];
        Piece.spawn_targetType = new int[count];
        Piece.spawn_range = new int[count];
        Piece.spawn_isOnlyOncePerTurn = new bool[count];
        Piece.spawn_botSurcharge = new int[count];

        #endregion
        #region multiCreate
        // Piece.multiCreate_enabledByType = new bool[count];
        // Piece.multiCreate_amountByType = new int[count];
        // Piece.multiCreate_isBoardering = new bool[count];

        #endregion
        #region factory
        Piece.factory_enabled = new bool[count];
        Piece.factory_amount = new int[count];
        Piece.factory_isRoundMultiplier = new bool[count];
        Piece.factory_isGroup = new bool[count];
        Piece.factory_groupAmount = new int[count];

        Piece.factory_isInstantPayOut = new bool[count];
        Piece.factory_instantPayOutAmount = new int[count];
        Piece.factory_isKillPenalty = new bool[count];
        Piece.factory_killsNeeded = new int[count];
        Piece.factory_killsPunishment = new int[count];


        #endregion
        #region sanctuary
        Piece.sanctuary_enabled = new bool[count];
        Piece.sanctuary_range = new int[count];

        #endregion
        #region conversion Factory
        Piece.conversionFactory_enabled = new bool[count];
        Piece.conversionFactory_isCoreHealth = new bool[count];
        Piece.conversionFactory_isVp = new bool[count];
        Piece.conversionFactory_amount = new int[count];
        Piece.conversionFactory_botSurcharge = new int[count];

        #endregion
        #region eat
        Piece.eat_enabled = new bool[count];
        Piece.eat_amount = new int[count];

        #endregion
        #region shoot
        Piece.shoot_enabled = new bool[count];
        Piece.shoot_rangeMin = new int[count];
        Piece.shoot_rangeMax = new int[count];
        Piece.shoot_damage = new int[count];
        Piece.shoot_botSurcharge = new int[count];

        #endregion
        #region Move
        Piece.move_enabled = new bool[count];
        Piece.move_rangeMin = new int[count];
        Piece.move_rangeMax = new int[count];
        Piece.move_damage = new int[count];
        Piece.move_botSurcharge = new int[count];

        #endregion
        #region sacrifice Factory
        Piece.sacrificeFactory_enabled = new bool[count];
        Piece.sacrificeFactory_amount = new int[count];
        Piece.sacrificeFactory_rangeMin = new int[count];
        Piece.sacrificeFactory_rangeMax = new int[count];
        Piece.sacrificeFactory_botSurcharge = new int[count];

        #endregion
        #region capture
        Piece.captureVP_enabled = new bool[count];
        Piece.captureVP_botSurcharge = new int[count];

        Piece.coreDamage_enabled = new bool[count];
        Piece.coreDamage_damage = new int[count];
        Piece.coreDamage_botSurcharge = new int[count];

        // #endregion
        // #region sacrificeCost
        // Piece.sacrificeCost_enabled = new bool[count];
        // Piece.sacrificeCost_isNeedsSpecificPiece = new bool[count];
        // Piece.sacrificeCost_specificPiece = new int[count];
        // Piece.sacrificeCost_howManyItNeeds = new int[count];

        #endregion
        #region Feeding Ground
        Piece.feedingGround_enabled = new bool[count];
        Piece.feedingGround_payOut = new int[count];
        Piece.feedingGround_Range = new int[count];
        #endregion
        #region explosive
        Piece.explosive_enabled = new bool[count];
        Piece.explosive_isFriendlyFire = new bool[count];
        Piece.explosive_isKillItself = new bool[count];
        Piece.explosive_damage = new int[count];
        Piece.explosive_range = new int[count];
        #endregion
        // #region pieceBuild
        // Piece.pieceBuild_enabled = new bool[count];        
        // Piece.pieceBuild_range = new int[count];
        // Piece.pieceBuild_targetIds = new int[count][];
        // #endregion
        #region sniper
        Piece.sniper_enabled = new bool[count];
        Piece.sniper_minRange = new int[count];
        Piece.sniper_damage = new int[count];
        Piece.sniper_maxRange = new int[count];
        Piece.sniper_isonlySoldiers = new bool[count];
        Piece.sniper_isLineOfSight = new bool[count];
        Piece.sniper_isFriendlyFire = new bool[count];
        Piece.sniper_lineLength = new int[count];
        #endregion
        #region Zombie
        Piece.zombie_enabled = new bool[count];
        #endregion
        #region necroSpawn
        Piece.necroSpawn_enabled = new bool[count];
        Piece.necroSpawn_range = new int[count];
        Piece.necroSpawn_botSurcharge = new int[count];
        #endregion
        #region necroSpawn
        Piece.workYard_enabled = new bool[count];
        Piece.workYard_range = new int[count];
        Piece.workYard_botSurcharge = new int[count];
        Piece.workYard_excludeBuildings = new bool[count];
        #endregion
    }

}
