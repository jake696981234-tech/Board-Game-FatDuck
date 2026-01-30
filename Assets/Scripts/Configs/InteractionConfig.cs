// Assets/Scripts/UI/HIC/InteractionConfig.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "BGO/InteractionConfig")]
public class InteractionConfig : ScriptableObject
{
    [Header("Backdrop colors per mode")]
    public Color buildModeBackground = new(0.1f, 0.1f, 0.1f, 0.85f);
    public Color buildModePanelBackground = new(0.12f, 0.1f, 0.1f, 0.85f);

    public Color createModeBackground = new(0.12f, 0.1f, 0.1f, 0.85f);
    public Color createModePanelBackground = new(0.1f, 0.12f, 0.1f, 0.85f);

    public Color pieceActionBackground = new(0.1f, 0.12f, 0.1f, 0.85f);
    public Color pieceActionPanelBackground = new(0.1f, 0.1f, 0.12f, 0.85f);
    public Color actionExecuteBackground = new(0.1f, 0.1f, 0.12f, 0.85f);
    public Color actionExecutePanelBackground = new(0.1f, 0.1f, 0.1f, 0.85f);

    public Color ConnectorModeBackground = new(0.1f, 0.12f, 0.1f, 0.85f);



    [Header("Cell Highlights")]
    // (Legacy) Kept for backwards compatibility, not used for highlights anymore:
    public Color createModeCellHighlight = new(0.1f, 0.1f, 0.1f, 0.85f);
    public Color SacrificeCostCellHighlight = new(0.1f, 0.1f, 0.1f, 0.85f);

    public Color actionLegalTargetHighlight = new(0.1f, 0.1f, 0.1f, 0.85f);
    public Color defaultCellColor = Color.white;

    public Color cellHoverTint = new(0.90f, 0.90f, 1f, 1f);

    public Color cellPressedTint = new(0.80f, 0.80f, 1f, 1f);
    public Color cellDisabledTint = new(0.50f, 0.50f, 0.50f, 1f);

    public Color selectionHighlight = new(0.85f, 0.85f, 0.10f, 0.80f);

    [Header("Build Menu")]
    public Color BuildingColour = new(0f, 0.5450981f, 0.5450981f, 1f);
    public Color SoldierColour = new(0.7215686f, 0.5254902f, 0.04313726f, 1f);

    public List<FactionColor> FactionColors;





    [Header("Input")]
    public bool blockInputWhenNotYourTurn = true;

    public bool GiveRawActionOffers = true;

    public bool ManualStepThroughSnapShots = false;

    public bool DelayOnActions = false;
    public float TimeDelayOnActions = 0.1f;

    public bool HumanTimeDecrease = false;
    public bool altWallSelect = false;
    public bool skipNumberWallSelect = false;

    [System.Serializable]
    public struct FactionColor
    {
        public string faction;
        public Color color;
    }
}
