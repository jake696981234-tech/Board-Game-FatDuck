using UnityEngine;
using System.Collections.Generic;
using Game.Core; // for GameState
using System;
using UnityEngine.InputSystem;

public static class UIBridge
{

    public static GameState gameState;
    public static BoardModel bm;
    private static int gameIndex;
    public static bool IsCurrentPlayer = false;

    public static byte _humanPlayer;

    public static void Init(HumanInteractionController theHic, int theGameIndex, byte seat)
    {
        gameIndex = theGameIndex;
        gameState = GameRegistry.game[gameIndex].gameState;
        bm = GameRegistry.game[gameIndex].boardModel;
        var events = GameRegistry.game[gameIndex].eventManager;

        _humanPlayer = seat;
        IsCurrentPlayer = UIBridge.gameState.CurrentPlayerId == _humanPlayer;


        snapShotHistory.Clear();
        snapShotNumber = 0;
        _pendingSnapshots.Clear();
        _applyQueueRoutine = null;

        UI.Init(theHic, events);

        ShowLeftPanel.HudRefresh();
        showBoard.IndexCellViews();
    }



    #region Action in Point

    // Offers built from core (capacity big enough to hold a full turn's options)
    const int kCap = 10000;
    public static Game.Core.Action[] _offers = new Game.Core.Action[kCap];
    public static float[] _quoted = new float[kCap];
    public static byte[] _mask = new byte[kCap];
    public static int _total;   // total actions returned by provider (may exceed cap)
    public static int _count;   // displayed = min(total, cap)

    public static void RebuildOffersForCurrentPlayer()
    {
        _total = _count = 0;
        if (bm == null || gameState == null)
            return;

        // Build the query from live systems (readonly struct → must use constructor)
        var query = new OfferQuery(
            gameState.CurrentPlayerId,
            gameState.PieceLimitEnabled,
            gameState.pieceLimitPerPlayer,
            gameState.multiCreateActive,
            gameState.multiCreateType,
            gameState.multiCreateBorder,
            gameState.multiCreateRemaining,
            gameState.multiCreateCells.ToArray(),
            gameState.MultiCreateCellCount
        );

        OfferBuild offerBuild;
        offerBuild.query = query;
        offerBuild.outActions = _offers.AsSpan();
        offerBuild.outCosts = _quoted.AsSpan();
        offerBuild.outMask = _mask.AsSpan();
        offerBuild.gameIndex = gameIndex;
        offerBuild.write = 0;
        offerBuild.total = 0;
        offerBuild.cap = 0;

        // Fill the spans (zero-alloc path in OfferProvider). Function returns TOTAL (may exceed cap). :contentReference[oaicite:7]{index=7}
        _total = OfferProvider.BuildActionList(offerBuild);
        _count = Mathf.Min(kCap, _total);
    }

    #endregion
    #region Action Out Point
    public static void PerformActionIndex(Game.Core.Action theAction)
    {
        // Execute through reducer (single source of truth). This method fires OnActionExecuted afterwards. :contentReference[oaicite:9]{index=9}
        if (!gameState.Perform(in theAction, _offers))
        {
            Debug.LogWarning($"[HIC] Perform rejected: {UIHelpers.PrettyAction(theAction)}");
        }
        else
        {
            UI._lastActionLabel = UIHelpers.PrettyAction(theAction); // show in Debug HUD
        }
        // UI refresh happens via HandleActionExecuted()
    }
    #endregion
    #region Visual In Point

    public static readonly Queue<GameSnapshot> _pendingSnapshots = new();
    public static GameSnapshot _snapshot;
    public static Coroutine _applyQueueRoutine;

    public static void SnapShotUpdate()
    {
        if (!UI.hic.config.ManualStepThroughSnapShots) return;
        if (snapShotHistory.Count == 0) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.rightArrowKey.wasPressedThisFrame && snapShotNumber < snapShotHistory.Count - 1)
        {
            snapShotNumber++;
            Debug.Log($"snapShotNumber is: {snapShotNumber} / {snapShotHistory.Count - 1}");
            actuallyApplySnapshot();
        }

        if (kb.leftArrowKey.wasPressedThisFrame && snapShotNumber > 0)
        {
            snapShotNumber--;
            Debug.Log($"snapShotNumber is: {snapShotNumber} / {snapShotHistory.Count - 1}");
            actuallyApplySnapshot();
        }
    }


    public static void ApplySnapshot(GameSnapshot s)
    {
        snapShotHistory.Add(s);

        snapShotNumber = Mathf.Clamp(snapShotNumber, 0, snapShotHistory.Count - 1);

        if (!UI.hic.config.ManualStepThroughSnapShots || snapShotHistory.Count == 1 || (!IsCurrentPlayer && snapShotNumber < snapShotHistory.Count))
        {
            if (UI.hic.config.DelayOnActions && !IsCurrentPlayer)
            {
                UI.hic.EnqueueSnapshotForDelayedApply(s);
            }
            else
            {
                showBoard.ApplySnapshotData(s);
            }
        }
    }

    private static int snapShotNumber = 0;

    private static List<GameSnapshot> snapShotHistory = new List<GameSnapshot>();

    public static void actuallyApplySnapshot()
    {
        snapShotNumber = Mathf.Clamp(snapShotNumber, 0, snapShotHistory.Count - 1);

        if (!UI.hic.config.ManualStepThroughSnapShots)
        {
            snapShotNumber = snapShotHistory.Count - 1;
            showBoard.ApplySnapshotData(snapShotHistory[snapShotNumber]);
        }
        else
        {
            showBoard.ApplySnapshotData(snapShotHistory[snapShotNumber]);
        }
    }

    #endregion
}
