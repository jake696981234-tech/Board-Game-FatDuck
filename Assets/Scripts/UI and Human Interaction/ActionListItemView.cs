// Assets/Scripts/UI/HIC/ActionListItemView.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActionListItemView : MonoBehaviour
{
    public Button  button;
    public TMP_Text actionNameText;
    public TMP_Text costText;
    public GameObject illegalBadge;

    private ActionItem _data;

    public void Bind(ActionItem data, Action<ActionItem> onClick)
    {
        _data = data;
        if (actionNameText) actionNameText.text = data.name;
        if (costText)       costText.text       = data.cost.ToString();
        if (illegalBadge)   illegalBadge.SetActive(!data.legal);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke(_data));
        button.interactable = data.legal; // or true to allow clicking and show why illegal
    }
}
