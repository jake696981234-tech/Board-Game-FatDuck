using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Forwards a UI Button click on a cell to BoardViewController.NotifyCellClicked(cellId).
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class CellButtonClickForwarder : MonoBehaviour
{
    public CellView cell;                    // assign (or auto-find parent)
    public BoardViewController boardView;    // assign (or auto-find parent)

    Button _btn;

    void Awake()
    {
        _btn = GetComponent<Button>();
        if (!cell)      cell      = GetComponentInParent<CellView>();
        if (!boardView) boardView = GetComponentInParent<BoardViewController>();

        _btn.onClick.RemoveAllListeners();
        _btn.onClick.AddListener(OnClicked);
    }

    void OnClicked()
    {
        if (cell && boardView) boardView.NotifyCellClicked(cell.cellId);
    }
}