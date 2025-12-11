// Assets/Scripts/UI/HIC/BuildMenuPresenter.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public sealed class BuildMenuPresenter : MonoBehaviour
{
    [Header("Wiring")]
    public Transform listContent;            // BuildPanel/BuildScroll/Viewport/Content
    public BuildMenuItemView itemPrefab;     // prefab with Button + Icon/Name/Cost

    public event Action<BuildItem> OnItemClicked;

    private readonly List<BuildMenuItemView> _pool = new();


    public void Show(IEnumerable<BuildItem> rawItems, InteractionConfig config)
    {
        IEnumerable<BuildItem> items;
        if (config.GiveRawActionOffers)
        {
            items = rawItems;
        }
        else
        {
            items = filteredBuildOptions(rawItems);
        }

        gameObject.SetActive(true);
        int i = 0;
        foreach (var it in items)
        {
            var view = Ensure(i++);
            view.Bind(it, OnItemClicked, PieceDefinition.isBuildingByType[it.pieceType], PieceDefinition.factionNameByType[it.pieceType]);
            view.gameObject.SetActive(true);
        }
        for (; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
    }

    private IEnumerable<BuildItem> filteredBuildOptions(IEnumerable<BuildItem> items)
    {
        var filteredItems = new List<BuildItem>();
        var iHaveAlreadySeenYou = new HashSet<byte>();

        foreach (var item in items)
        {
            if (iHaveAlreadySeenYou.Add(item.pieceType))
            {
                filteredItems.Add(item);
            }
        }
        return filteredItems;
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

public struct FilterForBuildItems
{
    bool onlyBuildings;

    bool faction1;
    bool faction2;
    bool faction3;
}