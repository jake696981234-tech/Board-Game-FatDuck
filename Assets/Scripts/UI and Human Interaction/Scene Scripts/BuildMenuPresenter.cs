// Assets/Scripts/UI/HIC/BuildMenuPresenter.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Action = Game.Core.Action;
using System.Linq;
using static Game.Core.ActionKind;

public sealed class BuildMenuPresenter : MonoBehaviour
{
    [Header("Wiring")]
    public Transform listContent;            // BuildPanel/BuildScroll/Viewport/Content
    public BuildMenuItemView itemPrefab;     // prefab with Button + Icon/Name/Cost

    public event Action<Game.Core.Action, UIInfo> OnItemClicked;

    public BuildMenuItemView[] BuildActionPrefabs;
    // public List<UIInfo> theUIInfo = new(Info.totalCells);

    // private static bool isWeirdAction(IEnumerable<Game.Core.Action> rawItems) //please rename me
    // {
    //     var item = rawItems.FirstOrDefault().kind;
    //     return item == Upgrade || item == GroupBuild;
    // }

    public void ShowBuildActionMenu((List<Action> BuildActions, List<UIInfo> uiInfo) data)
    {
        for (int i = 0; i < BuildActionPrefabs.Length; i++) if (BuildActionPrefabs[i] != null) Destroy(BuildActionPrefabs[i].gameObject);
        BuildActionPrefabs = new BuildMenuItemView[data.BuildActions.Count()];
        for (int i = 0; i < data.BuildActions.Count(); i++)
        {
            var prefab = Instantiate(itemPrefab, listContent);
            BuildActionPrefabs[i] = prefab;
            prefab.Bind(data.BuildActions[i], data.uiInfo[i], OnItemClicked);
            BuildActionPrefabs[i].gameObject.SetActive(true);
        }
    }

    // public void DestoryAllBuildActionPrefabs()
    // {
    //     foreach (var obj in BuildActionPrefabs) Destroy(obj.gameObject);
    // }

    // private IEnumerable<Action> filteredBuildOptions(IEnumerable<Game.Core.Action> items)
    // {
    //     var filteredItems = new List<Action>();
    //     var iHaveAlreadySeenYou = new HashSet<int>();

    //     int i = 0;
    //     foreach (var item in items)
    //     {
    //         if (iHaveAlreadySeenYou.Add(item.TargetType))
    //         {
    //             filteredItems.Add(item);
    //             i++;
    //         }
    //     }
    //     return filteredItems;
    // }



    public void Hide() => gameObject.SetActive(false);

    // private BuildMenuItemView Ensure(int index)
    // {
    //     while (BuildActionPrefabs.Count <= index)
    //     {
    //         var v = Instantiate(itemPrefab, listContent);
    //         BuildActionPrefabs.Add(v);
    //     }
    //     return BuildActionPrefabs[index];
    // }
}

public struct FilterForBuildItems
{
    bool onlyBuildings;

    bool faction1;
    bool faction2;
    bool faction3;
}