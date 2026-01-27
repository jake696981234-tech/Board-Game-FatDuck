// Assets/Scripts/Core/GameBootstrapper.cs
using UnityEngine;
using Game.Core;
using System.IO;
using System;

public sealed class GameBootstrapper : MonoBehaviour
{
    [Header("Authoring")]
    public PerGameConfig perGameConfig;
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private GameObject inspectGameController;
    // Live systems (optional to expose for debugging)
    // public BoardModel board;
    void Awake()
    {
        uiRoot.SetActive(Info.inspectGame);
        string csvPath = Path.Combine(Application.streamingAssetsPath, "pieces.csv");
        PiecesCsvImporter.Import(csvPath);
        Piece.SetActiveAbilitesEnabledFromType();
        // 3) GameRegistry
        GameRegistry.Init(Info.gamesToRun);
        for (int i = 0; i < Info.gamesToRun; i++)
        {
            GameObject newGameController = Instantiate(inspectGameController);
            var controller = newGameController.GetComponent<GameController>();
            GameRegistry.game[i].gameController = controller;
            controller.gameBootstrapper = this;
            controller.gameIndex = i;
            controller.perGameConfig = perGameConfig;
            if (Info.inspectGame && i == 0) controller.inspectGame = true;
        }
    }
}


