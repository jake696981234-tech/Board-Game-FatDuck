// Assets/Scripts/UI/HIC/BuildMenuItemView.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using static Game.Core.ActionKind;


public sealed class BuildMenuItemView : MonoBehaviour
{
    public Button button;
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text costText;
    public GameObject illegalBadge;

    public Image FactionColourSet;
    public Image BuildingColourSet;

    private Game.Core.Action cachedAction;
    public UIInfo cachedUIInfo;

    public void Bind(Game.Core.Action theAction, UIInfo uiInfo, Action<Game.Core.Action, UIInfo> onClick)
    {
        setFactionColor(theAction);
        setIfBuildingColor(theAction);
        cachedAction = theAction;
        if (nameText) nameText.text = Piece.name[theAction.TargetType];
        if (costText) costText.text = uiInfo.fullCost.ToString();

        var sprite = getSprite(theAction);
        icon.sprite = sprite;
        icon.enabled = sprite != null;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke(cachedAction, uiInfo));

        setLegality(uiInfo.legal);
    }

    private Sprite getSprite(Game.Core.Action theAction)
    {
        if (theAction.kind == Spawner) return !string.IsNullOrEmpty(Piece.spritePath[Piece.spawn_targetType[UIBridge.bm.GetPieceTypeFromCell(theAction.ActorsCell)]]) ? Resources.Load<Sprite>(Piece.spritePath[Piece.spawn_targetType[UIBridge.bm.GetPieceTypeFromCell(theAction.ActorsCell)]]) : null;
        return !string.IsNullOrEmpty(Piece.spritePath[theAction.TargetType]) ? Resources.Load<Sprite>(Piece.spritePath[theAction.TargetType]) : null;
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
