using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Game.Core; // for GameState
using System.Linq;

public static class UI
{
    public enum Mode { Build, Create, PieceAction, ActionExecute, WallOptionsSecoundPanel, MultiInputAction }

    static List<int> _targetsBuffer = new List<int>(128);

    public static GameState gameState;
    public static BoardModel bm;
    private static HumanInteractionController hic;
    private static EventManager events;
    public static GameSnapshot snapshot;
    private static EventManager eventManager;
    public static bool giveRawActionOffers;
    public static bool IsCurrentPlayer = false;
    private static int gameIndex;

    public static void ManualAwake(
    HumanInteractionController theHic, int theGameIndex)
    {
        gameIndex = theGameIndex;
        var gameState = GameRegistry.game[gameIndex].gameState;
        var bm = GameRegistry.game[gameIndex].boardModel;
        var events = GameRegistry.game[gameIndex].eventManager;
        hic = theHic;

        subscribeMe();
        if (hic.boardView) hic.boardView.CellClicked += OnCellClicked;   // from your BoardViewController
        if (hic.endTurnButton) hic.endTurnButton.onClick.AddListener(OnEndTurnClicked);

        // Ensure player row button array is allocated (supports up to 4 seats by design)
        if (hic.PlayerRow_Button == null || hic.PlayerRow_Button.Length < 4)
        {
            hic.PlayerRow_Button = new Button[4];
        }

        HowManyWallSelected += EnterCreateModeWithWallChosen;
        giveRawActionOffers = hic.config.GiveRawActionOffers;

        ChangetoSecondPanelMode += SetWallOptionsSecondPanelMode;

        IsCurrentPlayer = gameState.CurrentPlayerId == _humanPlayer;
    }

    public static void ManualEnable()
    {
        if (gameState != null) gameState.OnActionExecuted += HandleActionExecuted; // refresh on every mutation
        RebuildOffersForCurrentPlayer();
        EnterBuildMode(); // will push menus from offers
        HookPresenters();
        HudRefresh();
    }

    private static Mode _mode = Mode.Build;

    [SerializeField, Range(0, 3)] private static byte _humanPlayer = 0; // bound by bootstrapper
    private static int? _selectedPieceId;
    private static int? _selectedCellId;

    private static int? _firstCellSelected;
    private static ActionItem? _selectedAction;
    private static int _selectedActionIndex = -1;

    // Create flow
    private static byte _selectedCreateType = 0;   // which piece type we're trying to create
    private static bool _createArmed = false;      // in Create mode and seeded

    // Offers built from core (capacity big enough to hold a full turn's options)
    const int kCap = 1500;
    private static Game.Core.Action[] _offers = new Game.Core.Action[kCap];
    private static float[] _quoted = new float[kCap];
    private static byte[] _mask = new byte[kCap];
    private static int _total;   // total actions returned by provider (may exceed cap)
    private static int _count;   // displayed = min(total, cap)

    private static ushort? cachedChosenWall;

    private static BuildItem CachedBuildItemForCreateConnector;

    private static string _lastActionLabel = string.Empty; // for Debug HUD


    private static void OnDisable()
    {
        if (gameState != null) gameState.OnActionExecuted -= HandleActionExecuted;
        UnhookPresenters();
    }


    public static void SetWallOptionsSecondPanelMode()
    {

        _mode = Mode.WallOptionsSecoundPanel;
    }

