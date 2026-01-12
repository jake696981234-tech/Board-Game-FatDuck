using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AxialCordSet
{
    [MenuItem("Tools/BGO/Assign Axial Cords (from Cell IDs)")]
    public static void AssignAxialCordsFromCellIds()
    {
        var cells = Object.FindObjectsByType<CellView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (cells == null || cells.Length == 0)
        {
            EditorUtility.DisplayDialog("BGO Axial Cords", "No CellView components found in the scene.", "OK");
            return;
        }

        int maxId = -1;
        int invalidId = 0;
        foreach (var cv in cells)
        {
            if (cv == null) continue;
            if (cv.cellId < 0) { invalidId++; continue; }
            if (cv.cellId > maxId) maxId = cv.cellId;
        }

        if (maxId < 0)
        {
            EditorUtility.DisplayDialog("BGO Axial Cords", "No valid cellId values found.", "OK");
            return;
        }

        int radius = InferRadiusFromMaxId(maxId);
        if (radius < 0)
        {
            EditorUtility.DisplayDialog("BGO Axial Cords", $"Could not infer radius from maxId={maxId}.", "OK");
            return;
        }

        var coordsById = BuildCoordsById(radius);

        Undo.RecordObjects(cells, "Assign Axial Cords");
        int assigned = 0;
        int outOfRange = 0;
        foreach (var cv in cells)
        {
            if (cv == null) continue;
            int id = cv.cellId;
            if (id < 0 || id >= coordsById.Length)
            {
                outOfRange++;
                continue;
            }

            var (q, r) = coordsById[id];
            cv.OneAxialCord = q;
            cv.TwoAxialCord = r;
            EditorUtility.SetDirty(cv);
            assigned++;
        }

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "BGO Axial Cords",
            $"Radius (inferred): {radius}\n" +
            $"Assigned: {assigned}\n" +
            $"Out of range: {outOfRange}\n" +
            $"Invalid IDs: {invalidId}",
            "OK");
    }

    private static int InferRadiusFromMaxId(int maxId)
    {
        for (int r = 0; r <= 20; r++)
        {
            int cellCount = 1 + 3 * r * (r + 1);
            if (maxId < cellCount) return r;
        }
        return -1;
    }

    private static (int q, int r)[] BuildCoordsById(int radius)
    {
        int cellCount = 1 + 3 * radius * (radius + 1);
        var coords = new List<(int q, int r)>(cellCount);
        // Match GeometryBuilder.Build() enumeration order (q-major, r within rmin..rmax).
        for (int q = -radius; q <= radius; q++)
        {
            int rmin = Mathf.Max(-radius, -q - radius);
            int rmax = Mathf.Min(radius, -q + radius);
            for (int r = rmin; r <= rmax; r++)
            {
                coords.Add((q, r));
            }
        }
        return coords.ToArray();
    }
}
