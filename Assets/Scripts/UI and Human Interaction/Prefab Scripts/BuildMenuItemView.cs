// Assets/Scripts/UI/HIC/BuildMenuItemView.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public sealed class BuildMenuItemView : MonoBehaviour
{
    public Button button;
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text costText;
    public GameObject illegalBadge;

    public Image FactionColourSet;
    public Image BuildingColourSet;

    private Game.Core.Action _data;

    public void Bind(Game.Core.Action data, UIInfo uiInfo, Action<Game.Core.Action, UIInfo> onClick)
    {
        FactionColourSet.color = FactionColorUtil.ColorFromString(Piece.factionName[data.TargetType]);

        if (Piece.isBuilding[data.TargetType])
        {
            BuildingColourSet.color = Color.darkCyan;
        }
        else
        {
            BuildingColourSet.color = Color.darkGoldenRod;
        }

        _data = data;
        if (nameText) nameText.text = Piece.name[data.TargetType];
        if (costText) costText.text = uiInfo.fullCost.ToString();

        if (icon)
        {
            var sprite = !string.IsNullOrEmpty(Piece.spritePath[data.TargetType]) ? Resources.Load<Sprite>(Piece.spritePath[data.TargetType]) : null;
            icon.sprite = sprite;
            icon.enabled = (sprite != null);
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke(_data, uiInfo));

        setLegality(uiInfo.legal);
    }

    public void setLegality(bool legal)
    {
        if (illegalBadge) illegalBadge.SetActive(!legal);
        button.interactable = legal; // change to true if you want illegal items clickable for tooltips
    }
}
