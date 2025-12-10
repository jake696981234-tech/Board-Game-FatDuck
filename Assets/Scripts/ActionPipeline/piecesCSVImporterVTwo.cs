using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;


public static class piecesCSVImporterVTwo
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


        PieceDefinition.Allocate(typeCount);
        PieceDefinition.typeCount = typeCount;

        var H = BuildHeaderIndex(headers);

        for (int typeId = 0; typeId < typeCount; typeId++)
        {
            var cols = rows[typeId];


            #region UI
            PieceDefinition.displayNameByType[typeId] = Get(cols, H, "displayNameByType", required: true);
            PieceDefinition.factionNameByType[typeId] = Get(cols, H, "factionNameByType", defaultValue: "");
            PieceDefinition.spritePathByType[typeId] = Get(cols, H, "spritePathByType", defaultValue: "");

            #endregion
            #region General Fields
            PieceDefinition.isBuildingByType[typeId] = GetBool(cols, H, "isBuildingByType", defaultValue: false);
            PieceDefinition.buildableByType[typeId] = GetBool(cols, H, "buildableByType", defaultValue: true);
            PieceDefinition.buildCostByType[typeId] = GetInt(cols, H, "buildCostByType", defaultValue: 0);
            PieceDefinition.maxHPByType[typeId] = GetInt(cols, H, "maxHPByType", defaultValue: 0);

            #endregion
            #region Digits
            PieceDefinition.codeDigitsByType[typeId] = GetInt(cols, H, "codeDigitsByType", defaultValue: 0);
            PieceDefinition.grantsDigitByType[typeId] = GetInt(cols, H, "grantsDigitByType", defaultValue: 0);

            #endregion
            #region Connectors
            PieceDefinition.connectors_enabled[typeId] = GetBool(cols, H, "connectors_enabled", defaultValue: false);
            PieceDefinition.connectorNeedsCapital[typeId] = GetBool(cols, H, "connectorNeedsCapital", defaultValue: false);
            PieceDefinition.connectorIsCapital[typeId] = GetBool(cols, H, "connectorIsCapital", defaultValue: false);
            PieceDefinition.connectorCapitalHealth[typeId] = GetInt(cols, H, "connectorCapitalHealth", defaultValue: 0);
            PieceDefinition.connectorAllowedMasks[typeId] = GetInt(cols, H, "connectorAllowedMasks", defaultValue: 0);

            #endregion
            #region Group Build
            PieceDefinition.groupBuild_enabled[typeId] = GetBool(cols, H, "groupBuild_enabled", defaultValue: false);
            PieceDefinition.groupBuildTargetType[typeId] = GetInt(cols, H, "groupBuildTargetType", defaultValue: -1);
            PieceDefinition.groupBuildRequireNumber[typeId] = GetInt(cols, H, "groupBuildRequireNumber", defaultValue: 0);
            PieceDefinition.groupBuildDeletion[typeId] = GetBool(cols, H, "groupBuildDeletion", defaultValue: false);
            PieceDefinition.groupBuild_botSurcharge[typeId] = GetInt(cols, H, "groupBuild_botSurcharge", defaultValue: 0);

            #endregion
            #region Upgrade
            PieceDefinition.upgradeEnabled[typeId] = GetBool(cols, H, "upgradeEnabled", defaultValue: false);
            PieceDefinition.upgradeTargetType[typeId] = GetInt(cols, H, "upgradeTargetType", defaultValue: -1);
            PieceDefinition.upgrade_botSurcharge[typeId] = GetInt(cols, H, "upgrade_botSurcharge", defaultValue: 0);

            #endregion
            #region launcher
            PieceDefinition.launcher_enabled[typeId] = GetBool(cols, H, "launcher_enabled", defaultValue: false);
            PieceDefinition.launcher_inputRange[typeId] = GetInt(cols, H, "launcher_inputRange", defaultValue: 0);
            PieceDefinition.launcher_outputRange[typeId] = GetInt(cols, H, "launcher_outputRange", defaultValue: 0);
            PieceDefinition.launcher_friendlyFire[typeId] = GetBool(cols, H, "launcher_friendlyFire", defaultValue: false);
            PieceDefinition.launcher_enemyFire[typeId] = GetBool(cols, H, "launcher_enemyFire", defaultValue: false);
            PieceDefinition.launcher_botSurcharge[typeId] = GetInt(cols, H, "launcher_botSurcharge", defaultValue: 0);


            #endregion
            #region Push
            PieceDefinition.push_enabled[typeId] = GetBool(cols, H, "push_enabled", defaultValue: false);
            PieceDefinition.push_TargetsBuildings[typeId] = GetBool(cols, H, "push_TargetsBuildings", defaultValue: false);
            PieceDefinition.push_TargetsSoldiers[typeId] = GetBool(cols, H, "push_TargetsSoldiers", defaultValue: false);
            PieceDefinition.push_rangeMax[typeId] = GetInt(cols, H, "push_rangeMax", defaultValue: 0);
            PieceDefinition.push_PushAmount[typeId] = GetInt(cols, H, "push_PushAmount", defaultValue: 0);
            PieceDefinition.push_pull[typeId] = GetBool(cols, H, "push_pull", defaultValue: false);
            PieceDefinition.push_FriendlyFire[typeId] = GetBool(cols, H, "push_FriendlyFire", defaultValue: false);
            PieceDefinition.push_damage[typeId] = GetInt(cols, H, "push_damage", defaultValue: 0);

            #endregion
            #region spawn
            PieceDefinition.spawn_enabled[typeId] = GetBool(cols, H, "spawn_enabled", defaultValue: false);
            PieceDefinition.spawn_pieceAmount[typeId] = GetInt(cols, H, "spawn_pieceAmount", defaultValue: 0);
            PieceDefinition.spawn_targetType[typeId] = GetInt(cols, H, "spawn_targetType", defaultValue: -1);
            PieceDefinition.spawn_range[typeId] = GetInt(cols, H, "spawn_range", defaultValue: 0);
            PieceDefinition.spawn_onlyOncePerTurn[typeId] = GetBool(cols, H, "spawn_onlyOncePerTurn", defaultValue: false);
            PieceDefinition.spawn_botSurcharge[typeId] = GetInt(cols, H, "spawn_botSurcharge", defaultValue: 0);

            #endregion
            #region multiCreate
            PieceDefinition.multiCreate_enabledByType[typeId] = GetBool(cols, H, "multiCreate_enabledByType", defaultValue: false);
            PieceDefinition.multiCreate_amountByType[typeId] = GetInt(cols, H, "multiCreate_amountByType", defaultValue: 0);
            PieceDefinition.multiCreate_boarderingByType[typeId] = GetBool(cols, H, "multiCreate_boarderingByType", defaultValue: false);

            #endregion
            #region factory
            PieceDefinition.factory_enabled[typeId] = GetBool(cols, H, "factory_enabled", defaultValue: false);
            PieceDefinition.factory_amount[typeId] = GetInt(cols, H, "factory_amount", defaultValue: 0);
            PieceDefinition.factory_roundMultiplier[typeId] = GetBool(cols, H, "factory_roundMultiplier", defaultValue: false);
            PieceDefinition.factory_group[typeId] = GetBool(cols, H, "factory_group", defaultValue: false);
            PieceDefinition.factory_groupAmount[typeId] = GetInt(cols, H, "factory_groupAmount", defaultValue: 0);

            #endregion
            #region sanctuary
            PieceDefinition.sanctuary_enabled[typeId] = GetBool(cols, H, "sanctuary_enabled", defaultValue: false);
            PieceDefinition.sanctuary_range[typeId] = GetInt(cols, H, "sanctuary_range", defaultValue: 0);


            #endregion
            #region conversion Factory
            // may add this to the Factory region
            PieceDefinition.conversionFactory_enabled[typeId] = GetBool(cols, H, "conversionFactory_enabled", defaultValue: false);
            PieceDefinition.conversionFactory_coreHealth[typeId] = GetBool(cols, H, "conversionFactory_coreHealth", defaultValue: false);
            PieceDefinition.conversionFactory_vp[typeId] = GetBool(cols, H, "conversionFactory_vp", defaultValue: false);
            PieceDefinition.conversionFactory_amount[typeId] = GetInt(cols, H, "conversionFactory_amount", defaultValue: 0);
            PieceDefinition.conversionFactory_botSurcharge[typeId] = GetInt(cols, H, "conversionFactory_botSurcharge", defaultValue: 0);

            #endregion
            #region eat

            PieceDefinition.eat_enabled[typeId] = GetBool(cols, H, "eat_enabled", defaultValue: false);
            PieceDefinition.eat_amount[typeId] = GetInt(cols, H, "eat_amount", defaultValue: 0);
            #endregion
            #region shoot
            // this needs to re done- used to be Generic params
            //this is all addition
            PieceDefinition.shoot_enabled[typeId] = GetBool(cols, H, "shoot_enabled", defaultValue: false);
            PieceDefinition.shoot_rangeMin[typeId] = GetInt(cols, H, "shoot_rangeMin", defaultValue: 0);
            PieceDefinition.shoot_rangeMax[typeId] = GetInt(cols, H, "shoot_rangeMax", defaultValue: 0);
            PieceDefinition.shoot_damage[typeId] = GetInt(cols, H, "shoot_damage", defaultValue: 0);
            PieceDefinition.shoot_botSurcharge[typeId] = GetInt(cols, H, "shoot_botSurcharge", defaultValue: 0);

            #endregion
            #region Move
            PieceDefinition.move_enabled[typeId] = GetBool(cols, H, "move_enabled", defaultValue: false);
            PieceDefinition.move_rangeMin[typeId] = GetInt(cols, H, "move_rangeMin", defaultValue: 0);
            PieceDefinition.move_rangeMax[typeId] = GetInt(cols, H, "move_rangeMax", defaultValue: 0);
            PieceDefinition.move_damage[typeId] = GetInt(cols, H, "move_damage", defaultValue: 0);
            PieceDefinition.move_botSurcharge[typeId] = GetInt(cols, H, "move_botSurcharge", defaultValue: 0);

            #endregion
            #region sacrifice Factory
            PieceDefinition.sacrificeFactory_enabled[typeId] = GetBool(cols, H, "sacrificeFactory_enabled", defaultValue: false);
            PieceDefinition.sacrificeFactory_amount[typeId] = GetInt(cols, H, "sacrificeFactory_amount", defaultValue: 0);
            PieceDefinition.sacrificeFactory_rangeMin[typeId] = GetInt(cols, H, "sacrificeFactory_rangeMin", defaultValue: 0);
            PieceDefinition.sacrificeFactory_rangeMax[typeId] = GetInt(cols, H, "sacrificeFactory_rangeMax", defaultValue: 0);
            PieceDefinition.sacrificeFactory_botSurcharge[typeId] = GetInt(cols, H, "sacrificeFactory_botSurcharge", defaultValue: 0);

            #endregion
            #region capture

            PieceDefinition.captureVP_enabled[typeId] = GetBool(cols, H, "captureVP_enabled", defaultValue: false);
            PieceDefinition.captureVP_botSurcharge[typeId] = GetInt(cols, H, "captureVP_botSurcharge", defaultValue: 0);

            PieceDefinition.coreDamage_enabled[typeId] = GetBool(cols, H, "coreDamage_enabled", defaultValue: false);
            PieceDefinition.coreDamage_damage[typeId] = GetInt(cols, H, "coreDamage_damage", defaultValue: 0);
            PieceDefinition.coreDamage_botSurcharge[typeId] = GetInt(cols, H, "coreDamage_botSurcharge", defaultValue: 0);

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

    public static void Allocate(int count)
    {
        PieceDefinition.typeCount = count;

        #region UI
        PieceDefinition.displayNameByType = new string[count];
        PieceDefinition.factionNameByType = new string[count];
        PieceDefinition.spritePathByType = new string[count];

        #endregion
        #region General Fields
        PieceDefinition.isBuildingByType = new bool[count];
        PieceDefinition.buildableByType = new bool[count];
        PieceDefinition.buildCostByType = new int[count];
        PieceDefinition.maxHPByType = new int[count];

        #endregion
        #region Digits
        PieceDefinition.codeDigitsByType = new int[count];
        PieceDefinition.grantsDigitByType = new int[count];


        #endregion
        #region Connectors
        PieceDefinition.connectors_enabled = new bool[count];
        PieceDefinition.connectorNeedsCapital = new bool[count];
        PieceDefinition.connectorIsCapital = new bool[count];
        PieceDefinition.connectorCapitalHealth = new int[count];
        PieceDefinition.connectorAllowedMasks = new int[count];

        #endregion
        #region Group Build
        PieceDefinition.groupBuild_enabled = new bool[count];
        PieceDefinition.groupBuildTargetType = new int[count];
        PieceDefinition.groupBuildRequireNumber = new int[count];
        PieceDefinition.groupBuildDeletion = new bool[count];
        PieceDefinition.groupBuild_botSurcharge = new int[count];

        #endregion
        #region Upgrade
        PieceDefinition.upgradeEnabled = new bool[count];
        PieceDefinition.upgradeTargetType = new int[count];
        PieceDefinition.upgrade_botSurcharge = new int[count];

        #endregion
        #region launcher
        PieceDefinition.launcher_enabled = new bool[count];
        PieceDefinition.launcher_inputRange = new int[count];
        PieceDefinition.launcher_outputRange = new int[count];
        PieceDefinition.launcher_friendlyFire = new bool[count];
        PieceDefinition.launcher_enemyFire = new bool[count];
        PieceDefinition.launcher_botSurcharge = new int[count];

        #endregion
        #region Push
        PieceDefinition.push_enabled = new bool[count];
        PieceDefinition.push_TargetsBuildings = new bool[count];
        PieceDefinition.push_TargetsSoldiers = new bool[count];
        PieceDefinition.push_rangeMax = new int[count];
        PieceDefinition.push_PushAmount = new int[count];
        PieceDefinition.push_pull = new bool[count];
        PieceDefinition.push_FriendlyFire = new bool[count];
        PieceDefinition.push_damage = new int[count];

        #endregion
        #region spawn
        PieceDefinition.spawn_enabled = new bool[count];
        PieceDefinition.spawn_pieceAmount = new int[count];
        PieceDefinition.spawn_targetType = new int[count];
        PieceDefinition.spawn_range = new int[count];
        PieceDefinition.spawn_onlyOncePerTurn = new bool[count];
        PieceDefinition.spawn_botSurcharge = new int[count];

        #endregion
        #region multiCreate
        PieceDefinition.multiCreate_enabledByType = new bool[count];
        PieceDefinition.multiCreate_amountByType = new int[count];
        PieceDefinition.multiCreate_boarderingByType = new bool[count];

        #endregion
        #region factory
        PieceDefinition.factory_enabled = new bool[count];
        PieceDefinition.factory_amount = new int[count];
        PieceDefinition.factory_roundMultiplier = new bool[count];
        PieceDefinition.factory_group = new bool[count];
        PieceDefinition.factory_groupAmount = new int[count];

        #endregion
        #region sanctuary
        PieceDefinition.sanctuary_enabled = new bool[count];
        PieceDefinition.sanctuary_range = new int[count];

        #endregion
        #region conversion Factory
        PieceDefinition.conversionFactory_enabled = new bool[count];
        PieceDefinition.conversionFactory_coreHealth = new bool[count];
        PieceDefinition.conversionFactory_vp = new bool[count];
        PieceDefinition.conversionFactory_amount = new int[count];
        PieceDefinition.conversionFactory_botSurcharge = new int[count];

        #endregion
        #region eat
        PieceDefinition.eat_enabled = new bool[count];
        PieceDefinition.eat_amount = new int[count];

        #endregion
        #region shoot
        PieceDefinition.shoot_enabled = new bool[count];
        PieceDefinition.shoot_rangeMin = new int[count];
        PieceDefinition.shoot_rangeMax = new int[count];
        PieceDefinition.shoot_damage = new int[count];
        PieceDefinition.shoot_botSurcharge = new int[count];

        #endregion
        #region Move
        PieceDefinition.move_enabled = new bool[count];
        PieceDefinition.move_rangeMin = new int[count];
        PieceDefinition.move_rangeMax = new int[count];
        PieceDefinition.move_damage = new int[count];
        PieceDefinition.move_botSurcharge = new int[count];

        #endregion
        #region sacrifice Factory
        PieceDefinition.sacrificeFactory_enabled = new bool[count];
        PieceDefinition.sacrificeFactory_amount = new int[count];
        PieceDefinition.sacrificeFactory_rangeMin = new int[count];
        PieceDefinition.sacrificeFactory_rangeMax = new int[count];
        PieceDefinition.sacrificeFactory_botSurcharge = new int[count];

        #endregion
        #region capture
        PieceDefinition.captureVP_enabled = new bool[count];
        PieceDefinition.captureVP_botSurcharge = new int[count];

        PieceDefinition.coreDamage_enabled = new bool[count];
        PieceDefinition.coreDamage_damage = new int[count];
        PieceDefinition.coreDamage_botSurcharge = new int[count];


        // etc. for anything else you add
    }

}

