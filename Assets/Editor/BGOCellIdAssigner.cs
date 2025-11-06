// Assets/Editor/BGOCellIdAssigner.cs
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class BGOCellIdAssigner
{
    // Matches: Cell (-8,0)  or  Cell ( 12, -3 )
    static readonly Regex NameRx = new(@"^Cell\s*\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)\s*$", RegexOptions.Compiled);

    [MenuItem("Tools/BGO/Assign Cell IDs (from names)")]
    public static void AssignCellIdsFromNames()
    {
        // 1) Find all CellViews in scene
        var cells = Object.FindObjectsByType<CellView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (cells == null || cells.Length == 0)
        {
            EditorUtility.DisplayDialog("BGO Assign IDs", "No CellView components found in the scene.", "OK");
            return;
        }

        // 2) Parse all names to collect (q,r)
        var qrList = new List<(CellView cv, short q, short r)>(cells.Length);
        int parseFail = 0;
        foreach (var cv in cells)
        {
            if (cv == null) continue;
            var m = NameRx.Match(cv.gameObject.name);
            if (!m.Success) { parseFail++; continue; }
            short q = short.Parse(m.Groups[1].Value);
            short r = short.Parse(m.Groups[2].Value);
            qrList.Add((cv, q, r));
        }

        if (qrList.Count == 0)
        {
            EditorUtility.DisplayDialog("BGO Assign IDs", $"Could not parse any cell names. Parse failed: {parseFail}", "OK");
            return;
        }

        // 3) Infer radius from max axial norm over provided cells
        int R = 0;
        foreach (var (_, q, r) in qrList)
        {
            int k = Mathf.Max(Mathf.Abs(q), Mathf.Abs(r), Mathf.Abs(q + r));
            if (k > R) R = k;
        }

        // 4) Build the EXACT idByAxial mapping GeometryBuilder uses (q-major)
        //    See GeometryBuilder.Build: q = -R..R; r from rmin..rmax; id = coords.Count; map[(q,r)] = id
        var idByAxial = new Dictionary<(short, short), int>(qrList.Count);
        for (int q = -R; q <= R; q++)
        {
            int rmin = Mathf.Max(-R, -q - R);
            int rmax = Mathf.Min( R, -q + R);
            for (int r = rmin; r <= rmax; r++)
            {
                int id = idByAxial.Count;
                idByAxial[((short)q, (short)r)] = id;
            }
        }

        // 5) Assign ids to CellViews by looking up (q,r) → id
        Undo.RecordObjects(cells, "Assign Cell IDs");
        int ok = 0, missing = 0;
        foreach (var (cv, q, r) in qrList)
        {
            if (!idByAxial.TryGetValue((q, r), out int id))
            {
                missing++;
                continue;
            }
            cv.cellId = id;
            if (cv.idLabel) cv.idLabel.text = id.ToString();
            EditorUtility.SetDirty(cv);
            ok++;
        }

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("BGO Assign IDs",
            $"Radius (inferred): {R}\n" +
            $"Assigned: {ok}\n" +
            $"Missing (outside shape?): {missing}\n" +
            $"Name parse failed: {parseFail}",
            "OK");

        Debug.Log($"[BGO] Assign Cell IDs — R={R}, Assigned={ok}, Missing={missing}, ParseFail={parseFail}");
    }
}
