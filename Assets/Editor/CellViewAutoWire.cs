#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;

public static class CellViewAutoWire
{
    [MenuItem("Tools/BGO/Auto-Wire CellView References")]
    public static void AutoWire()
    {
        var cellViews = Object.FindObjectsByType<CellView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int wiredCount = 0;

        Undo.RecordObjects(cellViews, "Auto-wire CellView refs");

        foreach (var cv in cellViews)
        {
            if (cv == null) continue;

            // Try find child SpriteRenderer if baseSprite not set
            if (cv.baseSprite == null)
            {
                var sr = cv.GetComponentInChildren<SpriteRenderer>(true);
                if (sr != null)
                {
                    cv.baseSprite = sr;
                }
            }

            // Try find child TextMeshPro if idLabel not set
            if (cv.idLabel == null)
            {
                var tmp = cv.GetComponentInChildren<TextMeshPro>(true);
                if (tmp != null)
                {
                    cv.idLabel = tmp;
                }
            }

            // Mark as dirty if we changed anything
            EditorUtility.SetDirty(cv);
            wiredCount++;
        }

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Auto-Wire CellView", $"Processed {wiredCount} CellViews.", "OK");
    }
}
#endif
