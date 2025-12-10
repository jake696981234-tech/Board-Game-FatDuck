using UnityEngine;
using Game.Core; // for GameState and Action Kind
using System.Collections.Generic;

public static class UIHelpers
{
    public static BuildItem CachedBuildItemForCreateConnector;
    public static int? _selectedPieceId;
    // Create flow
    public static byte _selectedCreateType = 0;   // which piece type we're trying to create
    public static int? _selectedCellId;
    public static ushort? cachedChosenWall;
    public static bool _createArmed = false;      // in Create mode and seeded

    public static ActionItem? _selectedAction;

    public static int _selectedActionIndex = -1;
    #region Right Panel

    // --- Create helpers ---
    public static int FindFirstCreateIndexForType(byte type)
    {
        for (int i = 0; i < UIBridge._count; i++)
        {
            var a = UIBridge._offers[i];
            if (a.kind != Game.Core.ActionKind.Create) continue;
            if (a.pieceType != type) continue;
            if (UIBridge._mask[i] == 0) continue;
            return i;
        }
        return -1;
    }

    public static int FindConcreteCreateAction(byte type, ushort dst, ushort? Aux = null)
    {
        // In OfferProvider, Create uses srcCell = 0xFFFF sentinel and kind=Create. We only match type+dst+mask. :contentReference[oaicite:1]{index=1}
        for (int i = 0; i < UIBridge._count; i++)
        {
            var a = UIBridge._offers[i];
            if (a.kind != Game.Core.ActionKind.Create) continue;

            // When a wall/config was chosen, only accept the matching aux value
            if (Aux != null && a.aux != Aux) continue;

            if (a.pieceType != type) continue;
            if (a.TargetCellId != dst) continue;
            if (UIBridge._mask[i] == 0) continue;
            return i;
        }
        return -1;
    }
    readonly static List<ushort> wallConfigs = new List<ushort>(64);
    public static IEnumerable<ushort> WallOptionsForPieceType(byte pieceType)
    {
        wallConfigs.Clear();
        for (int i = 0; i < UIBridge._count; i++)
        {
            var offer = UIBridge._offers[i];
            if (offer.kind != ActionKind.Create) continue;   // byte code
            if (offer.pieceType != pieceType) continue;
            if (UIBridge._mask[i] == 0) continue; // masked out = illegal/unaffordable
            wallConfigs.Add(offer.aux);
        }
        return wallConfigs;
    }

    public static int FindEndTurnIndex()
    {
        for (int i = 0; i < UIBridge._count; i++) if (UIBridge._offers[i].kind == Game.Core.ActionKind.EndTurn) return i;
        return -1;
    }

    public static int? _firstCellSelected;
    public static int FindConcreteAction(byte kind, ushort src, byte type, ushort dst)
    {
        for (int i = 0; i < UIBridge._count; i++)
        {
            var a = UIBridge._offers[i];

            if (_firstCellSelected != null)
            {
                if (_firstCellSelected != UIBridge.bm.pieceCellId[a.aux]) continue;
            }

            if (a.kind != kind) continue;
            if (a.ActorsCellId != src) continue;
            if (a.pieceType != type) continue;
            if (a.TargetCellId != dst) continue;
            if (UIBridge._mask[i] == 0) continue;
            return i;
        }
        return -1;
    }

    public static bool FindIfLegalForMultiAction(int cellid)
    {
        for (int i = 0; i < UIBridge._count; i++)
        {
            var a = UIBridge._offers[i];
            if (a.kind != Game.Core.ActionKind.Launcher) continue;
            if (UIBridge._mask[i] == 0) continue; // skip masked/illegal offers

            int pieceId = a.aux;
            if (pieceId < 0 || pieceId >= UIBridge.bm.pieceCellId.Length) continue;
            if (cellid != UIBridge.bm.pieceCellId[pieceId]) continue;
            return true;
        }
        return false;
    }

    public static bool HasPieceActionsForCell(int cellId)
    {
        ushort src = (ushort)cellId;
        for (int i = 0; i < UIBridge._count; i++)
        {
            var a = UIBridge._offers[i];
            if (UIBridge._mask[i] == 0) continue;                    // illegal/masked
            if (a.kind == Game.Core.ActionKind.EndTurn) continue; // non-piece; ignore
            if (a.ActorsCellId != src) continue;
            return true;
        }
        return false;
    }

