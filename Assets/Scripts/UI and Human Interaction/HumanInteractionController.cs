// Assets/Scripts/UI/HIC/HumanInteractionController.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Game.Core; // for GameState
using System.Linq;

public sealed class HumanInteractionController : MonoBehaviour
{
    public enum Mode { Build, Create, PieceAction, ActionExecute, WallOptionsSecoundPanel, MultiInputAction }

    List<int> _targetsBuffer = new List<int>(128);

    #region Refrences

    [Header("Config & Refs")]
    public InteractionConfig config;

    private GameActions gameActions;

    public BoardViewController boardView;   // emits CellClicked(int)
    public GameState gameState;             // your core state (read-only in this skeleton)
    // public OfferProvider offerProvider;  // we'll integrate next pass with your existing OfferProvider API :contentReference[oaicite:7]{index=7}
    // public Pieces pieces;                // for names/icons/costs (aligns with your Pieces registry) :contentReference[oaicite:8]{index=8}

    // Core systems used to build offers:
    public OfferProvider offerProvider;     // assign in inspector
    public BoardModel boardModel;        // assign the same model used by GameState
    public Pieces pieces;            // your registry (names, flags, etc.)
    public CostEngine costEngine;        // pricing engine used by GameState'

    [Header("Canvas/UI")]
    public Image backdrop;
    public Image panelBackDrop;
    public GameObject blockInputOverlay;

    public TMP_Text turnStatusText;
    public TMP_Text budgetText;
    public Button endTurnButton;

    [Header("Panels")]
    public BuildMenuPresenter buildMenu;         // BuildPanel
    public RectTransform createPanel;       // CreatePanel
    public TMP_Text createTitleText;
    public TMP_Text createCostText;
    public Image createSprite;

    public GameObject WallOptionPanelObject;
    public WallOptionPanel wallOptionPanel;

    // Right side (default layout): Build (60%) + Action (non-piece actions) (40%)
    public RectTransform actionPanel;                // 40% panel for non-piece actions
    public ActionListPresenter nonPieceActionList;   // presenter on actionPanel

    // Full coverage panel shown only in PieceAction mode (piece-driven actions)
    public RectTransform pieceActionPanelFull;       // full coverage on right side
    public ActionListPresenter pieceActionListFull;  // presenter on pieceActionPanelFull


    public RectTransform actionExecutePanel; // ActionExecutePanel
    public TMP_Text actionTitleText, actionPieceText, actionCostText;


    // ---------------- HUD (Left) ----------------
    [Header("HUD / Match Header")]
    public TMP_Text Header_TurnOwnerText;
    public TMP_Text Header_ModeText;

    //my own little additon- New Event Manager system, might update everything to go through that script
    public TMP_Text turnNumberText;
    public TMP_Text roundNumberText;
    public TMP_Text gameNumberText;

    [Header("HUD / Player Panel - Personal")]
    public TMP_Text Personal_BudgetText;
    public TMP_Text Personal_VPText;
    public TMP_Text Personal_CoreHPText;
    public Image Personal_TintSwatch; // optional

    [Header("HUD / Player Panel - All Players")]
    public RectTransform AllPlayers_ListRoot; // container to hold rows
    public GameObject PlayerRowPrefab;     // prefab with child names:
                                           // PlayerRow_NameText, PlayerRow_TintSwatch,
                                           // PlayerRow_BudgetText, PlayerRow_VPText, PlayerRow_CoreHPText
    public Button SeePerPieceTypeTotalsButton;


    private Button[] PlayerRow_Button;

    [Header("HUD / Debug Box")]
    public TMP_Text Debug_OffersText;
    public TMP_Text Debug_LastActionText;
    public TMP_Text Debug_SnapshotText;
    private EventManager events;

    [Header("Player End Round Totals")]

    public GameObject Payout_Panel1;
    public GameObject Payout_Panel2;
    public TMP_Text TotalPayoutText;
    public TMP_Text VpBonusText;
    public TMP_Text CoreBonusText;
    public TMP_Text TotalFactoryTotalText;
    public GameSnapshot snapshot;
    public GameObject PerTypeFactoryPayOutPrefab;
    public RectTransform PerTypeFactoryPayOutRoot;

    public static bool giveRawActionOffers;

    public bool IsCurrentPlayer = false;

