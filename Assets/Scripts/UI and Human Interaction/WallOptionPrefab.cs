// Assets/Scripts/UI/HIC/ActionListItemView.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WallOptionPrefab : MonoBehaviour
{
    public GameObject[] wallObjects;

    public Button button;

    public void SeedData(ushort wall)
    {
        for (int i = 0; i < 5; i++)
        {
            wallObjects[i].SetActive(!PiecesSides.IsConnectorSide(wall, i));
        }
    }

}
