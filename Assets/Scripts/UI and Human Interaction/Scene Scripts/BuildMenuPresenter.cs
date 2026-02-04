// Assets/Scripts/UI/HIC/BuildMenuPresenter.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using static Game.Core.ActionKind;

public sealed class BuildMenuPresenter : MonoBehaviour
{
    [Header("Wiring")]
    public Transform listContent;            // BuildPanel/BuildScroll/Viewport/Content
    public BuildMenuItemView itemPrefab;     // prefab with Button + Icon/Name/Cost

    public event Action<Game.Core.Action, UIInfo> OnItemClicked;

    public readonly List<BuildMenuItemView> _pool = new();
    public List<UIInfo> theUIInfo = new();

    // private static bool isWeirdAction(IEnumerable<Game.Core.Action> rawItems) //please rename me
    // {
    //     var item = rawItems.FirstOrDefault().kind;
    //     return item == Upgrade || item == GroupBuild;
    // }

    public void Show(Action[] BuildActions, UIInfo[] uiInfo)
    {
        for (int i = 0; i < BuildActions.Length; i++)
        {
            var view = Ensure(i);
            view.Bind(BuildActions[i], uiInfo[i], OnItemClicked);
            view.gameObject.SetActive(true);
            i++;
        }
        for (; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
        theUIInfo = UiInfo;
    }

    private IEnumerable<Game.Core.Action> filteredBuildOptions(IEnumerable<Game.Core.Action> items)
    {
        var filteredItems = new List<Game.Core.Action>();
        var iHaveAlreadySeenYou = new HashSet<int>();

        int i = 0;
        foreach (var item in items)
        {
            if (iHaveAlreadySeenYou.Add(item.TargetType))
            {
                filteredItems.Add(item);
                i++;
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