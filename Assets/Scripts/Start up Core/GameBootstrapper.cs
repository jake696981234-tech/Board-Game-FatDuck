// Assets/Scripts/Core/GameBootstrapper.cs
using UnityEngine;
using Game.Core;
using System.IO;
using System;

public sealed class GameBootstrapper : MonoBehaviour
{
    [Header("Authoring")]
    public Config config;     // assign in Inspector
    public BoardViewController boardView;

    public PerGameConfig perGameConfig;

    [SerializeField] private GameObject uiRoot;


    [SerializeField] private GameObject inspectGameController;


    // Live systems (optional to expose for debugging)
    public BoardModel board;


    public GameConfigHub hub;
    public CostEngine cost;

    void Awake()
    {
        uiRoot.SetActive(config.inspectGame);

        string csvPath = Path.Combine(Application.streamingAssetsPath, "pieces.csv");
        PiecesCsvImporter.Import(csvPath);


        if (config == null) { Debug.LogError("Config asset not assigned."); return; }



        // 1) Freeze authoring into an immutable hub
        hub = config.BuildHub();

        // 3) Cost engine
        cost = new CostEngine(in hub);



        for (int i = 0; i < config.gamesToRun; i++)
        {
            GameObject newGameController = Instantiate(inspectGameController);
            var controller = newGameController.GetComponent<GameController>();

            if (controller != null)
            {
                controller.gameBootstrapper = this;
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
                if (controller.boardView == null)
                    controller.boardView = this.boardView;
            }
        }

    }


}


