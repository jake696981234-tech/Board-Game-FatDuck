using UnityEngine;
using Game.Core; // for GameState and Action Kind
using System.Collections.Generic;
using System;
using System.IO;

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
            case Game.Core.ActionKind.Move: return $"Move {theAction.ActorsCell} → {theAction.TargetCell}";
            case Game.Core.ActionKind.Shoot: return $"Shoot {theAction.ActorsCell} → {theAction.TargetCell}";
            case Game.Core.ActionKind.Create: return $"Create {theAction.TargetType} @ {theAction.TargetCell}";
            case Game.Core.ActionKind.CaptureVP: return $"Capture VP @ {theAction.TargetCell}";
            case Game.Core.ActionKind.CoreDamage: return $"Core Damage @ {theAction.TargetCell}";
            case Game.Core.ActionKind.Push: return $"Push target @ {theAction.TargetCell}";
            case Game.Core.ActionKind.GroupBuild: return $"Group Build {theAction.TargetType} @ {theAction.TargetCell}";
            case Game.Core.ActionKind.Upgrade: return $"Upgrade → {UIBridge.bm.GetPieceTypeFromCell(theAction.ActorsCell)} @ {theAction.TargetCell}";
            case Game.Core.ActionKind.Launcher: return $"Launch {theAction.intakeCell} → {theAction.TargetCell}";
            // case Game.Core.ActionKind.Spawner:
            //     // pieceType carries the actor type; aux carries the target type for readability
            //     return $"Spawn x? {(theAction.aux != 0 ? theAction.aux : Piece.spawn_targetType[theAction.pieceType])} @ {theAction.TargetCell}"; to do
            case Game.Core.ActionKind.SacrificeFactory: return $"Sacrifice Factory {theAction.ActorsCell} → {theAction.TargetCell}";
            case Game.Core.ActionKind.ConversionFactory: return $"ConversionFactory";
            case Game.Core.ActionKind.EndTurn: return "End Turn";
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