    private static void enterWallOptionsSecondPanelMode()
    {
        TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: false, walls: true);
        hic.boardView.ClearHighlights();
        _mode = Mode.Create;
    }

    private static void Update()
    {
        hic.boardView.SnapShotUpdate();
        // Global cancel (New Input System)
        // Esc key OR Right Mouse Button pressed this frame
        bool cancel =
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
           (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);

        if (cancel)
        {
            switch (_mode)
            {
                case Mode.WallOptionsSecoundPanel: enterWallOptionsSecondPanelMode(); break; //this is dumb lazy code i added
                case Mode.Create: EnterBuildMode(); break;
                case Mode.PieceAction: EnterBuildMode(); break;
                case Mode.ActionExecute: EnterPieceActionMode(); break;
                case Mode.MultiInputAction: EnterPieceActionMode(); break;
            }
        }
    }

    // ===================== Mode transitions =====================
    #region Mode Transitions
    private static void EnterBuildMode()
    {
        hic.boardView.ClearHighlights();
        if (hic.config && hic.boardView) hic.boardView.ApplyDefaultCellColor(hic.config.defaultCellColor);
        _mode = Mode.Build;
        _selectedPieceId = null;
        _selectedAction = null;

        SetBackdropColor(hic.config ? hic.config.buildModeBackground : new Color(0, 0, 0, 0.8f));
        SetPanelBackdropColor(hic.config ? hic.config.buildModePanelBackground : new Color(0, 0, 0, 0.8f));

        TogglePanels(build: true, create: false, action: true, pieceFull: false, execute: false, walls: false);
        PushBuildMenu();
        PushNonPieceActionList();   // NEW: default action list = non-piece actions (e.g., End Turn)

        HudRefresh();
    }

    private static void EnterCreateMode(BuildItem build, ushort? ChosenWall = null)
    {
        hic.boardView.ClearHighlights();
        if (hic.config && hic.boardView) hic.boardView.ApplyDefaultCellColor(hic.config.defaultCellColor);
        // Seed the create family (piece type) and highlight all legal cells for that type.
        _selectedCreateType = build.pieceType;
        _selectedActionIndex = FindFirstCreateIndexForType(_selectedCreateType); // seed (can be -1 if none)
        _createArmed = (_selectedActionIndex >= 0);

        if (ChosenWall == null)
        {
            hic.boardView.HighlightCells(
                ComputeCreateTargetsForPieceType(_selectedCreateType),
                hic.config ? hic.config.createModeCellHighlight : new Color(0.25f, 0.75f, 0.25f, 0.6f)
            );

            cachedChosenWall = null;
        }
        else
        {
            hic.boardView.HighlightCells(
                ComputeCreateTargetsForPieceType(_selectedCreateType, ChosenWall),
                hic.config ? hic.config.createModeCellHighlight : new Color(0.25f, 0.75f, 0.25f, 0.6f)
            );

            cachedChosenWall = ChosenWall;
        }


        _mode = Mode.Create;
        _selectedPieceId = null;
        _selectedCellId = null;
        _selectedAction = null;

        SetBackdropColor(hic.config ? hic.config.createModeBackground : new Color(0, 0, 0, 0.8f));
        SetPanelBackdropColor(hic.config ? hic.config.createModePanelBackground : new Color(0, 0, 0, 0.8f));
        TogglePanels(build: false, create: true, action: false, pieceFull: false, execute: false, walls: false);

        if (hic.createTitleText) hic.createTitleText.text = $"Create: {build.name}";
        if (hic.createCostText) hic.createCostText.text = $"Cost: {build.cost}";
        if (hic.createSprite)
        {
            var s = !string.IsNullOrEmpty(build.spritePath) ? Resources.Load<Sprite>(build.spritePath) : null;
            hic.createSprite.sprite = s;
            hic.createSprite.enabled = (s != null);
        }

        // NOTE: We haven't added highlight APIs to BoardViewController yet, so no highlight calls here.
        HudRefresh();
    }

    public static void EnterConnectingMode(BuildItem item)
    {
        // Ensure we query offers for the piece type the user just picked

        _mode = Mode.Create;
        _selectedCreateType = item.pieceType;
        _selectedActionIndex = FindFirstCreateIndexForType(_selectedCreateType);
        var wallOptions = WallOptionsForPieceType(_selectedCreateType);

        TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: false, walls: true);

        hic.wallOptionPanel.ShowSideOptions(wallOptions);
    }

    private static void EnterPieceActionMode(int? pieceId = null)
    {
        hic.boardView.ClearHighlights();
        if (hic.config && hic.boardView) hic.boardView.ApplyDefaultCellColor(hic.config.defaultCellColor);
        _mode = Mode.PieceAction;
        _selectedAction = null;
        if (pieceId.HasValue) _selectedPieceId = pieceId;

        SetBackdropColor(hic.config ? hic.config.pieceActionBackground : new Color(0, 0, 0, 0.8f));
        SetPanelBackdropColor(hic.config ? hic.config.pieceActionPanelBackground : new Color(0, 0, 0, 0.8f));

        TogglePanels(build: false, create: false, action: false, pieceFull: true, execute: false, walls: false);
        // Immediately fill the full panel so it shows on the first click:
        PushPieceActionListForSelection();

        HudRefresh();
    }

    private static void EnterActionExecuteMode(ActionItem action)
    {
        hic.boardView.ClearHighlights();
        if (action.kind == Game.Core.ActionKind.Launcher && _mode != Mode.MultiInputAction)
        {
            _mode = Mode.MultiInputAction;
        }
        else
        {
            _mode = Mode.ActionExecute;
        }

        _selectedAction = action;

        SetBackdropColor(hic.config ? hic.config.actionExecuteBackground : new Color(0, 0, 0, 0.8f));
        SetPanelBackdropColor(hic.config ? hic.config.actionExecutePanelBackground : new Color(0, 0, 0, 0.8f));

        TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: true, walls: false);
        // (Re)apply legal-target highlights for clarity while in execute mode
        if (_selectedActionIndex >= 0)
        {
            var col = hic.config ? hic.config.actionLegalTargetHighlight : new Color(0.6f, 0.35f, 0.9f, 0.65f);
            hic.boardView.ClearHighlights();
            hic.boardView.HighlightCells(ComputeTargetsForActionIndex(_selectedActionIndex), col);
        }

        if (hic.actionTitleText) hic.actionTitleText.text = action.name;
        if (hic.actionPieceText) hic.actionPieceText.text = _selectedPieceId.HasValue ? $"Piece #{_selectedPieceId.Value}" : "Piece (none)";
        if (hic.actionCostText) hic.actionCostText.text = $"Cost: {action.cost}";

        HudRefresh();
    }

    #endregion

    // ===================== Input (from board) =====================
    #region input from Board
    private static void OnCellClicked(int cellId)
    {
        if (hic.config && hic.config.blockInputWhenNotYourTurn && gameState != null)
        {
            // gate input while not human's turn (wire up your player id if needed)
            if (gameState.CurrentPlayerId != _humanPlayer) return;
        }

        switch (_mode)
        {
            case Mode.Build:
                _firstCellSelected = null;
                _selectedCellId = cellId;
                // Selection/hover feedback: show selected cell using config colour
                if (hic.config && hic.boardView)
                {
                    hic.boardView.ClearHighlights();
                    hic.boardView.HighlightSelection(cellId, hic.config.selectionHighlight);
                }
                // Only enter piece mode if the cell has any piece-driven actions.
                if (HasPieceActionsForCell(cellId))
                {
                    EnterPieceActionMode();  // switches right column to full panel
                    // NOTE: EnterPieceActionMode() calls PushPieceActionListForSelection(),
                    // so the list is visible on the very first click.
                }
                // else: stay on default Build + Non-piece Action layout
                break;

            case Mode.Create:
            case Mode.WallOptionsSecoundPanel:
                _firstCellSelected = null;
                // Click on a highlighted target → perform the concrete Create action
                if (_createArmed)
                {
                    if (cachedChosenWall == null)
                    {
                        int idx = FindConcreteCreateAction(_selectedCreateType, (ushort)cellId);
                        if (idx >= 0) PerformActionIndex(idx);
                    }
                    else
                    {
                        int idx = FindConcreteCreateAction(_selectedCreateType, (ushort)cellId, cachedChosenWall);
                        if (idx >= 0) PerformActionIndex(idx);
                    }
                }
                // Clear either way and return to Build
                hic.boardView.ClearHighlights();
                EnterBuildMode();
                break;

            case Mode.MultiInputAction:
                if (FindIfLegalForMultiAction(cellId))
                {
                    _firstCellSelected = cellId;
                    ActionItem item = _selectedAction ?? throw new Exception("Null!");
                    EnterActionExecuteMode(item);
                }
                break;

            case Mode.PieceAction:
                _firstCellSelected = null;
                _selectedCellId = cellId;
                PushPieceActionListForSelection();
                break;

            case Mode.ActionExecute:
                // We’re in targeting: the user clicked a highlighted cell → find the concrete action
                if (_selectedActionIndex >= 0)
                {
                    var seed = _offers[_selectedActionIndex];
                    int idx = FindConcreteAction(seed.kind, seed.srcCell, seed.pieceType, (ushort)cellId);
                    if (idx >= 0) { PerformActionIndex(idx); }
                    // Clear highlights regardless (perform may change board)
                    hic.boardView.ClearHighlights();
                    EnterBuildMode();
                }
                break;
        }
    }

    private static bool FindIfLegalForMultiAction(int cellid)
    {
        for (int i = 0; i < _count; i++)
        {
            var a = _offers[i];
            if (a.kind != Game.Core.ActionKind.Launcher) continue;
            if (_mask[i] == 0) continue; // skip masked/illegal offers

            int pieceId = a.aux;
            if (pieceId < 0 || pieceId >= bm.pieceCellId.Length) continue;
            if (cellid != bm.pieceCellId[pieceId]) continue;
            return true;
        }
        return false;
    }

    private static int FindConcreteAction(byte kind, ushort src, byte type, ushort dst)
    {
        for (int i = 0; i < _count; i++)
        {
            var a = _offers[i];

            if (_firstCellSelected != null)
            {
                if (_firstCellSelected != bm.pieceCellId[a.aux]) continue;
            }

            if (a.kind != kind) continue;
            if (a.srcCell != src) continue;
            if (a.pieceType != type) continue;
            if (a.dstCell != dst) continue;
            if (_mask[i] == 0) continue;
            return i;
        }
        return -1;
    }

    #endregion

    // ===================== Buttons =====================

    #region Out Point To core
    private static void PerformActionIndex(int idx)
    {
        if (gameState == null) return;
        var a = _offers[idx];
        // Execute through reducer (single source of truth). This method fires OnActionExecuted afterwards. :contentReference[oaicite:9]{index=9}
        if (!gameState.Perform(in a))
        {
            Debug.LogWarning($"[HIC] Perform rejected: {PrettyAction(a)}");
        }
        else
        {
            _lastActionLabel = PrettyAction(a); // show in Debug HUD
        }
        // UI refresh happens via HandleActionExecuted()
    }
    #endregion

    #region Buttons
    private static void OnEndTurnClicked()
    {
        // find EndTurn in the current offers and perform it
        int idx = FindEndTurnIndex();
        if (idx >= 0) PerformActionIndex(idx);
    }

    private static int FindEndTurnIndex()
    {
        for (int i = 0; i < _count; i++) if (_offers[i].kind == Game.Core.ActionKind.EndTurn) return i;
        return -1;
    }

    #endregion
    // ===================== UI helpers =====================

    #region UI Helpers
    private static void TogglePanels(bool build, bool create, bool action, bool pieceFull, bool execute, bool walls)
    {
        if (hic.buildMenu) hic.buildMenu.gameObject.SetActive(build);
        if (hic.createPanel) hic.createPanel.gameObject.SetActive(create);
        if (hic.actionPanel) hic.actionPanel.gameObject.SetActive(action);
        if (hic.pieceActionPanelFull) hic.pieceActionPanelFull.gameObject.SetActive(pieceFull);
        if (hic.actionExecutePanel) hic.actionExecutePanel.gameObject.SetActive(execute);
        if (hic.WallOptionPanelObject) hic.WallOptionPanelObject.gameObject.SetActive(walls);
    }

    public enum LeftPanelsModes { DefaultPanel, EndRoundTotalPanel, EndRoundTotalPanel2 }
    public static LeftPanelsModes leftPanelMode = LeftPanelsModes.DefaultPanel;
    private static void ToggleLeftPanels(bool EndRoundTotalPanel1, bool EndRoundTotalPanel2)
    {
        hic.Payout_Panel1.SetActive(EndRoundTotalPanel1);
        hic.Payout_Panel2.SetActive(EndRoundTotalPanel2);
    }



    private static void SetBackdropColor(Color c)
    {
        if (hic.backdrop) hic.backdrop.color = c;
    }

    private static void SetPanelBackdropColor(Color c)
    {
        if (hic.panelBackDrop) hic.panelBackDrop.color = c;
    }

    // Legacy small HUD fields (kept) + new consolidated HUD refresh
    private static void UpdateHud_LegacySmall()
    {
        if (hic.budgetText && gameState != null)
            hic.budgetText.text = $"Budget: {gameState.GetBudget(_humanPlayer):0}";
        if (hic.blockInputOverlay && gameState != null && hic.config != null && hic.config.blockInputWhenNotYourTurn)
            hic.blockInputOverlay.SetActive(gameState.CurrentPlayerId != _humanPlayer);
    }

    private static void HudRefresh()
    {
        UpdateHud_LegacySmall();
        if (gameState == null) return;

        // --- Match header ---
        if (hic.Header_TurnOwnerText) hic.Header_TurnOwnerText.text = $"Player {gameState.CurrentPlayerId}";
        if (hic.Header_ModeText) hic.Header_ModeText.text = _mode.ToString();

        // --- Personal stats (your seat) ---
        if (hic.Personal_BudgetText) hic.Personal_BudgetText.text = "Budget: " + $"{Mathf.RoundToInt(gameState.GetBudget(_humanPlayer))}";
        if (hic.Personal_VPText) hic.Personal_VPText.text = "VP: " + $"{gameState.GetVP(_humanPlayer)}";
        if (hic.Personal_CoreHPText) hic.Personal_CoreHPText.text = "Core Hp: " + $"{gameState.GetCoreHealth(_humanPlayer)}";
        // Tint swatch optional; if you have a palette somewhere you can assign it here.

        // --- All players list ---
        if (hic.AllPlayers_ListRoot && hic.PlayerRowPrefab)
        {
            // Clear old rows
            for (int i = hic.AllPlayers_ListRoot.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(hic.AllPlayers_ListRoot.GetChild(i).gameObject);

            // Show current player first, then others in seat order
            Span<int> order = stackalloc int[4] { gameState.CurrentPlayerId, (gameState.CurrentPlayerId + 1) & 3, (gameState.CurrentPlayerId + 2) & 3, (gameState.CurrentPlayerId + 3) & 3 };
            for (int k = 0; k < 4; k++)
            {
                int p = order[k];
                var go = UnityEngine.Object.Instantiate(hic.PlayerRowPrefab, hic.AllPlayers_ListRoot);
                BindPlayerRow(go.transform as RectTransform, p);
            }
        }

        // --- Debug box ---
        if (hic.Debug_OffersText)
        {
            int masked = 0; for (int i = 0; i < _count; i++) if (_mask[i] == 0) masked++;
            hic.Debug_OffersText.text = $"Shown: {_count}  /  Total: {_total}  (Masked: {masked})";
        }
        if (hic.Debug_LastActionText) hic.Debug_LastActionText.text = string.IsNullOrEmpty(_lastActionLabel) ? "—" : _lastActionLabel;
        if (hic.Debug_SnapshotText)
        {
            // We don't hold a snapshot here; show some quick match counters instead.
            hic.Debug_SnapshotText.text = $"CenterVP={gameState.GetCenterVP()}  RoundsLeft={gameState.RoundsLeft}";
        }
    }
    private static void BindPlayerRow(RectTransform row, int playerId)
    {
        if (!row) return;
        if (hic.PlayerRow_Button == null || hic.PlayerRow_Button.Length <= playerId)
        {
            hic.PlayerRow_Button = new Button[4];
        }
        // Find children by the agreed names
        var nameText = row.Find("PlayerRow_NameText")?.GetComponent<TMP_Text>();
        var tintImg = row.Find("PlayerRow_TintSwatch")?.GetComponent<Image>();
        var budgetText = row.Find("PlayerRow_BudgetText")?.GetComponent<TMP_Text>();
        var vpText = row.Find("PlayerRow_VPText")?.GetComponent<TMP_Text>();
        var hpText = row.Find("PlayerRow_CoreHPText")?.GetComponent<TMP_Text>();
        var turn = row.Find("PlayerRow_TurnText")?.GetComponent<TMP_Text>();
        var action = row.Find("PlayerRow_ActionText")?.GetComponent<TMP_Text>();
        var passed = row.Find("PlayerRow_PassedText")?.GetComponent<TMP_Text>();
        hic.PlayerRow_Button[playerId] = row.Find("PlayerRow_Button")?.GetComponent<Button>();

        if (nameText) nameText.text = (playerId == gameState.CurrentPlayerId) ? $">Player {playerId}" : $"Player {playerId}";
        if (budgetText) budgetText.text = $"{Mathf.RoundToInt(gameState.GetBudget((byte)playerId))}";
        if (vpText) vpText.text = $"{gameState.GetVP((byte)playerId)}";
        if (hpText) hpText.text = $"{gameState.GetCoreHealth((byte)playerId)}";
        if (turn) turn.text = $"{playerTurn[playerId]}";
        if (action) action.text = $"{playerAction[playerId]}";
        if (passed) passed.text = $"{gameState.PassedTurn(playerId)}";

        hic.PlayerRow_Button[playerId].onClick.AddListener(() => showPlayerEndRoundTotals(playerId));



        // Optional tint swatch: if you have a palette elsewhere, assign it here (left blank by default)
        if (tintImg) tintImg.enabled = false;
    }

    public enum EndRoundTotalsPlayer
    {
        playerOne = 0,
        PlayerTwo = 1,
        PlayerThree = 2,
        PlayerFour = 3
    }
    public static EndRoundTotalsPlayer endRoundTotalsPlayer;
    private static void showPlayerEndRoundTotals(int playerId)
    {
        ToggleLeftPanels(leftPanelMode == LeftPanelsModes.DefaultPanel, false);

        if (leftPanelMode == LeftPanelsModes.DefaultPanel)
        {
            leftPanelMode = LeftPanelsModes.EndRoundTotalPanel;

            endRoundTotalsPlayer = (EndRoundTotalsPlayer)playerId;
        }
        else
        {
            leftPanelMode = LeftPanelsModes.DefaultPanel;
        }


        updatePlayerEndRoundTotals(playerId);

    }


    public static void updatePlayerEndRoundTotals(int playerId)
    {
        float TotalFactory = snapshot.PerEndRoundPayOut[playerId].PieceTypePayOut.Sum();

        hic.TotalPayoutText.text = $"{TotalFactory + snapshot.PerEndRoundPayOut[playerId].BonusForVP + snapshot.PerEndRoundPayOut[playerId].BonusForCoreDamage}";
        hic.VpBonusText.text = $"{snapshot.PerEndRoundPayOut[playerId].BonusForVP}";
        hic.CoreBonusText.text = $"{snapshot.PerEndRoundPayOut[playerId].BonusForCoreDamage}";
        hic.TotalFactoryTotalText.text = $"{TotalFactory}";

        if (leftPanelMode == LeftPanelsModes.EndRoundTotalPanel2) updatePerTypeEndRoundTotals();
    }

    private static readonly List<GameObject> spawnedPerTypeFactoryPayOutPrefab = new List<GameObject>();
    public static void ShowFactoryBonusByPieceTypePrefabs()
    {
        ToggleLeftPanels(true, leftPanelMode == LeftPanelsModes.EndRoundTotalPanel);

        if (leftPanelMode == LeftPanelsModes.EndRoundTotalPanel)
        {
            leftPanelMode = LeftPanelsModes.EndRoundTotalPanel2;
        }
        else
        {
            leftPanelMode = LeftPanelsModes.EndRoundTotalPanel;
        }


        updatePerTypeEndRoundTotals();
    }

    public static void updatePerTypeEndRoundTotals()
    {
        for (int c = 0; c < spawnedPerTypeFactoryPayOutPrefab.Count; c++)
        {
            if (spawnedPerTypeFactoryPayOutPrefab[c] != null)
            {
                UnityEngine.Object.Destroy(spawnedPerTypeFactoryPayOutPrefab[c]);
            }
        }
        spawnedPerTypeFactoryPayOutPrefab.Clear();

        var payout = snapshot.PerEndRoundPayOut[(int)endRoundTotalsPlayer];
        if (payout.pieceType == null || payout.PieceTypePayOut == null) return;
        int count = Math.Min(payout.pieceType.Length, payout.PieceTypePayOut.Length);
        if (count <= 0) return;

        for (int i = 0; i < payout.pieceType.Length; i++)
        {
            int type = payout.pieceType[i];
            var prefab = UnityEngine.Object.Instantiate(hic.PerTypeFactoryPayOutPrefab, hic.PerTypeFactoryPayOutRoot);
            prefab.SetActive(true);

            var prefabScript = prefab.GetComponent<FactoryPerTypePayOut>();
            prefabScript.SetValues(
                Pieces.displayNameByType[type],
                payout.PieceTypePayOut[i]
            );
            spawnedPerTypeFactoryPayOutPrefab.Add(prefab);
        }
    }

    // Called by bootstrapper
    public static void SetHumanSeat(byte seat)
    {
        _humanPlayer = seat;
        HudRefresh();
    }

    private static void HookPresenters()
    {
        if (hic.buildMenu) hic.buildMenu.OnItemClicked += OnBuildItemClicked;
        if (hic.nonPieceActionList) hic.nonPieceActionList.OnItemClicked += OnActionItemClicked;
        if (hic.pieceActionListFull) hic.pieceActionListFull.OnItemClicked += OnActionItemClicked;
        if (hic.SeePerPieceTypeTotalsButton) hic.SeePerPieceTypeTotalsButton.onClick.AddListener(() => ShowFactoryBonusByPieceTypePrefabs());
    }

    private static void UnhookPresenters()
    {
        if (hic.buildMenu) hic.buildMenu.OnItemClicked -= OnBuildItemClicked;
        if (hic.nonPieceActionList) hic.nonPieceActionList.OnItemClicked -= OnActionItemClicked;
        if (hic.pieceActionListFull) hic.pieceActionListFull.OnItemClicked -= OnActionItemClicked;
    }

    private static void OnBuildItemClicked(BuildItem item)
    {
        if (Pieces.HasConnectors(item.pieceType))
        {
            CachedBuildItemForCreateConnector = item;
            EnterConnectingMode(item);
        }
        else
        {
            EnterCreateMode(item);
        }
    }

    private static void OnActionItemClicked(ActionItem item)
    {
        if (!int.TryParse(item.id, out var idx)) return;
        if (idx < 0 || idx >= _count) return;

        // If this is a NON-piece action (e.g., End Turn), perform immediately.
        // NOTE: If your constant lives at Action.Kind.EndTurn, swap the symbol accordingly.

        if (_offers[idx].kind == Game.Core.ActionKind.EndTurn)
        {
            PerformActionIndex(idx);
            hic.boardView.ClearHighlights();
            EnterBuildMode(); // stay on default layout after executing a non-piece action
            return;
        }

        // Piece-derived action: highlight all legal target cells right away
        var col = (hic.config ? hic.config.actionLegalTargetHighlight : new Color(0.6f, 0.35f, 0.9f, 0.65f));
        _selectedAction = item;
        _selectedActionIndex = idx; // seed used for target resolution
        hic.boardView.ClearHighlights();
        hic.boardView.HighlightCells(ComputeTargetsForActionIndex(_selectedActionIndex), col);
        EnterActionExecuteMode(item);
    }

    // Buffer for create targets (re-use across calls)
    readonly static List<int> _createTargetsBuffer = new List<int>(128);


    // Collect all legal dst cells for "Create <pieceType>"
    private static IEnumerable<int> ComputeCreateTargetsForPieceType(byte pieceType, ushort? chosenWall = null)
    {
        _createTargetsBuffer.Clear();
        for (int i = 0; i < _count; i++)
        {
            var a = _offers[i];
            if (a.kind != Game.Core.ActionKind.Create) continue;   // byte code

            if (chosenWall != null)
            {
                if (a.aux != chosenWall) continue;
            }

            if (a.pieceType != pieceType) continue;
            if (_mask[i] == 0) continue; // masked out = illegal/unaffordable
            _createTargetsBuffer.Add(a.dstCell);
        }
        return _createTargetsBuffer;
    }

    readonly static List<ushort> wallConfigs = new List<ushort>(64);
    private static IEnumerable<ushort> WallOptionsForPieceType(byte pieceType)
    {
        wallConfigs.Clear();
        for (int i = 0; i < _count; i++)
        {
            var offer = _offers[i];
            if (offer.kind != ActionKind.Create) continue;   // byte code
            if (offer.pieceType != pieceType) continue;
            if (_mask[i] == 0) continue; // masked out = illegal/unaffordable
            wallConfigs.Add(offer.aux);
        }
        return wallConfigs;
    }



    #endregion
    // ===================== Offer plumbing (real) =====================
    #region Offer Plumbing
    private static void RebuildOffersForCurrentPlayer()
    {
        _total = _count = 0;
        if (bm == null || gameState == null)
            return;

        // Build the query from live systems (readonly struct → must use constructor)
        var q = new OfferQuery(
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

        // Fill the spans (zero-alloc path in OfferProvider). Function returns TOTAL (may exceed cap). :contentReference[oaicite:7]{index=7}
        _total = OfferProvider.BuildActionList(in q, _offers.AsSpan(), _quoted.AsSpan(), _mask.AsSpan(), gameIndex, gameState.CurrentPlayerId);
        _count = Mathf.Min(kCap, _total);
    }

    private static void HandleActionExecuted()
    {
        // World changed; rebuild and refresh UI
        RebuildOffersForCurrentPlayer();
        if (_mode == Mode.PieceAction)
        {
            PushPieceActionListForSelection();
        }
        else
        {
            PushBuildMenu();
            PushNonPieceActionList();
        }
        HudRefresh();
    }

    // Pretty label for an action (for list rows)
    private static string PrettyAction(Game.Core.Action a)
    {
        switch (a.kind)
        {
            case Game.Core.ActionKind.Move: return $"Move {a.srcCell} → {a.dstCell}";
            case Game.Core.ActionKind.Shoot: return $"Shoot {a.srcCell} → {a.dstCell}";
            case Game.Core.ActionKind.Create: return $"Create {a.pieceType} @ {a.dstCell}";
            case Game.Core.ActionKind.CaptureVP: return $"Capture VP @ {a.dstCell}";
            case Game.Core.ActionKind.CoreDamage: return $"Core Damage @ {a.dstCell}";
            case Game.Core.ActionKind.Push: return $"Push target @ {a.dstCell}";
            case Game.Core.ActionKind.GroupBuild: return $"Group Build {a.pieceType} @ {a.dstCell}";
            case Game.Core.ActionKind.Upgrade: return $"Upgrade → {a.pieceType} @ {a.dstCell}";
            case Game.Core.ActionKind.Launcher: return $"Launch {a.aux} → {a.dstCell}";
            case Game.Core.ActionKind.Spawner: return $"Spawn x? {a.pieceType} @ {a.dstCell}";
            case Game.Core.ActionKind.SacrificeFactory: return $"Sacrifice Factory {a.srcCell} → {a.dstCell}";
            case Game.Core.ActionKind.ConversionFactory: return $"ConversionFactory";
            case Game.Core.ActionKind.EndTurn: return "End Turn";
            default: return $"{a.kind} [{a.srcCell}->{a.dstCell}]";
        }
    }


    private static void PushBuildMenu()
    {
        // Build list shows all Create* actions this turn (grouping/labeling by piece type + quoted cost)
        var items = new List<BuildItem>(_count);
        if (_count > 0)
        {
            var names = Pieces.displayNameByType;     // assumed from your Pieces registry
            var paths = Pieces.spritePathByType;      // assumed from your Pieces registry

            for (int i = 0; i < _count; i++)
            {
                var a = _offers[i];
                if (a.kind != Game.Core.ActionKind.Create) continue;
                byte t = a.pieceType;
                string name = (t < names.Length) ? names[t] : $"Type {t}";
                string path = (t < paths.Length) ? paths[t] : null;
                int cost = Mathf.RoundToInt(_quoted[i]);
                bool legal = _mask[i] != 0;           // 1 = affordable+legal; 0 = masked out by cost, etc. :contentReference[oaicite:8]{index=8}

                //Adding the Connector Field
                ushort auxiliary = a.aux;

                items.Add(new BuildItem(t, name, path, cost, legal, auxiliary));
            }
        }
        hic.buildMenu.Show(items, hic.config);
    }




    // Default layout (right-side 40%): Non-piece actions only (e.g., End Turn)
    private static void PushNonPieceActionList()
    {
        var items = new List<ActionItem>();
        for (int i = 0; i < _count; i++)
        {
            var a = _offers[i];
            if (a.kind != Game.Core.ActionKind.EndTurn) continue; // future: add more non-piece kinds here
            int cost = Mathf.RoundToInt(_quoted[i]);
            bool legal = _mask[i] != 0;
            int kind = a.kind;
            items.Add(new ActionItem(i.ToString(), PrettyAction(a), cost, legal, Array.Empty<int>(), kind));
        }
        hic.nonPieceActionList.Show(items);
    }


    // PieceAction mode (full coverage panel): Only actions originating at the selected cell


    private static void PushPieceActionListForSelection()
    {
        var items = new List<ActionItem>();
        bool moveAddedForCell = false;
        if (_selectedCellId.HasValue)
        {
            int cell = _selectedCellId.Value;
            for (int i = 0; i < _count; i++)
            {
                var a = _offers[i];
                if (a.kind == Game.Core.ActionKind.EndTurn) continue; // exclude non-piece actions
                if (a.srcCell != (ushort)cell) continue;               // only actions from this piece

                // Show only one Move per selected piece unless raw offers requested
                if (a.kind == Game.Core.ActionKind.Move && !hic.config.GiveRawActionOffers)
                {
                    if (moveAddedForCell) continue;
                    moveAddedForCell = true;
                }

                int kind = a.kind;
                string label = PrettyAction(a);
                int cost = Mathf.RoundToInt(_quoted[i]);
                bool legal = _mask[i] != 0;
                items.Add(new ActionItem(i.ToString(), label, cost, legal, Array.Empty<int>(), kind));
            }
        }
        hic.pieceActionListFull.Show(items);
    }

    private static IEnumerable<int> ComputeTargetsForActionIndex(int idx)
    {
        _targetsBuffer.Clear();
        if (idx < 0 || idx >= _count) return _targetsBuffer;

        var seed = _offers[idx];
        var kind = seed.kind;
        var src = seed.srcCell;
        var type = seed.pieceType;

        for (int i = 0; i < _count; i++)
        {
            var a = _offers[i];
            if (a.kind != kind) continue;
            if (a.srcCell != src) continue;
            if (a.pieceType != type) continue;
            if (_mask[i] == 0) continue; // masked out = illegal

            if (_mode == Mode.MultiInputAction)
            {
                _targetsBuffer.Add(bm.pieceCellId[a.aux]);
            }
            else
            {
                _targetsBuffer.Add(a.dstCell);
            }
        }
        return _targetsBuffer;
    }


    // --- Create helpers ---
    private static int FindFirstCreateIndexForType(byte type)
    {
        for (int i = 0; i < _count; i++)
        {
            var a = _offers[i];
            if (a.kind != Game.Core.ActionKind.Create) continue;
            if (a.pieceType != type) continue;
            if (_mask[i] == 0) continue;
            return i;
        }
        return -1;
    }

    private static int FindConcreteCreateAction(byte type, ushort dst, ushort? Aux = null)
    {
        // In OfferProvider, Create uses srcCell = 0xFFFF sentinel and kind=Create. We only match type+dst+mask. :contentReference[oaicite:1]{index=1}
        for (int i = 0; i < _count; i++)
        {
            var a = _offers[i];
            if (a.kind != Game.Core.ActionKind.Create) continue;

            // When a wall/config was chosen, only accept the matching aux value
            if (Aux != null && a.aux != Aux) continue;

            if (a.pieceType != type) continue;
            if (a.dstCell != dst) continue;
            if (_mask[i] == 0) continue;
            return i;
        }
        return -1;
    }

    private static bool HasPieceActionsForCell(int cellId)
    {
        ushort src = (ushort)cellId;
        for (int i = 0; i < _count; i++)
        {
            var a = _offers[i];
            if (_mask[i] == 0) continue;                    // illegal/masked
            if (a.kind == Game.Core.ActionKind.EndTurn) continue; // non-piece; ignore
            if (a.srcCell != src) continue;
            return true;
        }
        return false;
    }

    #endregion
    #region new Subscription system

    private static int[] playerTurn = new int[4];
    private static int[] playerAction = new int[4];

    private static int roundNumber = 1;
    private static int turnNumber;
    private static int gameNumber = 1;

    public static event System.Action<ushort> HowManyWallSelected;

    public static event System.Action ChangetoSecondPanelMode;

    public static void howManyWallSelected(ushort ChosenWall)
    {
        HowManyWallSelected?.Invoke(ChosenWall);

        ChangetoSecondPanelMode?.Invoke();
    }

    public static void EnterCreateModeWithWallChosen(ushort chosenWall)
    {
        EnterCreateMode(CachedBuildItemForCreateConnector, chosenWall);
    }

    private static void subscribeMe()
    {
        events.TurnBegin += whenTurnBegins;
        events.ActionBegin += whenActionHappens;
        events.RoundBegin += whenRoundEnds; //this exludes the first round
        events.GameEnd += whenGameEnds;
    }

    private static void whenActionHappens(ActionContext actionContext)
    {
        playerAction[actionContext.ThePlayer]++;
    }

    private static void whenTurnBegins(TurnContext turnContext)
    {
        playerTurn[turnContext.ThePlayer]++;
        playerAction[turnContext.ThePlayer] = 0;
        turnNumber++;

        if (hic.turnNumberText) hic.turnNumberText.text = turnNumber.ToString();
    }

    private static void whenRoundEnds()
    {
        roundNumber++;
        turnNumber = 0;
        Array.Clear(playerTurn, 0, playerTurn.Length);
        if (hic.roundNumberText) hic.roundNumberText.text = $"{roundNumber}";
    }

    private static void whenGameEnds()
    {
        roundNumber = 1;
        gameNumber++;
        if (hic.gameNumberText) hic.gameNumberText.text = $"{gameNumber}";
    }



    #endregion

}
