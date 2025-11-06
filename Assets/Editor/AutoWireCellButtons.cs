// Assets/Editor/AutoWireCellButtons.cs
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class AutoWireCellButtons : MonoBehaviour
{
    [MenuItem("Tools/Board/Auto Wire Cell Buttons")]
    public static void AutoWireAllCells()
    {
        var cells = Object.FindObjectsByType<CellView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;

        foreach (var cell in cells)
        {
            if (!cell) continue;

            // Check if it already has a Button child
            var existing = cell.GetComponentInChildren<Button>(true);
            if (existing != null)
            {
                Debug.Log($"[AutoWire] Skipped (already has Button): {cell.name}");
                continue;
            }

            // Create child object for the Button
            var buttonGO = new GameObject("CellButton", typeof(RectTransform));
            buttonGO.transform.SetParent(cell.transform, false);

            var rect = buttonGO.GetComponent<RectTransform>();
            rect.localPosition = Vector3.zero;
            rect.sizeDelta = Vector2.one * 2.965f; // adjust to your cell size

            // Add Canvas (world-space)
            var canvas = buttonGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            buttonGO.AddComponent<GraphicRaycaster>();

            // Add a dummy transparent Image (needed by Button)
            var img = buttonGO.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0f);

            // Add Button
            var btn = buttonGO.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.9f, 0.9f, 1f);
            cb.pressedColor = new Color(0.7f, 0.7f, 1f);
            cb.selectedColor = new Color(0.8f, 0.8f, 1f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f);
            cb.colorMultiplier = 1f;
            btn.colors = cb;

            // Add tint proxy and wire fields
            var proxy = buttonGO.AddComponent<CellButtonTintProxy>();
            proxy.cell = cell;
            proxy.sprite = cell.baseSprite;

            // NEW: forward clicks to BoardViewController.CellClicked(cellId)
            var fwd = buttonGO.AddComponent<CellButtonClickForwarder>();
            fwd.cell = cell;
            fwd.boardView = cell.GetComponentInParent<BoardViewController>();

            // Done
            count++;
            Debug.Log($"[AutoWire] Added Button + TintProxy to {cell.name}");
        }

        Debug.Log($"[AutoWire] Finished wiring {count} cells.");
    }
}
