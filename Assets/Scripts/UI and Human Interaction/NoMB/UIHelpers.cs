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
            if (a.ActorsCellId != src) continue;
            return true;
        }
        return false;
    }

   
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
            case Game.Core.ActionKind.Spawner:
                // pieceType carries the actor type; aux carries the target type for readability
                return $"Spawn x? {(a.aux != 0 ? a.aux : Piece.spawn_targetType[a.pieceType])} @ {a.TargetCellId}";
            case Game.Core.ActionKind.SacrificeFactory: return $"Sacrifice Factory {a.ActorsCellId} → {a.TargetCellId}";
            case Game.Core.ActionKind.ConversionFactory: return $"ConversionFactory";
            case Game.Core.ActionKind.EndTurn: return "End Turn";
            default: return $"{a.kind} [{a.ActorsCellId}->{a.TargetCellId}]";
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
