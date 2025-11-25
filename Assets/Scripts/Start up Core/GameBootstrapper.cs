// Assets/Scripts/Core/GameBootstrapper.cs
using UnityEngine;
using Game.Core;
using System.IO;
using System;

public sealed class GameBootstrapper : MonoBehaviour
{
    [Header("Authoring")]
    public Config config;     // assign in Inspector
    public Pieces pieces;     // your pieces registry asset / component
    public static Pieces PiecesData;
    public BoardViewController boardView;

    public PerGameConfig perGameConfig;

    [SerializeField] private GameObject uiRoot;


    [SerializeField] private GameObject inspectGameController;


    // Live systems (optional to expose for debugging)
    public BoardModel board;


    public GameConfigHub hub;
    public CostEngine cost;

    public OfferProvider offers;



    void Awake()
    {
        uiRoot.SetActive(config.inspectGame);

        string csvPath = Path.Combine(Application.streamingAssetsPath, "pieces.csv");
        PiecesData = PiecesCsvImporter.Import(csvPath);


        if (config == null) { Debug.LogError("Config asset not assigned."); return; }


        pieces = PiecesData;

        // 1) Freeze authoring into an immutable hub
        hub = config.BuildHub();

        // 3) Cost engine
        cost = new CostEngine(in hub);

        // 6) Shared offer provider
        offers = new OfferProvider();


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


