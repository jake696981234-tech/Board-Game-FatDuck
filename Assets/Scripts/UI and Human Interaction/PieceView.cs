using UnityEngine;
using TMPro;

public sealed class PieceView : MonoBehaviour
{
    [Header("Highlights (optional)")]
    public GameObject ownerHighlight;     // e.g., a child ring
    public GameObject selectedHighlight;  // toggled when selected

    public GameObject[] Walls;

    public int pieceIndex;
    public int cellId;
    public int owner;
    public byte type;

    public byte? wallConfig;

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



    public void setWalls()
    {
        if (wallConfig != null)
        {
            for (int i = 0; i < 6; i++)
            {
                Walls[i].SetActive(((wallConfig >> i) & 1) == 0);
            }
        }
        else
        {
            for (int i = 0; i < 6; i++)
            {
                Walls[i].SetActive(false);
            }
        }
    }
}