    readonly static List<int> _createTargetsBuffer = new List<int>(128);
    public static IEnumerable<int> ComputeCreateTargetsForPieceType(byte pieceType, ushort? chosenWall = null)
    {
        _createTargetsBuffer.Clear();
        for (int i = 0; i < UIBridge._count; i++)
        {
            var a = UIBridge._offers[i];
            if (a.kind != Game.Core.ActionKind.Create) continue;   // byte code

            if (chosenWall != null)
            {
                if (a.aux != chosenWall) continue;
            }

            if (a.pieceType != pieceType) continue;
            if (UIBridge._mask[i] == 0) continue; // masked out = illegal/unaffordable
            _createTargetsBuffer.Add(a.TargetCellId);
        }
        return _createTargetsBuffer;
    }

    // PieceAction mode (full coverage panel): Only actions originating at the selected cell
    static List<int> _targetsBuffer = new List<int>(128);
    public static IEnumerable<int> ComputeTargetsForActionIndex(int idx)
    {
        _targetsBuffer.Clear();
        if (idx < 0 || idx >= UIBridge._count) return _targetsBuffer;

        var seed = UIBridge._offers[idx];
        var kind = seed.kind;
        var src = seed.ActorsCellId;
        var type = seed.pieceType;

        for (int i = 0; i < UIBridge._count; i++)
        {
            var a = UIBridge._offers[i];
            if (a.kind != kind) continue;
            if (a.ActorsCellId != src) continue;
            if (a.pieceType != type) continue;
            if (UIBridge._mask[i] == 0) continue; // masked out = illegal

            if (PanelToggles._mode == PanelToggles.Mode.MultiInputAction)
            {
                _targetsBuffer.Add(UIBridge.bm.pieceCellId[a.aux]);
            }
            else
            {
                _targetsBuffer.Add(a.TargetCellId);
            }
        }
        return _targetsBuffer;
    }

    // Pretty label for an action (for list rows)
    public static string PrettyAction(Game.Core.Action a)
    {
        switch (a.kind)
        {
            case Game.Core.ActionKind.Move: return $"Move {a.ActorsCellId} → {a.TargetCellId}";
            case Game.Core.ActionKind.Shoot: return $"Shoot {a.ActorsCellId} → {a.TargetCellId}";
            case Game.Core.ActionKind.Create: return $"Create {a.pieceType} @ {a.TargetCellId}";
            case Game.Core.ActionKind.CaptureVP: return $"Capture VP @ {a.TargetCellId}";
            case Game.Core.ActionKind.CoreDamage: return $"Core Damage @ {a.TargetCellId}";
            case Game.Core.ActionKind.Push: return $"Push target @ {a.TargetCellId}";
            case Game.Core.ActionKind.GroupBuild: return $"Group Build {a.pieceType} @ {a.TargetCellId}";
            case Game.Core.ActionKind.Upgrade: return $"Upgrade → {a.pieceType} @ {a.TargetCellId}";
            case Game.Core.ActionKind.Launcher: return $"Launch {a.aux} → {a.TargetCellId}";
            case Game.Core.ActionKind.Spawner: return $"Spawn x? {a.pieceType} @ {a.TargetCellId}";
            case Game.Core.ActionKind.SacrificeFactory: return $"Sacrifice Factory {a.ActorsCellId} → {a.TargetCellId}";
            case Game.Core.ActionKind.ConversionFactory: return $"ConversionFactory";
            case Game.Core.ActionKind.EndTurn: return "End Turn";
            default: return $"{a.kind} [{a.ActorsCellId}->{a.TargetCellId}]";
        }
    }

    public static void HandleActionExecuted()
    {
        // World changed; rebuild and refresh UI
        UIBridge.RebuildOffersForCurrentPlayer();
        if (PanelToggles._mode == PanelToggles.Mode.PieceAction)
        {
            ShowRightPanel.PushPieceActionListForSelection();
        }
        else
        {
            ShowRightPanel.PushBuildMenu();
            ShowRightPanel.PushNonPieceActionList();
        }
        ShowLeftPanel.HudRefresh();
    }
    #endregion




    public static void SetBackdropColor(Color c)
    {
        if (UI.hic.backdrop) UI.hic.backdrop.color = c;
    }

    public static void SetPanelBackdropColor(Color c)
    {
        if (UI.hic.panelBackDrop) UI.hic.panelBackDrop.color = c;
    }
}
