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
        FactionColourSet.color = FactionColorUtil.ColorFromString(PieceDefinition.factionName[data.pieceType]);

        if (PieceDefinition.isBuilding[data.pieceType])
        {
            BuildingColourSet.color = Color.darkCyan;
        }
        else
        {
            BuildingColourSet.color = Color.darkGoldenRod;
        }

        _data = data;
        if (nameText) nameText.text = PieceDefinition.name[data.pieceType];
        if (costText) costText.text = uiInfo.fullCost.ToString();

        if (icon)
        {
            var sprite = !string.IsNullOrEmpty(PieceDefinition.spritePath[data.pieceType]) ? Resources.Load<Sprite>(PieceDefinition.spritePath[data.pieceType]) : null;
            icon.sprite = sprite;
            icon.enabled = (sprite != null);
        }

        if (illegalBadge) illegalBadge.SetActive(!uiInfo.legal);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke(_data, uiInfo));
        button.interactable = uiInfo.legal; // change to true if you want illegal items clickable for tooltips
    }
}
