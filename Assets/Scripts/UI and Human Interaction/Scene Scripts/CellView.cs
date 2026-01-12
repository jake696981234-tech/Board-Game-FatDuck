using UnityEngine;
using TMPro;

public sealed class CellView : MonoBehaviour
{
    [Header("Identity")]
    public int cellId;

    public int OneAxialCord;
    public int TwoAxialCord;

    public (short q, short r) AxialCord => ((short)OneAxialCord, (short)TwoAxialCord);

    public bool tintForced { get; private set; }

    [Header("Visuals")]
    public SpriteRenderer baseSprite;
    public GameObject highlightGO;
    public TextMeshPro idLabel;

    [Tooltip("Dynamic number shown for VP pool or Core HP on special cells.")]
    public TextMeshPro statusLabel;


    public void Init()
    {
        SetHighlight(false);
        SetIdVisible(false);
        SetStatusVisible(false);
    }

    public void SetBaseColor(Color c) { if (baseSprite) baseSprite.color = c; }
    public void SetHighlight(bool on) { if (highlightGO) highlightGO.SetActive(on); }
    public void SetIdVisible(bool on) { if (idLabel) idLabel.gameObject.SetActive(on); }
    public void SetIdText(string text) { if (idLabel) idLabel.text = text; }

    public void SetStatusVisible(bool on)
    {
        if (statusLabel) statusLabel.gameObject.SetActive(on);
    }

    public void SetStatusText(string text)
    {
        if (statusLabel) statusLabel.text = text;
    }

    public void SetForcedTint(Color? c)
    {
        tintForced = c.HasValue;
        baseSprite.color = c ?? Color.white;
    }

}
