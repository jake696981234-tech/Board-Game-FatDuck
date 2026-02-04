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

    public ActionListItemView[] PieceActionPrefabs;
    public ActionItem[] theItems;

    // public void Show(IEnumerable<ActionItem> items)
    // {
    //     theItems = items.ToArray();
    //     gameObject.SetActive(true);
    //     int i = 0;
    //     foreach (var it in items)
    //     {
    //         var v = Ensure(i++);
    //         v.Bind(it, OnItemClicked);
    //         v.gameObject.SetActive(true);
    //     }
    //     for (; i < PieceActionPrefabs.Count; i++) PieceActionPrefabs[i].gameObject.SetActive(false);
    // }

    public void Show(List<ActionItem> items)
    {
        for (int i = 0; i < PieceActionPrefabs.Length; i++) if (PieceActionPrefabs[i] != null) Destroy(PieceActionPrefabs[i].gameObject);
        PieceActionPrefabs = new ActionListItemView[items.Count()];
        for (int i = 0; i < items.Count(); i++)
        {
            var prefab = Instantiate(itemPrefab, listContent);
            PieceActionPrefabs[i] = prefab;
            prefab.Bind(items[i], OnItemClicked);
            PieceActionPrefabs[i].gameObject.SetActive(true);
        }
    }


    public void Hide() => gameObject.SetActive(false);

    // private ActionListItemView Ensure(int index)
    // {
    //     while (PieceActionPrefabs.Count <= index)
    //     {
    //         var v = Instantiate(itemPrefab, listContent);
    //         PieceActionPrefabs.Add(v);
    //     }
    //     return PieceActionPrefabs[index];
    // }
}
