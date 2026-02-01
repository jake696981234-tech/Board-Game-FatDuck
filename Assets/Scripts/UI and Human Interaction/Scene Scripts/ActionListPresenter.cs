// Assets/Scripts/UI/HIC/ActionListPresenter.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public sealed class ActionListPresenter : MonoBehaviour
{
    [Header("Wiring")]
    public Transform listContent;            // PieceActionPanel/ActionScroll/Viewport/Content
    public ActionListItemView itemPrefab;

    public event Action<ActionItem> OnItemClicked;

    public readonly List<ActionListItemView> _pool = new();
    public ActionItem[] theItems;

    public void Show(IEnumerable<ActionItem> items)
    {
        theItems = items.ToArray();
        gameObject.SetActive(true);
        int i = 0;
        foreach (var it in items)
        {
            var v = Ensure(i++);
            v.Bind(it, OnItemClicked);
            v.gameObject.SetActive(true);
        }
        for (; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
    }


    public void Hide() => gameObject.SetActive(false);

    private ActionListItemView Ensure(int index)
    {
        while (_pool.Count <= index)
        {
            var v = Instantiate(itemPrefab, listContent);
            _pool.Add(v);
        }
        return _pool[index];
    }
}
