// Assets/Editor/CellViewStatusLabelPlacer.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;

public static class CellViewStatusLabelPlacer
{
    private const string kPrefabSearch = "statusLabel t:prefab"; // looks for a prefab named "statusLabel" (case-insensitive)

    [MenuItem("Tools/BGO/Add & Wire 'statusLabel' Prefab To Cells")]
    public static void AddAndWireStatusLabels()
    {
        // 1) Locate the statusLabel prefab in the project
        var guids = AssetDatabase.FindAssets(kPrefabSearch);
        if (guids == null || guids.Length == 0)
        {
            EditorUtility.DisplayDialog("BGO Status Labels",
                "Could not find a prefab named 'statusLabel' in the project.\n\n" +
                "Tip: ensure the prefab is named exactly 'statusLabel' (any folder), or change the search string in this tool.",
                "OK");
            return;
        }

        // We'll just use the first match
        var prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        var statusLabelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (statusLabelPrefab == null)
        {
            EditorUtility.DisplayDialog("BGO Status Labels", "Failed to load the 'statusLabel' prefab.", "OK");
            return;
        }

        // 2) Find all CellView components (including inactive)
        var cells = Object.FindObjectsByType<CellView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (cells == null || cells.Length == 0)
        {
            EditorUtility.DisplayDialog("BGO Status Labels", "No CellView components found in the scene.", "OK");
            return;
        }

        int wired = 0, created = 0, alreadyWired = 0, missingTMP = 0;

        Undo.RegisterCompleteObjectUndo(cells, "Add & Wire Status Labels");

        foreach (var cv in cells)
        {
            if (cv == null) continue;

            // If already wired, skip (don’t create duplicates)
            if (cv.statusLabel != null)
            {
                alreadyWired++;
                continue;
            }

            // Check if a child instance of the statusLabel prefab already exists (by name match)
            // This avoids creating duplicates if you’ve already placed one manually.
            Transform existingChild = cv.transform.Find(statusLabelPrefab.name);

            GameObject instanceGo = null;

            if (existingChild == null)
            {
                // Create a new instance as a child of the cell
                instanceGo = (GameObject)PrefabUtility.InstantiatePrefab(statusLabelPrefab, cv.transform);
                if (instanceGo == null)
                {
                    Debug.LogWarning($"[BGO] Failed to instantiate statusLabel under '{cv.name}'.");
                    continue;
                }

                // Reset local transform so prefab’s own offsets are preserved (or zero them if you prefer)
                // instanceGo.transform.localPosition = Vector3.zero;
                // instanceGo.transform.localRotation = Quaternion.identity;
                // instanceGo.transform.localScale    = Vector3.one;

                created++;
            }
            else
            {
                instanceGo = existingChild.gameObject;
            }

            // Critically: only search for TMP **inside the prefab instance** we used/created.
            // This prevents us grabbing any other TMPs that already live under the cell.
            var tmp = instanceGo.GetComponentInChildren<TextMeshPro>(true);
            if (tmp != null)
            {
                cv.statusLabel = tmp;
                EditorUtility.SetDirty(cv);
                wired++;
            }
            else
            {
                // If the prefab doesn’t contain a TMP, let the user know.
                missingTMP++;
                Debug.LogWarning($"[BGO] The 'statusLabel' prefab under '{cv.name}' has no TextMeshPro component. " +
                                 $"Please add a TMP component inside that prefab.");
            }
        }

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("BGO Status Labels",
            $"Cells processed: {cells.Length}\n" +
            $"• Created new statusLabel instances: {created}\n" +
            $"• Wired statusLabel references:       {wired}\n" +
            $"• Already wired (skipped):            {alreadyWired}\n" +
            $"• Prefab without TMP (check prefab):  {missingTMP}",
            "OK");
    }
}
#endif
