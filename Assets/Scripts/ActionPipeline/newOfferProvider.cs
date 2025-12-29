using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using UnityEngine;

public static class OfferProvider
{
     public static int BuildActionList(ref OfferBuild offerBuild)
    {
        offerBuild.cap =  offerBuild.outActions.Length;
        if (offerBuild.outCosts.Length < offerBuild.cap) offerBuild.cap = offerBuild.outCosts.Length;
        if (offerBuild.outMask.Length < offerBuild.cap) offerBuild.cap = offerBuild.outMask.Length;

        offerBuild.write = 0;
        offerBuild.total = 0;

        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;
        int cellCount = bm.GetCellCount();
        int[] scratch = Scratch.GetScratchCellBuffer(offerBuild.gameIndex); // neighbor buffer, etc. (no allocs)

        for (int cell = 0; cell < cellCount; cell++)
        {
            PieceActions(ref offerBuild, cell, ref scratch);
            CreateAction.CreateActions(cell, ref offerBuild);
        }

        EndTurnAction(ref offerBuild);

        ZeroTail(offerBuild.write, offerBuild.outCosts, offerBuild.outMask);
        return offerBuild.total; 
    }


    private static void PieceActions(ref OfferBuild offerBuild, int cell, ref int[] scratch)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

        int pieceId = bm.GetCellOccupant(cell);
        if (IsInvalid(bm, pieceId)) return;
        if ((byte)bm.GetPieceOwner(pieceId) != offerBuild.query.playerId) return;

        byte actorType = bm.GetPieceType(pieceId);

        if (Piece.move_enabled[actorType]) MoveAction.CreateActions(pieceId, actorType, cell, ref offerBuild);
        if (Piece.shoot_enabled[actorType]) ShootAction.CreateActions(pieceId, actorType, cell, ref offerBuild);
        if (Piece.captureVP_enabled[actorType]) CaptureVPAction.CreateActions(actorType, cell, ref offerBuild);
        if (Piece.coreDamage_enabled[actorType]) CoreDamageAction.CreateActions(actorType, cell, ref offerBuild);
        if (Piece.push_enabled[actorType]) PushAction.CreateActions(pieceId, actorType, cell, ref offerBuild);
        if (Piece.groupBuild_enabled[actorType]) GroupBuildAction.CreateActions(pieceId, actorType, cell, ref offerBuild);
        if (Piece.launcher_enabled[actorType]) LauncherAction.CreateActions(pieceId, actorType, cell, ref offerBuild);
        if (Piece.spawn_enabled[actorType]) SpawnAction.CreateActions(pieceId, actorType, cell, ref offerBuild);
        if (Piece.sacrificeFactory_enabled[actorType]) SacrificeFactoryAction.CreateActions(pieceId, actorType, cell, ref offerBuild);
        if (Piece.conversionFactory_enabled[actorType]) ConversionFactoryAction.CreateActions(pieceId, actorType, cell, ref offerBuild);
        if (Piece.explosive_enabled[actorType]) ExplosiveAction.CreateAction(pieceId, actorType, cell, ref offerBuild);
        UpgradeAction.CreateActions(pieceId, actorType, cell, ref offerBuild);
    }

    private static void EndTurnAction(ref OfferBuild offerBuild)
    {
    // =============================
        // EndTurn (always present, always last in prefix, always mask=1)
        // =============================
        offerBuild.total++;
        var endTurn = new Action
        {
            kind = EndTurn,
            pieceType = 0,
            ActorsCellId = (ushort)0xFFFF,
            TargetCellId = 0,
            aux = 0
        };
        if (offerBuild.write < offerBuild.cap)
        {
            offerBuild.outActions[offerBuild.write] = endTurn;
            // enforced free/affordable in WriteCostMask; but set here for clarity
            offerBuild.outCosts[offerBuild.write] = 0f;
            offerBuild.outMask[offerBuild.write] = 1;
            offerBuild.write++;
        }
        else if (offerBuild.cap > 0)
        {
            // Buffer full: overwrite the last slot to guarantee EndTurn is in-branch
            int last = offerBuild.cap - 1;
            offerBuild.outActions[last] = endTurn;
            offerBuild.outCosts[last] = 0f;
            offerBuild.outMask[last] = 1;
            offerBuild.write = offerBuild.cap;
        }
    }

    #region OfferProviderHelpers
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZeroTail(int write, Span<float> outCosts, Span<byte> outMask)
    {
        for (int i = write; i < outCosts.Length; i++) outCosts[i] = 0f;
        for (int i = write; i < outMask.Length; i++) outMask[i] = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInvalid(BoardModel bm, int pieceId) => pieceId == bm.InvalidId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Emit(Action theAction, ref OfferBuild offerBuild)
    {
        offerBuild.total++;
        if (offerBuild.write < offerBuild.cap)
        {
            offerBuild.outActions[offerBuild.write] = theAction;
            WriteCostMask(theAction, ref offerBuild);
            offerBuild.write++;
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteCostMask(in Action theAction, ref OfferBuild offerBuild)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

        // EndTurn is always free & affordable (never masked out by cost)
        if (theAction.kind == ActionKind.EndTurn)
        {
            offerBuild.outCosts[offerBuild.write] = 0f;
            offerBuild.outMask[offerBuild.write] = 1;
            return;
        }

        float quoted;
        if (CostEngine.IsAffordable(theAction, out quoted, offerBuild.gameIndex, offerBuild.query.playerId))
        { offerBuild.outCosts[offerBuild.write] = quoted; offerBuild.outMask[offerBuild.write] = 1; }
        else { offerBuild.outCosts[offerBuild.write] = quoted; offerBuild.outMask[offerBuild.write] = 0; }
    }
    #endregion
}
