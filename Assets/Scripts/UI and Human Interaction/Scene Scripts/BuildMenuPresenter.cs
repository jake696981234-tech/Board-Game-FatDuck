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

    public event Action<Game.Core.Action, UIInfo> OnItemClicked;

    private readonly List<BuildMenuItemView> _pool = new();


    public void Show(IEnumerable<Game.Core.Action> rawItems, List<UIInfo> uiInfo, InteractionConfig config)
    {
        IEnumerable<Game.Core.Action> items;
        var UiInfo = new List<UIInfo>();
        if (config.GiveRawActionOffers)
        {
            items = rawItems;
            UiInfo = uiInfo;
        }
        else
        {
            items = filteredBuildOptions(rawItems, uiInfo, out uiInfo);
        }

        gameObject.SetActive(true);
        int i = 0;
        foreach (var item in items)
        {
            var view = Ensure(i++);
            view.Bind(item, uiInfo[i - 1], OnItemClicked);
            view.gameObject.SetActive(true);
        }
        for (; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
    }

    private IEnumerable<Game.Core.Action> filteredBuildOptions(IEnumerable<Game.Core.Action> items, List<UIInfo> uiInfo, out List<UIInfo> outUiInfo)
    {
        var filteredItems = new List<Game.Core.Action>();
        var filteredUiInfo = new List<UIInfo>();
        var iHaveAlreadySeenYou = new HashSet<byte>();

        int i = 0;
        foreach (var item in items)
        {
            if (iHaveAlreadySeenYou.Add(item.pieceType))
            {
                filteredItems.Add(item);
                i++;
                filteredUiInfo.Add(uiInfo[i]);

            }
        }
        outUiInfo = filteredUiInfo;
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