    #endregion
    #region Boostrap
    public void ManualAwake(GameActions theGameActions,
    EventManager eventManager)
    {
        gameActions = theGameActions;
        events = eventManager;
        subscribeMe();
        if (boardView) boardView.CellClicked += OnCellClicked;   // from your BoardViewController
        if (endTurnButton) endTurnButton.onClick.AddListener(OnEndTurnClicked);

        // Ensure player row button array is allocated (supports up to 4 seats by design)
        if (PlayerRow_Button == null || PlayerRow_Button.Length < 4)
        {
            PlayerRow_Button = new Button[4];
        }



        HowManyWallSelected += EnterCreateModeWithWallChosen;
        giveRawActionOffers = config.GiveRawActionOffers;

        ChangetoSecondPanelMode += SetWallOptionsSecondPanelMode;

        IsCurrentPlayer = gameState.CurrentPlayerId == _humanPlayer;
    }

    public void ManualEnable()
    {
        if (gameState != null) gameState.OnActionExecuted += HandleActionExecuted; // refresh on every mutation
        RebuildOffersForCurrentPlayer();
        EnterBuildMode(); // will push menus from offers
        HookPresenters();
        HudRefresh();
    }

    #endregion

    // ---------- runtime state ----------

    #region Alloc Runtime Data
    private Mode _mode = Mode.Build;

    [SerializeField, Range(0, 3)] private byte _humanPlayer = 0; // bound by bootstrapper
    private int? _selectedPieceId;
    private int? _selectedCellId;

    private int? _firstCellSelected;
    private ActionItem? _selectedAction;
    private int _selectedActionIndex = -1;

    // Create flow
    private byte _selectedCreateType = 0;   // which piece type we're trying to create
    private bool _createArmed = false;      // in Create mode and seeded

    // Offers built from core (capacity big enough to hold a full turn's options)
    const int kCap = 1500;
    private Game.Core.Action[] _offers = new Game.Core.Action[kCap];
    private float[] _quoted = new float[kCap];
    private byte[] _mask = new byte[kCap];
    private int _total;   // total actions returned by provider (may exceed cap)
    private int _count;   // displayed = min(total, cap)

    private ushort? cachedChosenWall;

    private BuildItem CachedBuildItemForCreateConnector;

    private string _lastActionLabel = string.Empty; // for Debug HUD


    private void OnDisable()
    {
        if (gameState != null) gameState.OnActionExecuted -= HandleActionExecuted;
        UnhookPresenters();
    }


    public void SetWallOptionsSecondPanelMode()
    {

        _mode = Mode.WallOptionsSecoundPanel;
    }

