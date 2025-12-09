using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Forwards a UI Button click on a cell to UIInput.OnCellClicked(cellId).
/// Ensures the world-space canvas is ready to receive events (camera + raycaster).
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(UnityEngine.UI.GraphicRaycaster))]
public sealed class CellButtonClickForwarder : MonoBehaviour
{
    public CellView cell;                    // assign (or auto-find parent)

    Button _btn;

    void Awake()
    {
        _btn = GetComponent<Button>();
        if (!cell) cell = GetComponentInParent<CellView>();

        _btn.onClick.RemoveAllListeners();
        _btn.onClick.AddListener(OnClicked);
    }

    void OnClicked()
    {
        if (cell) UIInput.OnCellClicked(cell.cellId);
    }
}
