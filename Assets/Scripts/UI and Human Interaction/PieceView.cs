using UnityEngine;
using TMPro;

public sealed class PieceView : MonoBehaviour
{
    [Header("Highlights (optional)")]
    public GameObject ownerHighlight;     // e.g., a child ring
    public GameObject selectedHighlight;  // toggled when selected

    public GameObject TopLeft;
    public GameObject TopRight;
    public GameObject MiddleRight;
    public GameObject BottomRight;
    public GameObject BottomLeft;
    public GameObject MiddleLeft;

    public int pieceIndex;
    public int cellId;
    public int owner;
    public byte type;

    public SpriteRenderer spriteRenderer;
    public TextMeshPro hpLabel;
    public GameObject teamMarkGO;


    public void Init() { }

    public void SetWorldPosition(Vector3 p) { transform.position = p; }
    public void SetSprite(Sprite s, bool visibleIfNull = false)
    {
        if (!spriteRenderer) return;
        spriteRenderer.sprite = s;
        spriteRenderer.enabled = visibleIfNull || s != null;
    }
    public void SetTint(Color c)
    {
        var r = ownerHighlight.GetComponentInChildren<Renderer>();
        if (r is SpriteRenderer sr) sr.color = c;
    }
    public void SetSelectedHighlight(bool v)
    {
        if (selectedHighlight) selectedHighlight.SetActive(v);
    }
    public void SetHP(short hp, bool show)
    {
        if (!hpLabel) return;
        hpLabel.gameObject.SetActive(show);
        hpLabel.text = hp.ToString();
    }
    public void SetVisible(bool on) { gameObject.SetActive(on); }



    public void setWalls(byte[] mask)
    {
        if (mask[0] is 0) TopLeft.SetActive(true);
        if (mask[1] is 0) TopRight.SetActive(true);
        if (mask[2] is 0) MiddleRight.SetActive(true);
        if (mask[3] is 0) BottomRight.SetActive(true);
        if (mask[4] is 0) BottomLeft.SetActive(true);
        if (mask[5] is 0) MiddleLeft.SetActive(true);
    }


}