    private void enterWallOptionsSecondPanelMode()
    {
        TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: false, walls: true);
        boardView.ClearHighlights();
        _mode = Mode.Create;
    }

    private void Update()
    {
        boardView.SnapShotUpdate();
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
    #endregion
    // ===================== Mode transitions =====================
    #region Mode Transitions
    private void EnterBuildMode()
    {
        boardView.ClearHighlights();
        if (config && boardView) boardView.ApplyDefaultCellColor(config.defaultCellColor);
        _mode = Mode.Build;
        _selectedPieceId = null;
        _selectedAction = null;

        SetBackdropColor(config ? config.buildModeBackground : new Color(0, 0, 0, 0.8f));
        SetPanelBackdropColor(config ? config.buildModePanelBackground : new Color(0, 0, 0, 0.8f));

        TogglePanels(build: true, create: false, action: true, pieceFull: false, execute: false, walls: false);
        PushBuildMenu();
        PushNonPieceActionList();   // NEW: default action list = non-piece actions (e.g., End Turn)

        HudRefresh();
    }

    private void EnterCreateMode(BuildItem build, ushort? ChosenWall = null)
    {
        boardView.ClearHighlights();
        if (config && boardView) boardView.ApplyDefaultCellColor(config.defaultCellColor);
        // Seed the create family (piece type) and highlight all legal cells for that type.
        _selectedCreateType = build.pieceType;
        _selectedActionIndex = FindFirstCreateIndexForType(_selectedCreateType); // seed (can be -1 if none)
        _createArmed = (_selectedActionIndex >= 0);

        if (ChosenWall == null)
        {
            boardView.HighlightCells(
                ComputeCreateTargetsForPieceType(_selectedCreateType),
                config ? config.createModeCellHighlight : new Color(0.25f, 0.75f, 0.25f, 0.6f)
            );

            cachedChosenWall = null;
        }
        else
        {
            boardView.HighlightCells(
                ComputeCreateTargetsForPieceType(_selectedCreateType, ChosenWall),
                config ? config.createModeCellHighlight : new Color(0.25f, 0.75f, 0.25f, 0.6f)
            );

            cachedChosenWall = ChosenWall;
        }


        _mode = Mode.Create;
        _selectedPieceId = null;
        _selectedCellId = null;
        _selectedAction = null;

        SetBackdropColor(config ? config.createModeBackground : new Color(0, 0, 0, 0.8f));
        SetPanelBackdropColor(config ? config.createModePanelBackground : new Color(0, 0, 0, 0.8f));
        TogglePanels(build: false, create: true, action: false, pieceFull: false, execute: false, walls: false);

        if (createTitleText) createTitleText.text = $"Create: {build.name}";
        if (createCostText) createCostText.text = $"Cost: {build.cost}";
        if (createSprite)
        {
            var s = !string.IsNullOrEmpty(build.spritePath) ? Resources.Load<Sprite>(build.spritePath) : null;
            createSprite.sprite = s;
            createSprite.enabled = (s != null);
        }

        // NOTE: We haven't added highlight APIs to BoardViewController yet, so no highlight calls here.
        HudRefresh();
    }

    public void EnterConnectingMode(BuildItem item)
    {
        // Ensure we query offers for the piece type the user just picked

        _mode = Mode.Create;
        _selectedCreateType = item.pieceType;
        _selectedActionIndex = FindFirstCreateIndexForType(_selectedCreateType);
        var wallOptions = WallOptionsForPieceType(_selectedCreateType);

        TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: false, walls: true);

        wallOptionPanel.ShowSideOptions(wallOptions);
    }

    private void EnterPieceActionMode(int? pieceId = null)
    {
        boardView.ClearHighlights();
        if (config && boardView) boardView.ApplyDefaultCellColor(config.defaultCellColor);
        _mode = Mode.PieceAction;
        _selectedAction = null;
        if (pieceId.HasValue) _selectedPieceId = pieceId;

        SetBackdropColor(config ? config.pieceActionBackground : new Color(0, 0, 0, 0.8f));
        SetPanelBackdropColor(config ? config.pieceActionPanelBackground : new Color(0, 0, 0, 0.8f));

        TogglePanels(build: false, create: false, action: false, pieceFull: true, execute: false, walls: false);
        // Immediately fill the full panel so it shows on the first click:
        PushPieceActionListForSelection();

        HudRefresh();
    }

    private void EnterActionExecuteMode(ActionItem action)
    {
        boardView.ClearHighlights();
        if (action.kind == Game.Core.ActionKind.Launcher && _mode != Mode.MultiInputAction)
        {
            _mode = Mode.MultiInputAction;
        }
        else
        {
            _mode = Mode.ActionExecute;
        }

        _selectedAction = action;

        SetBackdropColor(config ? config.actionExecuteBackground : new Color(0, 0, 0, 0.8f));
        SetPanelBackdropColor(config ? config.actionExecutePanelBackground : new Color(0, 0, 0, 0.8f));

        TogglePanels(build: false, create: false, action: false, pieceFull: false, execute: true, walls: false);
        // (Re)apply legal-target highlights for clarity while in execute mode
        if (_selectedActionIndex >= 0)
        {
            var col = (config ? config.actionLegalTargetHighlight : new Color(0.6f, 0.35f, 0.9f, 0.65f));
            boardView.ClearHighlights();
            boardView.HighlightCells(ComputeTargetsForActionIndex(_selectedActionIndex), col);
        }

        if (actionTitleText) actionTitleText.text = action.name;
        if (actionPieceText) actionPieceText.text = _selectedPieceId.HasValue ? $"Piece #{_selectedPieceId.Value}" : "Piece (none)";
        if (actionCostText) actionCostText.text = $"Cost: {action.cost}";

        HudRefresh();
    }

    #endregion

    // ===================== Input (from board) =====================
    #region input from Board
    private void OnCellClicked(int cellId)
    {
        if (config && config.blockInputWhenNotYourTurn && gameState != null)
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
                if (config && boardView)
                {
                    boardView.ClearHighlights();
                    boardView.HighlightSelection(cellId, config.selectionHighlight);
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
                boardView.ClearHighlights();
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
                    boardView.ClearHighlights();
                    EnterBuildMode();
                }
                break;
        }
    }

    private bool FindIfLegalForMultiAction(int cellid)
    {
        for (int i = 0; i < _count; i++)
        {
            var a = _offers[i];
            if (a.kind != Game.Core.ActionKind.Launcher) continue;
            if (_mask[i] == 0) continue; // skip masked/illegal offers

            int pieceId = a.aux;
            if (pieceId < 0 || pieceId >= boardModel.pieceCellId.Length) continue;
            if (cellid != boardModel.pieceCellId[pieceId]) continue;
            return true;
        }
        return false;
    }

    private int FindConcreteAction(byte kind, ushort src, byte type, ushort dst)
    {
        for (int i = 0; i < _count; i++)
        {
            var a = _offers[i];

            if (_firstCellSelected != null)
            {
                if (_firstCellSelected != boardModel.pieceCellId[a.aux]) continue;
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
    private void PerformActionIndex(int idx)
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
    private void OnEndTurnClicked()
    {
        // find EndTurn in the current offers and perform it
        int idx = FindEndTurnIndex();
        if (idx >= 0) PerformActionIndex(idx);
    }

    private int FindEndTurnIndex()
    {
        for (int i = 0; i < _count; i++) if (_offers[i].kind == Game.Core.ActionKind.EndTurn) return i;
        return -1;
    }

    #endregion
    // ===================== UI helpers =====================

    #region UI Helpers
    private void TogglePanels(bool build, bool create, bool action, bool pieceFull, bool execute, bool walls)
    {
        if (buildMenu) buildMenu.gameObject.SetActive(build);
        if (createPanel) createPanel.gameObject.SetActive(create);
        if (actionPanel) actionPanel.gameObject.SetActive(action);
        if (pieceActionPanelFull) pieceActionPanelFull.gameObject.SetActive(pieceFull);
        if (actionExecutePanel) actionExecutePanel.gameObject.SetActive(execute);
        if (WallOptionPanelObject) WallOptionPanelObject.gameObject.SetActive(walls);
    }

    public enum LeftPanelsModes { DefaultPanel, EndRoundTotalPanel, EndRoundTotalPanel2 }
    public LeftPanelsModes leftPanelMode = LeftPanelsModes.DefaultPanel;
    private void ToggleLeftPanels(bool EndRoundTotalPanel1, bool EndRoundTotalPanel2)
    {
        Payout_Panel1.SetActive(EndRoundTotalPanel1);
        Payout_Panel2.SetActive(EndRoundTotalPanel2);
    }



    private void SetBackdropColor(Color c)
    {
        if (backdrop) backdrop.color = c;
    }

    private void SetPanelBackdropColor(Color c)
    {
        if (panelBackDrop) panelBackDrop.color = c;
    }

    // Legacy small HUD fields (kept) + new consolidated HUD refresh
    private void UpdateHud_LegacySmall()
    {
        if (budgetText && gameState != null)
            budgetText.text = $"Budget: {gameState.GetBudget(_humanPlayer):0}";
        if (blockInputOverlay && gameState != null && config != null && config.blockInputWhenNotYourTurn)
            blockInputOverlay.SetActive(gameState.CurrentPlayerId != _humanPlayer);
    }

    private void HudRefresh()
    {
        UpdateHud_LegacySmall();
        if (gameState == null) return;

        // --- Match header ---
        if (Header_TurnOwnerText) Header_TurnOwnerText.text = $"Player {gameState.CurrentPlayerId}";
        if (Header_ModeText) Header_ModeText.text = _mode.ToString();

        // --- Personal stats (your seat) ---
        if (Personal_BudgetText) Personal_BudgetText.text = "Budget: " + $"{Mathf.RoundToInt(gameState.GetBudget(_humanPlayer))}";
        if (Personal_VPText) Personal_VPText.text = "VP: " + $"{gameState.GetVP(_humanPlayer)}";
        if (Personal_CoreHPText) Personal_CoreHPText.text = "Core Hp: " + $"{gameState.GetCoreHealth(_humanPlayer)}";
        // Tint swatch optional; if you have a palette somewhere you can assign it here.

        // --- All players list ---
        if (AllPlayers_ListRoot && PlayerRowPrefab)
        {
            // Clear old rows
            for (int i = AllPlayers_ListRoot.childCount - 1; i >= 0; i--)
                Destroy(AllPlayers_ListRoot.GetChild(i).gameObject);

            // Show current player first, then others in seat order
            Span<int> order = stackalloc int[4] { gameState.CurrentPlayerId, (gameState.CurrentPlayerId + 1) & 3, (gameState.CurrentPlayerId + 2) & 3, (gameState.CurrentPlayerId + 3) & 3 };
            for (int k = 0; k < 4; k++)
            {
                int p = order[k];
                var go = Instantiate(PlayerRowPrefab, AllPlayers_ListRoot);
                BindPlayerRow(go.transform as RectTransform, p);
            }
        }

        // --- Debug box ---
        if (Debug_OffersText)
        {
            int masked = 0; for (int i = 0; i < _count; i++) if (_mask[i] == 0) masked++;
            Debug_OffersText.text = $"Shown: {_count}  /  Total: {_total}  (Masked: {masked})";
        }
        if (Debug_LastActionText) Debug_LastActionText.text = string.IsNullOrEmpty(_lastActionLabel) ? "—" : _lastActionLabel;
        if (Debug_SnapshotText)
        {
            // We don't hold a snapshot here; show some quick match counters instead.
            Debug_SnapshotText.text = $"CenterVP={gameState.GetCenterVP()}  RoundsLeft={gameState.RoundsLeft}";
        }
    }
    private void BindPlayerRow(RectTransform row, int playerId)
    {
        if (!row) return;
        if (PlayerRow_Button == null || PlayerRow_Button.Length <= playerId)
        {
            PlayerRow_Button = new Button[4];
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
        PlayerRow_Button[playerId] = row.Find("PlayerRow_Button")?.GetComponent<Button>();

        if (nameText) nameText.text = (playerId == gameState.CurrentPlayerId) ? $">Player {playerId}" : $"Player {playerId}";
        if (budgetText) budgetText.text = $"{Mathf.RoundToInt(gameState.GetBudget((byte)playerId))}";
        if (vpText) vpText.text = $"{gameState.GetVP((byte)playerId)}";
        if (hpText) hpText.text = $"{gameState.GetCoreHealth((byte)playerId)}";
        if (turn) turn.text = $"{playerTurn[playerId]}";
        if (action) action.text = $"{playerAction[playerId]}";
        if (passed) passed.text = $"{gameState.PassedTurn(playerId)}";

        PlayerRow_Button[playerId].onClick.AddListener(() => showPlayerEndRoundTotals(playerId));



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
    public EndRoundTotalsPlayer endRoundTotalsPlayer;
    private void showPlayerEndRoundTotals(int playerId)
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


    public void updatePlayerEndRoundTotals(int playerId)
    {
        float TotalFactory = snapshot.PerEndRoundPayOut[playerId].PieceTypePayOut.Sum();

        TotalPayoutText.text = $"{TotalFactory + snapshot.PerEndRoundPayOut[playerId].BonusForVP + snapshot.PerEndRoundPayOut[playerId].BonusForCoreDamage}";
        VpBonusText.text = $"{snapshot.PerEndRoundPayOut[playerId].BonusForVP}";
        CoreBonusText.text = $"{snapshot.PerEndRoundPayOut[playerId].BonusForCoreDamage}";
        TotalFactoryTotalText.text = $"{TotalFactory}";

        if (leftPanelMode == LeftPanelsModes.EndRoundTotalPanel2) updatePerTypeEndRoundTotals();
    }

    private readonly List<GameObject> spawnedPerTypeFactoryPayOutPrefab = new List<GameObject>();
    public void ShowFactoryBonusByPieceTypePrefabs()
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

    public void updatePerTypeEndRoundTotals()
    {
        for (int c = 0; c < spawnedPerTypeFactoryPayOutPrefab.Count; c++)
        {
            if (spawnedPerTypeFactoryPayOutPrefab[c] != null)
            {
                Destroy(spawnedPerTypeFactoryPayOutPrefab[c]);
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
            var prefab = Instantiate(PerTypeFactoryPayOutPrefab, PerTypeFactoryPayOutRoot);
            prefab.SetActive(true);

            var prefabScript = prefab.GetComponent<FactoryPerTypePayOut>();
            prefabScript.SetValues(
                pieces.displayNameByType[type],
                payout.PieceTypePayOut[i]
            );
            spawnedPerTypeFactoryPayOutPrefab.Add(prefab);
        }
    }

    // Called by bootstrapper
    public void SetHumanSeat(byte seat)
    {
        _humanPlayer = seat;
        HudRefresh();
    }

    private void HookPresenters()
    {
        if (buildMenu) buildMenu.OnItemClicked += OnBuildItemClicked;
        if (nonPieceActionList) nonPieceActionList.OnItemClicked += OnActionItemClicked;
        if (pieceActionListFull) pieceActionListFull.OnItemClicked += OnActionItemClicked;
        if (SeePerPieceTypeTotalsButton) SeePerPieceTypeTotalsButton.onClick.AddListener(() => ShowFactoryBonusByPieceTypePrefabs());
    }

    private void UnhookPresenters()
    {
        if (buildMenu) buildMenu.OnItemClicked -= OnBuildItemClicked;
        if (nonPieceActionList) nonPieceActionList.OnItemClicked -= OnActionItemClicked;
        if (pieceActionListFull) pieceActionListFull.OnItemClicked -= OnActionItemClicked;
    }

    private void OnBuildItemClicked(BuildItem item)
    {
        if (pieces.HasConnectors(item.pieceType))
        {
            CachedBuildItemForCreateConnector = item;
            EnterConnectingMode(item);
        }
        else
        {
            EnterCreateMode(item);
        }
    }

    private void OnActionItemClicked(ActionItem item)
    {
        if (!int.TryParse(item.id, out var idx)) return;
        if (idx < 0 || idx >= _count) return;

        // If this is a NON-piece action (e.g., End Turn), perform immediately.
        // NOTE: If your constant lives at Action.Kind.EndTurn, swap the symbol accordingly.

        if (_offers[idx].kind == Game.Core.ActionKind.EndTurn)
        {
            PerformActionIndex(idx);
            boardView.ClearHighlights();
            EnterBuildMode(); // stay on default layout after executing a non-piece action
            return;
        }

        // Piece-derived action: highlight all legal target cells right away
        var col = (config ? config.actionLegalTargetHighlight : new Color(0.6f, 0.35f, 0.9f, 0.65f));
        _selectedAction = item;
        _selectedActionIndex = idx; // seed used for target resolution
        boardView.ClearHighlights();
        boardView.HighlightCells(ComputeTargetsForActionIndex(_selectedActionIndex), col);
        EnterActionExecuteMode(item);
    }

    // Buffer for create targets (re-use across calls)
    readonly List<int> _createTargetsBuffer = new List<int>(128);


    // Collect all legal dst cells for "Create <pieceType>"
    IEnumerable<int> ComputeCreateTargetsForPieceType(byte pieceType, ushort? chosenWall = null)
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

    readonly List<ushort> wallConfigs = new List<ushort>(64);
    IEnumerable<ushort> WallOptionsForPieceType(byte pieceType)
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
    private void RebuildOffersForCurrentPlayer()
    {
        _total = _count = 0;
        if (offerProvider == null || boardModel == null || pieces == null || costEngine == null || gameState == null)
            return;

        // Build the query from live systems (readonly struct → must use constructor)
        var q = new OfferQuery(
            boardModel,
            pieces,
            gameState.CurrentPlayerRef,
            gameState.CurrentPlayerId,
            costEngine,
            gameState.PieceLimitEnabled,
            gameState.pieceLimitPerPlayer,
            gameActions.multiCreateActive,
            gameActions.multiCreateType,
            gameActions.multiCreateBorder,
            gameActions.multiCreateRemaining,
            gameActions.multiCreateCells.ToArray(),
            gameActions.MultiCreateCellCount
        );

        // Fill the spans (zero-alloc path in OfferProvider). Function returns TOTAL (may exceed cap). :contentReference[oaicite:7]{index=7}
        _total = offerProvider.BuildActionList(in q, _offers.AsSpan(), _quoted.AsSpan(), _mask.AsSpan());
        _count = Mathf.Min(kCap, _total);
    }

    private void HandleActionExecuted()
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
            case Game.Core.ActionKind.EndTurn: return "End Turn";
            default: return $"{a.kind} [{a.srcCell}->{a.dstCell}]";
        }
    }


    private void PushBuildMenu()
    {
        // Build list shows all Create* actions this turn (grouping/labeling by piece type + quoted cost)
        var items = new List<BuildItem>(_count);
        if (_count > 0)
        {
            var names = pieces.displayNameByType;     // assumed from your Pieces registry
            var paths = pieces.spritePathByType;      // assumed from your Pieces registry

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
        buildMenu.Show(items, config, pieces);
    }




    // Default layout (right-side 40%): Non-piece actions only (e.g., End Turn)
    private void PushNonPieceActionList()
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
        nonPieceActionList.Show(items);
    }


    // PieceAction mode (full coverage panel): Only actions originating at the selected cell


    private void PushPieceActionListForSelection()
    {
        var items = new List<ActionItem>();
        int onlyOneMove = 0;
        if (_selectedCellId.HasValue)
        {
            int cell = _selectedCellId.Value;
            for (int i = 0; i < _count; i++)
            {
                var a = _offers[i];
                if (a.kind == Game.Core.ActionKind.Move && !config.GiveRawActionOffers)
                {
                    onlyOneMove++;
                }
                if (a.kind == Game.Core.ActionKind.Move && onlyOneMove >= 2) continue;


                if (a.kind == Game.Core.ActionKind.EndTurn) continue; // exclude non-piece actions
                if (a.srcCell != (ushort)cell) continue;               // only actions from this piece

                int kind = a.kind;
                string label = PrettyAction(a);
                int cost = Mathf.RoundToInt(_quoted[i]);
                bool legal = _mask[i] != 0;
                items.Add(new ActionItem(i.ToString(), label, cost, legal, Array.Empty<int>(), kind));
            }
        }
        pieceActionListFull.Show(items);
    }

    IEnumerable<int> ComputeTargetsForActionIndex(int idx)
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
                _targetsBuffer.Add(boardModel.pieceCellId[a.aux]);
            }
            else
            {
                _targetsBuffer.Add(a.dstCell);
            }
        }
        return _targetsBuffer;
    }


    // --- Create helpers ---
    private int FindFirstCreateIndexForType(byte type)
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

    private int FindConcreteCreateAction(byte type, ushort dst, ushort? Aux = null)
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

    private bool HasPieceActionsForCell(int cellId)
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

    private int[] playerTurn = new int[4];
    private int[] playerAction = new int[4];

    private int roundNumber = 1;
    private int turnNumber;
    private int gameNumber = 1;

    public static event System.Action<ushort> HowManyWallSelected;

    public static event System.Action ChangetoSecondPanelMode;

    public static void howManyWallSelected(ushort ChosenWall)
    {
        HowManyWallSelected?.Invoke(ChosenWall);

        ChangetoSecondPanelMode?.Invoke();
    }

    public void EnterCreateModeWithWallChosen(ushort chosenWall)
    {
        EnterCreateMode(CachedBuildItemForCreateConnector, chosenWall);
    }

    private void subscribeMe()
    {
        events.TurnBegin += whenTurnBegins;
        events.ActionBegin += whenActionHappens;
        events.RoundBegin += whenRoundEnds; //this exludes the first round
        events.GameEnd += whenGameEnds;
    }

    private void whenActionHappens(ActionContext actionContext)
    {
        playerAction[actionContext.ThePlayer]++;
    }

    private void whenTurnBegins(TurnContext turnContext)
    {
        playerTurn[turnContext.ThePlayer]++;
        playerAction[turnContext.ThePlayer] = 0;
        turnNumber++;

        if (turnNumberText) turnNumberText.text = turnNumber.ToString();
    }

    private void whenRoundEnds()
    {
        roundNumber++;
        turnNumber = 0;
        Array.Clear(playerTurn, 0, playerTurn.Length);
        if (roundNumberText) roundNumberText.text = $"{roundNumber}";
    }

    private void whenGameEnds()
    {
        roundNumber = 1;
        gameNumber++;
        if (gameNumberText) gameNumberText.text = $"{gameNumber}";
    }



    #endregion

}
