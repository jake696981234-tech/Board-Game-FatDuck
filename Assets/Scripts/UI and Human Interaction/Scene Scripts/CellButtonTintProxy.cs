// Assets/Scripts/UI and Human Interaction/CellButtonTintProxy.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Drives a SpriteRenderer's color from a Button's ColorBlock (normal/hover/pressed),
/// unless the CellView is in "forcedTint" mode (used by highlights).
[RequireComponent(typeof(Button))]
public sealed class CellButtonTintProxy :
    MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public CellView cell;                  // assign the same cell this button belongs to
    public SpriteRenderer sprite;          // cell.baseSprite (white png)
    public bool applyWhenDisabled = false; // optional

    public InteractionConfig config; // optional; if null we fall back to Button's ColorBlock

    Button _btn;
    bool _hover, _pressed;

    void Awake()
    {
        _btn = GetComponent<Button>();
        if (!cell) cell = GetComponentInParent<CellView>();
        if (!sprite && cell) sprite = cell.baseSprite;
        if (!config)
        {
            var hic = FindFirstObjectByType<HumanInteractionController>();
            if (hic) config = hic.config;
        }
        Apply();
    }

    void OnEnable()  => Apply();
    void OnDisable() => Restore();

    public void OnPointerEnter(PointerEventData e) { _hover = true; Apply(); }
    public void OnPointerExit (PointerEventData e) { _hover = false; _pressed = false; Apply(); }
    public void OnPointerDown (PointerEventData e) { _pressed = true;  Apply(); }
    public void OnPointerUp   (PointerEventData e) { _pressed = false; Apply(); }

    public void ApplyNow() => Apply();

    void Apply()
    {
        if (!sprite || !cell || !_btn) return;

        // If a highlight/forced tint is active, do not fight it.
        if (cell.tintForced) return;

         // Prefer InteractionConfig colours; fall back to Button ColorBlock if config not available
        if (config)
        {
            Color c;
            if (!_btn.interactable && !applyWhenDisabled) c = config.cellDisabledTint;
            else if (_pressed)                            c = config.cellPressedTint;
            else if (_hover)                              c = config.cellHoverTint;
            else                                          c = config.defaultCellColor;
            sprite.color = c;
            return;
        }
        else
        {
            var cb = _btn.colors;
            Color c;
            if (!_btn.interactable && !applyWhenDisabled) c = cb.disabledColor;
            else if (_pressed)                            c = cb.pressedColor;
            else if (_hover)                              c = cb.highlightedColor;
            else                                          c = cb.normalColor;
            sprite.color = c;
        }
    }

    void Restore()
    {
        if (!sprite || !cell) return;
        if (cell.tintForced) return;
        sprite.color = config ? config.defaultCellColor : Color.white;
    }
}
