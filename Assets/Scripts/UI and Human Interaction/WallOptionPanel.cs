// Assets/Scripts/UI/HIC/ActionListPresenter.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public sealed class WallOptionPanel : MonoBehaviour
{
    [Header("Wiring")]
    public Transform WallContent;

    public GameObject WallOptionPanelObject;

    public WallOptionPrefab wallOptionPrefab;

    public Button[] buttons;
    private readonly List<WallOptionPrefab> wallConfigOption = new();

    public GameObject[] blockOption;

    private List<ushort> cachedwallOptions = new List<ushort>;

    public void ShowSideOptions(IEnumerable<ushort> wallOptions)
    {
        WallOptionPanelObject.SetActive(true);
        wallOptions = cachedwallOptions;
        for (int i = 0; i < blockOption.Length; i++) // auto-scale
        {
            bool existsWithThisCount = wallOptions.Any(w => PiecesSides.CountWalls(w) == i);

            // Active if at least one *doesn't* match
            blockOption[i].SetActive(!existsWithThisCount);
        }
    }


    public void showWallOptions(IEnumerable<ushort> wallOptions, int HowMany)
    {
        foreach (var wall in wallOptions)
        {
            if (HowMany == PiecesSides.CountWalls(wall))
            {
                var prefabRefrence = Instantiate(wallOptionPrefab, WallContent);
                wallConfigOption.Add(prefabRefrence);
                prefabRefrence.SeedData(wall);
            }
        }
    }


    public void whenButtonOneIsClicked()
    {
        showWallOptions(cachedwallOptions, 1);
        WallOptionPanelObject.SetActive(false);
    }
    public void whenButtonTwoIsClicked()
    {
        showWallOptions(cachedwallOptions, 2);
        WallOptionPanelObject.SetActive(false);
    }
    public void whenButtonThreeIsClicked()
    {
        showWallOptions(cachedwallOptions, 3);
        WallOptionPanelObject.SetActive(false);
    }
    public void whenButtonFourIsClicked()
    {
        showWallOptions(cachedwallOptions, 4);
        WallOptionPanelObject.SetActive(false);
    }
    public void whenButtonFiveIsClicked()
    {
        showWallOptions(cachedwallOptions, 5);
        WallOptionPanelObject.SetActive(false);
    }

}
