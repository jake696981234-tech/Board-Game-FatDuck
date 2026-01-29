// Assets/Scripts/UI/HIC/ActionListItemView.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using static Game.Core.ActionKind;

public sealed class ActionListItemView : MonoBehaviour
{
    public Button  button;
    public TMP_Text actionNameText;
    public TMP_Text costText;
    public GameObject illegalBadge;

    private int cachedTargetType;

    public void Bind(int targetType, Action<int> onClick)
    {
        cachedTargetType = targetType;
        actionNameText.text = Piece.name[targetType];
        costText.text       = Piece.BuildCost[targetType].ToString();
        // if (illegalBadge)   illegalBadge.SetActive(!data.legal);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke(targetType));
        bool legal = false;
        for (int i = 0; i < UIBridge._count; i++)
        {
            if (UIBridge._offers[i].kind != Create) continue; //if upgrade or group build dont work, this is probs the reason why.
            if (UIBridge._offers[i].TargetType != targetType) continue;
            if (UIBridge._mask[i] != 0) continue;
            legal = true;
            break;
        }
        setLegality(legal);
    }

    public void setLegality(bool legal)
    {
        if (illegalBadge)   illegalBadge.SetActive(!legal);
        button.interactable = legal; // or true to allow clicking and show why illegal
    }
}
