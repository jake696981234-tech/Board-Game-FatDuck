// Assets/Scripts/Core/GameBootstrapper.cs
using UnityEngine;
using Game.Core;
using System.IO;
using System;

public sealed class GameBootstrapper : MonoBehaviour
{
    [Header("Authoring")]
    public Config config;     // assign in Inspector
    public PerGameConfig perGameConfig;
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private GameObject inspectGameController;
    // Live systems (optional to expose for debugging)
    // public BoardModel board;
    void Awake()
    {
        uiRoot.SetActive(config.inspectGame);


        string csvPath = Path.Combine(Application.streamingAssetsPath, "pieces.csv");
        PiecesCsvImporter.Import(csvPath);
        Piece.SetActiveAbilitesEnabledFromType();


        if (config == null) { Debug.LogError("Config asset not assigned."); return; }

        // 1) Freeze authoring into an immutable hub

        // 3) GameRegistry
        GameRegistry.Init(config.gamesToRun);



        for (int i = 0; i < config.gamesToRun; i++)
        {
            GameObject newGameController = Instantiate(inspectGameController);
            var controller = newGameController.GetComponent<GameController>();

            GameRegistry.game[i].gameController = controller;


            if (controller != null)
            {
                controller.gameBootstrapper = this;
                controller.gameIndex = i;
                if (controller.config == null)
                    controller.config = this.config;
            }
            if (perGameConfig != null)
            {
                if (controller.perGameConfig == null)
                    controller.perGameConfig = this.perGameConfig;
            }


            if (config.inspectGame && i == 0)
            {
                controller.inspectGame = true;
            }
        }

    }


}


