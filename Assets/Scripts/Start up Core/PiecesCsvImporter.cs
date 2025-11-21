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
    public static Pieces Import(string pathToPiecesCsv, Action<int,string,bool,string,int> onTypeDefined = null)
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
        // Worst-case: each row can enable up to 4 abilities
        int abilityEstimate = Math.Max(4, typeCount * 4);

        // 2) Allocate Pieces registry
        var pcs = new Pieces();
        pcs.Allocate(typeCount, abilityEstimate, /*maxSlots*/4);

        // We will SYNTHESIZE abilities; keep a moving cursor for the next id.
        int nextAbilityId = 0; // grows as we define abilities; finalized into pcs.abilityCount at the end

        // Header map for name→index
        var H = BuildHeaderIndex(headers);

        // Canonical slot order (by availability): Move(0), Shoot(1), CaptureVP(2), DamageCore(3)
        
        // 3) Define types first (stable typeId = row index)
        for (int typeId = 0; typeId < typeCount; typeId++)
        {
            var cols = rows[typeId];
            string name = Get(cols, H, "name", required:true);
            pcs.DefineType(typeId, name);

            // Basic descriptive fields (optional where noted)
            string faction      = Get(cols, H, "faction", defaultValue:"");
            bool   isBuilding   = GetBool(cols, H, "isBuilding", false);
            int    buildCost    = GetInt(cols, H, "buildCost", 0);
            int    digitsReq    = GetInt(cols, H, "digitsRequired", 0);
            string spritePath   = Get(cols, H, "spritePath", defaultValue:"");
            string moveColor    = Get(cols, H, "moveColor", defaultValue:"");
            string shootColor   = Get(cols, H, "shootColor", defaultValue:"");
            bool   buildable    = GetBool(cols, H, "buildable", true);
            int    grantsDigit  = GetInt(cols, H, "grantsDigit", -1);
            short  maxHP        = (short)ClampToShort(GetInt(cols, H, "maxHP", 10));

            // Map to backing arrays if present in schema
            if (typeId < pcs.idByType.Length) pcs.idByType[typeId] = name;
            if (typeId < pcs.grantsDigitByType.Length)     pcs.grantsDigitByType[typeId] = (sbyte)grantsDigit;
            if (typeId < pcs.displayNameByType.Length)       pcs.displayNameByType[typeId] = name; // default display = name
            if (typeId < pcs.factionNameByType.Length)       pcs.factionNameByType[typeId] = faction;
            if (typeId < pcs.spritePathByType.Length)        pcs.spritePathByType[typeId] = spritePath;
            if (typeId < pcs.moveUIColorHexByType.Length)    pcs.moveUIColorHexByType[typeId] = moveColor;
            if (typeId < pcs.shootUIColorHexByType.Length)   pcs.shootUIColorHexByType[typeId] = shootColor;
            if (typeId < pcs.isBuildingByType.Length)        pcs.isBuildingByType[typeId] = isBuilding;
            if (typeId < pcs.buildCostByType.Length)         pcs.buildCostByType[typeId] = buildCost;
            if (typeId < pcs.maxHPByType.Length)             pcs.maxHPByType[typeId] = maxHP;
            if (typeId < pcs.buildableByType.Length)       pcs.buildableByType[typeId] = (byte)(buildable ? 1 : 0);

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
                int dmg  = GetInt(cols, H, "shoot_damage", 1);
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
        if (a < pcs.rangeMin.Length)          pcs.rangeMin[a] = rangeMin;
        if (a < pcs.rangeMax.Length)          pcs.rangeMax[a] = rangeMax;
        if (a < pcs.areaRadius.Length)        pcs.areaRadius[a] = 0;
        if (a < pcs.damage.Length)            pcs.damage[a] = damage;
        if (a < pcs.customParam.Length)       pcs.customParam[a] = 0;
        if (a < pcs.baseSurcharge.Length)     pcs.baseSurcharge[a] = 0;
        if (a < pcs.botThinkSurcharge.Length) pcs.botThinkSurcharge[a] = botSurcharge;
        if (a < pcs.buildTypeId.Length)       pcs.buildTypeId[a] = -1; // not used
        return a;
    }

    private static int DefineSynthAbility_Shoot(Pieces pcs, string typeName, int rangeMin, int rangeMax, int damage, ref int nextA, int botSurcharge)
    {
        int a = nextA++; // next synthesized id
        string name = $"Shoot@{typeName}";
        pcs.DefineAbility(a, name, Pieces.AbilityKind.Shoot, Pieces.TargetKind.Piece);
        if (a < pcs.rangeMin.Length)          pcs.rangeMin[a] = rangeMin;
        if (a < pcs.rangeMax.Length)          pcs.rangeMax[a] = rangeMax;
        if (a < pcs.areaRadius.Length)        pcs.areaRadius[a] = 0;
        if (a < pcs.damage.Length)            pcs.damage[a] = damage;
        if (a < pcs.customParam.Length)       pcs.customParam[a] = 0;
        if (a < pcs.baseSurcharge.Length)     pcs.baseSurcharge[a] = 0;
        if (a < pcs.botThinkSurcharge.Length) pcs.botThinkSurcharge[a] = botSurcharge;
        if (a < pcs.buildTypeId.Length)       pcs.buildTypeId[a] = -1; // not used
        return a;
    }

    private static int DefineSynthAbility_Capture(Pieces pcs, string typeName, ref int nextA, int botSurcharge)
    {
        int a = nextA++; // use the moving cursor
        string name = $"CaptureVP@{typeName}";
        pcs.DefineAbility(a, name, Pieces.AbilityKind.CaptureVP, Pieces.TargetKind.Cell);
        if (a < pcs.rangeMin.Length)          pcs.rangeMin[a] = 0;
        if (a < pcs.rangeMax.Length)          pcs.rangeMax[a] = 0;
        if (a < pcs.areaRadius.Length)        pcs.areaRadius[a] = 0;
        if (a < pcs.damage.Length)            pcs.damage[a] = 0;
        if (a < pcs.customParam.Length)       pcs.customParam[a] = 0;
        if (a < pcs.baseSurcharge.Length)     pcs.baseSurcharge[a] = 0;
        if (a < pcs.botThinkSurcharge.Length) pcs.botThinkSurcharge[a] = botSurcharge;
        if (a < pcs.buildTypeId.Length)       pcs.buildTypeId[a] = -1;
        return a;
    }

    private static int DefineSynthAbility_CoreDamage(Pieces pcs, string typeName, ref int nextA, int botSurcharge)
    {
        int a = nextA++; // use the moving cursor
        string name = $"CoreDamage@{typeName}";
        pcs.DefineAbility(a, name, Pieces.AbilityKind.CoreDamage, Pieces.TargetKind.Cell);
        if (a < pcs.rangeMin.Length)          pcs.rangeMin[a] = 0;
        if (a < pcs.rangeMax.Length)          pcs.rangeMax[a] = 0;
        if (a < pcs.areaRadius.Length)        pcs.areaRadius[a] = 0;
        if (a < pcs.damage.Length)            pcs.damage[a] = 0; // effect is contextual at kernel
        if (a < pcs.customParam.Length)       pcs.customParam[a] = 0;
        if (a < pcs.baseSurcharge.Length)     pcs.baseSurcharge[a] = 0;
        if (a < pcs.botThinkSurcharge.Length) pcs.botThinkSurcharge[a] = botSurcharge;
        if (a < pcs.buildTypeId.Length)       pcs.buildTypeId[a] = -1;
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

    private static Dictionary<string,int> BuildHeaderIndex(string[] headers)
    {
        var map = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            string h = headers[i]?.Trim() ?? string.Empty;
            if (h.Length == 0) continue;
            map[h] = i;
        }
        return map;
    }

    private static string Get(string[] cols, Dictionary<string,int> H, string key, bool required = false, string defaultValue = "")
    {
        if (H.TryGetValue(key, out int idx) && idx >= 0 && idx < cols.Length)
        {
            return cols[idx];
        }
        if (required)
            throw new InvalidDataException($"Missing required column '{key}'");
        return defaultValue;
    }

    private static int GetInt(string[] cols, Dictionary<string,int> H, string key, int defaultValue)
    {
        string s = Get(cols, H, key, required:false, defaultValue:string.Empty);
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
        return defaultValue;
    }

    private static bool GetBool(string[] cols, Dictionary<string,int> H, string key, bool defaultValue)
    {
        string s = Get(cols, H, key, required:false, defaultValue:string.Empty);
        if (string.IsNullOrWhiteSpace(s)) return defaultValue;
        s = s.Trim();
        if (s.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        if (s == "1") return true;
        if (s == "0") return false;
        return defaultValue;
    }

    private static int ClampToShort(int v) => Math.Max(short.MinValue, Math.Min(short.MaxValue, v));

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
