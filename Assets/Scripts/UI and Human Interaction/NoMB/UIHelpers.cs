using UnityEngine;
using Game.Core; // for GameState and Action Kind
using System.Collections.Generic;
using System;
using System.IO;
using static AFilter.UICState;

public static class UIHelpers
{

    public static Game.Core.Action FindEndTurnIndex()
    {
        for (int i = 0; i < UIBridge._count; i++) if (UIBridge._offers[i].kind == Game.Core.ActionKind.EndTurn) return UIBridge._offers[i];
        throw new InvalidDataException("Failed To Find End Turn");
    }


    public static bool HasPieceActionsForCell(int cellId)
    {
        ushort src = (ushort)cellId;
        for (int i = 0; i < UIBridge._count; i++)
        {
            var a = UIBridge._offers[i];
            if (UIBridge._mask[i] == 0) continue;                    // illegal/masked
            if (a.kind == Game.Core.ActionKind.EndTurn) continue; // non-piece; ignore
            if (a.ActorsCell != src) continue;
            return true;
        }
        return false;
    }


    public static string PrettyAction(Game.Core.Action theAction)
    {
        switch (theAction.kind)
        {
            case ActionKind.Move: return $"Move {theAction.ActorsCell} → {theAction.TargetCell}";
            case ActionKind.Shoot: return $"Shoot {theAction.ActorsCell} → {theAction.TargetCell}";
            case ActionKind.Create: return $"Create {theAction.TargetType} @ {theAction.TargetCell}";
            case ActionKind.CaptureVP: return $"Capture VP @ {theAction.TargetCell}";
            case ActionKind.CoreDamage: return $"Core Damage @ {theAction.TargetCell}";
            case ActionKind.Push: return $"Push target @ {theAction.TargetCell}";
            case ActionKind.GroupBuild: return $"Group Build {theAction.TargetType} @ {theAction.TargetCell}";
            case ActionKind.Upgrade: return $"Upgrade → {UIBridge.bm.GetPieceTypeFromCell(theAction.ActorsCell)} @ {theAction.TargetCell}";
            case ActionKind.Launcher: return $"Launch {theAction.IntakeCell} → {theAction.TargetCell}";
            case ActionKind.Spawner:
                // pieceType carries the actor type; aux carries the target type for readability
                return $"Spawn at {Piece.name[theAction.TargetType]} @ {AFilter.Chosen[(int)ChoosingTargetCell]}";
            case ActionKind.SacrificeFactory: return $"Sacrifice Factory {theAction.ActorsCell} → {theAction.TargetCell}";
            case ActionKind.ConversionFactory: return $"ConversionFactory";
            case ActionKind.EndTurn: return "End Turn";
            case ActionKind.WorkYard: return $"WorkYard @ {theAction.TargetCell}";
            default: return $"{theAction.kind} [{theAction.ActorsCell}->{theAction.TargetCell}]";
        }
    }
    public static void SetBackdropColor(Color c)
    {
        if (UI.hic.backdrop) UI.hic.backdrop.color = c;
    }

    public static void SetPanelBackdropColor(Color c)
    {
        if (UI.hic.panelBackDrop) UI.hic.panelBackDrop.color = c;
    }
}
