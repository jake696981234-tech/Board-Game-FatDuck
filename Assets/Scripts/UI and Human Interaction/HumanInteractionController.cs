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
    #region Refrences

    [Header("Config & Refs")]
    public InteractionConfig config;

    public BoardViewController boardView;   // emits CellClicked(int)

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
    public Button[] PlayerRow_Button;

    [Header("HUD / Debug Box")]
    public TMP_Text Debug_OffersText;
    public TMP_Text Debug_LastActionText;
    public TMP_Text Debug_SnapshotText;


    [Header("Player End Round Totals")]

    public GameObject Payout_Panel1;
    public GameObject Payout_Panel2;
    public TMP_Text TotalPayoutText;
    public TMP_Text VpBonusText;
    public TMP_Text CoreBonusText;
    public TMP_Text TotalFactoryTotalText;

    public GameObject PerTypeFactoryPayOutPrefab;
    public RectTransform PerTypeFactoryPayOutRoot;



    #endregion
    #region Boostrap
    

    

    #endregion

    // ---------- runtime state ----------

    #region Alloc Runtime Data
    

}
