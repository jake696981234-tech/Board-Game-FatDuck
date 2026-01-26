// Assets/Scripts/UI/HIC/ActionListItemView.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WallOptionPrefab : MonoBehaviour
{
    public GameObject[] wallObjects;

    private ushort CachedWall;

    public Button button;

    public void WhenButtonPressed()
    {

        UIInput.OnWallConfigClicked(CachedWall);
    }

    public void SeedData(ushort wall)
    {
        CachedWall = wall;
        for (int i = 0; i < 6; i++)
        {
            wallObjects[i].SetActive(!PiecesSides.IsConnectorSide(wall, i));
        }
    }

}
