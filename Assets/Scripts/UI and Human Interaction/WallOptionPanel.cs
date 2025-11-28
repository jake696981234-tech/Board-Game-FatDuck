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

    public GameObject FirstWallOptionPanel;

    public GameObject WallCreatePanel2;

    public WallOptionPrefab wallOptionPrefab;

    public HumanInteractionController HumanController;

    public Button[] buttons;
    private readonly List<WallOptionPrefab> wallConfigOption = new();

    public GameObject[] blockOption;

    private List<ushort> cachedwallOptions = new();

    public void ShowSideOptions(IEnumerable<ushort> wallOptions)
    {
        WallCreatePanel2.SetActive(false);
        FirstWallOptionPanel.SetActive(true);


        DestoryAllWallOptions();
        wallConfigOption.Clear();

        cachedwallOptions = wallOptions.ToList();
        for (int i = 0; i < blockOption.Length; i++) // auto-scale
        {
            bool existsWithThisCount = wallOptions.Any(w => PiecesSides.CountWalls(w) == i);

            // Active if at least one *doesn't* match
            blockOption[i].SetActive(!existsWithThisCount);
        }
    }



    public void showWallOptions(IEnumerable<ushort> wallOptions, int howMany)
    {
        WallCreatePanel2.SetActive(true);
        DestoryAllWallOptions();       // assuming this is your own method
        wallConfigOption.Clear();

        

        // Decide what set of walls we're going to show
        IEnumerable<ushort> wallsToShow;

        if (HumanInteractionController.giveRawActionOffers)
        {
            // Raw: show all that match howMany
            wallsToShow = wallOptions.Where(w => howMany == PiecesSides.CountWalls(w));
        }
        else
        {
            // Filtered: unique options that match howMany
            var seen = new HashSet<ushort>();
            wallsToShow = wallOptions
                .Where(w => howMany == PiecesSides.CountWalls(w))
                .Where(w => seen.Add(w));   // only first time a value appears
        }

        // Now actually spawn UI for the chosen walls
        foreach (var wall in wallsToShow)
        {
            var prefabReference = Instantiate(wallOptionPrefab, WallContent);
            prefabReference.gameObject.SetActive(true);
            wallConfigOption.Add(prefabReference);
            prefabReference.SeedData(wall);
        }
    }


    public void DestoryAllWallOptions()
    {
        foreach (var obj in wallConfigOption)
        {
            if (obj != null)
                Destroy(obj.gameObject);
        }
        wallConfigOption.Clear();
    }

    public void whenButtonOneIsClicked()
    {
        showWallOptions(cachedwallOptions, 1);
        FirstWallOptionPanel.SetActive(false);
    }
    public void whenButtonTwoIsClicked()
    {
        showWallOptions(cachedwallOptions, 2);
        FirstWallOptionPanel.SetActive(false);
    }
    public void whenButtonThreeIsClicked()
    {
        showWallOptions(cachedwallOptions, 3);
        FirstWallOptionPanel.SetActive(false);
    }
    public void whenButtonFourIsClicked()
    {
        showWallOptions(cachedwallOptions, 4);
        FirstWallOptionPanel.SetActive(false);
    }
    public void whenButtonFiveIsClicked()
    {
        showWallOptions(cachedwallOptions, 5);
        FirstWallOptionPanel.SetActive(false);
    }

    public void whenBackIsClicked()
    {
        ShowSideOptions(cachedwallOptions);
    }

}
