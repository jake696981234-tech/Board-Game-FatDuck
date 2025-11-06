// Assets/Scripts/UI/HIC/BuildMenuPresenter.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BuildMenuPresenter : MonoBehaviour
{
    [Header("Wiring")]
    public Transform listContent;            // BuildPanel/BuildScroll/Viewport/Content
    public BuildMenuItemView itemPrefab;     // prefab with Button + Icon/Name/Cost

    public event Action<BuildItem> OnItemClicked;

    private readonly List<BuildMenuItemView> _pool = new();

    public void Show(IEnumerable<BuildItem> items)
    {
        gameObject.SetActive(true);
        int i = 0;
        foreach (var it in items)
        {
            var view = Ensure(i++);
            view.Bind(it, OnItemClicked);
            view.gameObject.SetActive(true);
        }
        for (; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
    }

    public void Hide() => gameObject.SetActive(false);

    private BuildMenuItemView Ensure(int index)
    {
        while (_pool.Count <= index)
        {
            var v = Instantiate(itemPrefab, listContent);
            _pool.Add(v);
        }
        return _pool[index];
    }
}
