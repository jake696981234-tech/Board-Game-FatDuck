// Assets/Scripts/UI/HIC/BuildMenuItemView.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BuildMenuItemView : MonoBehaviour
{
    public Button button;
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text costText;
    public GameObject illegalBadge;

    public Image FactionColourSet;
    public Image BuildingColourSet;

    private BuildItem _data;

    public void Bind(BuildItem data, Action<BuildItem> onClick, bool isBuilding, string factionName)
    {
        FactionColourSet.color = FactionColorUtil.ColorFromString(factionName);

        if (isBuilding)
        {
            BuildingColourSet.color = Color.darkCyan;
        }
        else
        {
            BuildingColourSet.color = Color.darkGoldenRod;
        }

        _data = data;
        if (nameText) nameText.text = data.name;
        if (costText) costText.text = data.cost.ToString();

        if (icon)
        {
            var sprite = !string.IsNullOrEmpty(data.spritePath) ? Resources.Load<Sprite>(data.spritePath) : null;
            icon.sprite = sprite;
            icon.enabled = (sprite != null);
        }

        if (illegalBadge) illegalBadge.SetActive(!data.legal);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke(_data));
        button.interactable = data.legal; // change to true if you want illegal items clickable for tooltips
    }
}
