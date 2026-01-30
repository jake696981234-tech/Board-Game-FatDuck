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
        setFactionColor(data);
        setIfBuildingColor(data);
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

    private void setFactionColor(Game.Core.Action data)
    {
        for (int i = 0; i < UI.hic.config.FactionColors.Count; i++)
        {
            if (UI.hic.config.FactionColors[i].faction == Piece.factionName[data.TargetType])
            {
                FactionColourSet.color = UI.hic.config.FactionColors[i].color;
                return;
            }
        }
        FactionColourSet.color = FactionColorUtil.ColorFromString(Piece.factionName[data.TargetType]);
    }

    private void setIfBuildingColor(Game.Core.Action data)
    {
        if (Piece.isBuilding[data.TargetType]) { BuildingColourSet.color = Color.darkCyan; } else { BuildingColourSet.color = Color.darkGoldenRod; }
    }

    public void setLegality(bool legal)
    {
        if (illegalBadge) illegalBadge.SetActive(!legal);
        button.interactable = legal; // change to true if you want illegal items clickable for tooltips
    }
}
