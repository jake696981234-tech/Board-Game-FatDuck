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
            PieceDefinition.name[typeId] = Get(cols, H, "name", required: true);
            PieceDefinition.factionName[typeId] = Get(cols, H, "factionName", defaultValue: "");
            PieceDefinition.spritePath[typeId] = Get(cols, H, "spritePath", defaultValue: "");

            #endregion
            #region General Fields
            PieceDefinition.isBuilding[typeId] = GetBool(cols, H, "isBuilding", defaultValue: false);
            PieceDefinition.isBuildable[typeId] = GetBool(cols, H, "isbuildable", defaultValue: true);
            PieceDefinition.BuildCost[typeId] = GetInt(cols, H, "BuildCost", defaultValue: 0);
            PieceDefinition.maxHP[typeId] = (short)ClampToShort(GetInt(cols, H, "maxHP", defaultValue: 1));

            #endregion
            #region Digits
            PieceDefinition.requiredDigit[typeId] = GetInt(cols, H, "requiredDigit", defaultValue: -1);
            PieceDefinition.digitItGives[typeId] = GetInt(cols, H, "digitItGives", defaultValue: -1);

            #endregion
            #region Connectors
            PieceDefinition.connectors_enabled[typeId] = GetBool(cols, H, "connectors_enabled", defaultValue: false);
            PieceDefinition.connector_needsCapital[typeId] = GetBool(cols, H, "connector_needsCapital", defaultValue: false);
            PieceDefinition.connector_isCapital[typeId] = GetBool(cols, H, "connector_isCapital", defaultValue: false);
            PieceDefinition.connector_capitalHealth[typeId] = GetInt(cols, H, "connector_capitalHealth", defaultValue: 0);
            PieceDefinition.connector_allowedMasks[typeId] = ulong.MaxValue; //ParseConnectorMask(Get(cols, H, "connector_allowedMasks", defaultValue: string.Empty));

            #endregion
            #region Group Build
            PieceDefinition.groupBuild_enabled[typeId] = GetBool(cols, H, "groupBuild_enabled", defaultValue: false);
            PieceDefinition.groupBuild_target[typeId] = GetInt(cols, H, "groupBuild_target", defaultValue: -1);
            PieceDefinition.groupBuild_requireNumber[typeId] = GetInt(cols, H, "groupBuild_requireNumber", defaultValue: 0);
            PieceDefinition.groupBuild_deletion[typeId] = GetBool(cols, H, "groupBuild_deletion", defaultValue: false);
            PieceDefinition.groupBuild_botSurcharge[typeId] = GetInt(cols, H, "groupBuild_botSurcharge", defaultValue: 0);

            #endregion
            #region Upgrade
            PieceDefinition.upgrade_enabled[typeId] = GetBool(cols, H, "upgrade_enabled", defaultValue: false);
            PieceDefinition.upgrade_target[typeId] = GetInt(cols, H, "upgrade_target", defaultValue: -1);
            PieceDefinition.upgrade_botSurcharge[typeId] = GetInt(cols, H, "upgrade_botSurcharge", defaultValue: 0);

            #endregion
            #region launcher
            PieceDefinition.launcher_enabled[typeId] = GetBool(cols, H, "launcher_enabled", defaultValue: false);
            PieceDefinition.launcher_inputRange[typeId] = GetInt(cols, H, "launcher_inputRange", defaultValue: 0);
            PieceDefinition.launcher_outputRange[typeId] = GetInt(cols, H, "launcher_outputRange", defaultValue: 0);
            PieceDefinition.launcher_isfriendlyFire[typeId] = GetBool(cols, H, "launcher_isfriendlyFire", defaultValue: false);
            PieceDefinition.launcher_isEnemyFire[typeId] = GetBool(cols, H, "launcher_isEnemyFire", defaultValue: false);
            PieceDefinition.launcher_botSurcharge[typeId] = GetInt(cols, H, "launcher_botSurcharge", defaultValue: 0);


            #endregion
            #region Push
            PieceDefinition.push_enabled[typeId] = GetBool(cols, H, "push_enabled", defaultValue: false);
            PieceDefinition.push_IsTargetsBuildings[typeId] = GetBool(cols, H, "push_IsTargetsBuildings", defaultValue: false);
            PieceDefinition.push_isTargetsSoldiers[typeId] = GetBool(cols, H, "push_isTargetsSoldiers", defaultValue: false);
            PieceDefinition.push_rangeMax[typeId] = GetInt(cols, H, "push_rangeMax", defaultValue: 0);
            PieceDefinition.push_pushAmount[typeId] = GetInt(cols, H, "push_pushAmount", defaultValue: 0);
            PieceDefinition.push_isPull[typeId] = GetBool(cols, H, "push_isPull", defaultValue: false);
            PieceDefinition.push_isFriendlyFire[typeId] = GetBool(cols, H, "push_isFriendlyFire", defaultValue: false);
            PieceDefinition.push_damage[typeId] = GetInt(cols, H, "push_damage", defaultValue: 0);

            #endregion
            #region spawn
            PieceDefinition.spawn_enabled[typeId] = GetBool(cols, H, "spawn_enabled", defaultValue: false);
            PieceDefinition.spawn_pieceAmount[typeId] = GetInt(cols, H, "spawn_pieceAmount", defaultValue: 0);
            PieceDefinition.spawn_targetType[typeId] = GetInt(cols, H, "spawn_targetType", defaultValue: -1);
            PieceDefinition.spawn_range[typeId] = GetInt(cols, H, "spawn_range", defaultValue: 0);
            PieceDefinition.spawn_isOnlyOncePerTurn[typeId] = GetBool(cols, H, "spawn_isOnlyOncePerTurn", defaultValue: false);
            PieceDefinition.spawn_botSurcharge[typeId] = GetInt(cols, H, "spawn_botSurcharge", defaultValue: 0);

            #endregion
            #region multiCreate
            PieceDefinition.multiCreate_enabledByType[typeId] = GetBool(cols, H, "multiCreate_enabledByType", defaultValue: false);
            PieceDefinition.multiCreate_amountByType[typeId] = GetInt(cols, H, "multiCreate_amountByType", defaultValue: 0);
            PieceDefinition.multiCreate_isBoardering[typeId] = GetBool(cols, H, "multiCreate_isBoardering", defaultValue: false);

            #endregion
            #region factory
            PieceDefinition.factory_enabled[typeId] = GetBool(cols, H, "factory_enabled", defaultValue: false);
            PieceDefinition.factory_amount[typeId] = GetInt(cols, H, "factory_amount", defaultValue: 0);
            PieceDefinition.factory_isRoundMultiplier[typeId] = GetBool(cols, H, "factory_isRoundMultiplier", defaultValue: false);
            PieceDefinition.factory_isGroup[typeId] = GetBool(cols, H, "factory_isGroup", defaultValue: false);
            PieceDefinition.factory_groupAmount[typeId] = GetInt(cols, H, "factory_groupAmount", defaultValue: 0);

            #endregion
            #region sanctuary
            PieceDefinition.sanctuary_enabled[typeId] = GetBool(cols, H, "sanctuary_enabled", defaultValue: false);
            PieceDefinition.sanctuary_range[typeId] = GetInt(cols, H, "sanctuary_range", defaultValue: 0);


            #endregion
            #region conversion Factory
            // may add this to the Factory region
            PieceDefinition.conversionFactory_enabled[typeId] = GetBool(cols, H, "conversionFactory_enabled", defaultValue: false);
            PieceDefinition.conversionFactory_isCoreHealth[typeId] = GetBool(cols, H, "conversionFactory_isCoreHealth", defaultValue: false);
            PieceDefinition.conversionFactory_isVp[typeId] = GetBool(cols, H, "conversionFactory_isVp", defaultValue: false);
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
            #region sacrificeCost
            PieceDefinition.sacrificeCost_enabled[typeId] = GetBool(cols, H, "sacrificeCost_enabled", defaultValue: false);
            PieceDefinition.sacrificeCost_isNeedsSpecificPiece[typeId] = GetBool(cols, H, "sacrificeCost_isNeedsSpecificPiece", defaultValue: false);
            PieceDefinition.sacrificeCost_specificPiece[typeId] = GetInt(cols, H, "sacrificeCost_specificPiece", defaultValue: -1);
            PieceDefinition.sacrificeCost_howManyItNeeds[typeId] = GetInt(cols, H, "sacrificeCost_howManyItNeeds", defaultValue: -1);
            #endregion

            #region Feeding Ground
            PieceDefinition.feedingGround_enabled[typeId] = GetBool(cols, H, "feedingGround_enabled", defaultValue: false);
            PieceDefinition.feedingGround_payOut[typeId] = GetInt(cols, H, "feedingGround_payOut", defaultValue: -1);
            PieceDefinition.feedingGround_Range[typeId] = GetInt(cols, H, "feedingGround_Range", defaultValue: -1);

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

    private static int ClampToShort(int v) => Math.Max(short.MinValue, Math.Min(short.MaxValue, v));

    public static void Allocate(int count)
    {
        PieceDefinition.typeCount = count;

        #region UI
        PieceDefinition.name = new string[count];
        PieceDefinition.factionName = new string[count];
        PieceDefinition.spritePath = new string[count];

        #endregion
        #region General Fields
        PieceDefinition.isBuilding = new bool[count];
        PieceDefinition.isBuildable = new bool[count];
        PieceDefinition.BuildCost = new int[count];
        PieceDefinition.maxHP = new short[count];

        #endregion
        #region Digits
        PieceDefinition.requiredDigit = new int[count];
        PieceDefinition.digitItGives = new int[count];


        #endregion
        #region Connectors
        PieceDefinition.connectors_enabled = new bool[count];
        PieceDefinition.connector_needsCapital = new bool[count];
        PieceDefinition.connector_isCapital = new bool[count];
        PieceDefinition.connector_capitalHealth = new int[count];
        PieceDefinition.connector_allowedMasks = new ulong[count];

        #endregion
        #region Group Build
        PieceDefinition.groupBuild_enabled = new bool[count];
        PieceDefinition.groupBuild_target = new int[count];
        PieceDefinition.groupBuild_requireNumber = new int[count];
        PieceDefinition.groupBuild_deletion = new bool[count];
        PieceDefinition.groupBuild_botSurcharge = new int[count];

        #endregion
        #region Upgrade
        PieceDefinition.upgrade_enabled = new bool[count];
        PieceDefinition.upgrade_target = new int[count];
        PieceDefinition.upgrade_botSurcharge = new int[count];

        #endregion
        #region launcher
        PieceDefinition.launcher_enabled = new bool[count];
        PieceDefinition.launcher_inputRange = new int[count];
        PieceDefinition.launcher_outputRange = new int[count];
        PieceDefinition.launcher_isfriendlyFire = new bool[count];
        PieceDefinition.launcher_isEnemyFire = new bool[count];
        PieceDefinition.launcher_botSurcharge = new int[count];

        #endregion
        #region Push
        PieceDefinition.push_enabled = new bool[count];
        PieceDefinition.push_IsTargetsBuildings = new bool[count];
        PieceDefinition.push_isTargetsSoldiers = new bool[count];
        PieceDefinition.push_rangeMax = new int[count];
        PieceDefinition.push_pushAmount = new int[count];
        PieceDefinition.push_isPull = new bool[count];
        PieceDefinition.push_isFriendlyFire = new bool[count];
        PieceDefinition.push_damage = new int[count];

        #endregion
        #region spawn
        PieceDefinition.spawn_enabled = new bool[count];
        PieceDefinition.spawn_pieceAmount = new int[count];
        PieceDefinition.spawn_targetType = new int[count];
        PieceDefinition.spawn_range = new int[count];
        PieceDefinition.spawn_isOnlyOncePerTurn = new bool[count];
        PieceDefinition.spawn_botSurcharge = new int[count];

        #endregion
        #region multiCreate
        PieceDefinition.multiCreate_enabledByType = new bool[count];
        PieceDefinition.multiCreate_amountByType = new int[count];
        PieceDefinition.multiCreate_isBoardering = new bool[count];

        #endregion
        #region factory
        PieceDefinition.factory_enabled = new bool[count];
        PieceDefinition.factory_amount = new int[count];
        PieceDefinition.factory_isRoundMultiplier = new bool[count];
        PieceDefinition.factory_isGroup = new bool[count];
        PieceDefinition.factory_groupAmount = new int[count];

        #endregion
        #region sanctuary
        PieceDefinition.sanctuary_enabled = new bool[count];
        PieceDefinition.sanctuary_range = new int[count];

        #endregion
        #region conversion Factory
        PieceDefinition.conversionFactory_enabled = new bool[count];
        PieceDefinition.conversionFactory_isCoreHealth = new bool[count];
        PieceDefinition.conversionFactory_isVp = new bool[count];
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

        #endregion
        #region sacrificeCost
        PieceDefinition.sacrificeCost_enabled = new bool[count];
        PieceDefinition.sacrificeCost_isNeedsSpecificPiece = new bool[count];
        PieceDefinition.sacrificeCost_specificPiece = new int[count];
        PieceDefinition.sacrificeCost_howManyItNeeds = new int[count];

        #endregion
        #region Feeding Ground
        PieceDefinition.feedingGround_enabled = new bool[count];
        PieceDefinition.feedingGround_payOut = new int[count];
        PieceDefinition.feedingGround_Range = new int[count];
        #endregion
    }

}

