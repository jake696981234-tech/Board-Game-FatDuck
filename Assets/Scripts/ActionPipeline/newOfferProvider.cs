using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Core;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values
using UnityEngine;

public static class newOfferProvider
{
     public static int newBuildActionList(OfferBuild offerBuild)
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
            PieceActions(offerBuild, cell, ref scratch);
            CreateAction.CreateActions(cell);
        }

        EndTurnAction(offerBuild);

        ZeroTail(offerBuild.write, offerBuild.outCosts, offerBuild.outMask);
        return offerBuild.total; 
    }


    private static void PieceActions(OfferBuild offerBuild, int cell, ref int[] scratch)
    {
        var bm = GameRegistry.game[offerBuild.gameIndex].boardModel;

        int pieceId = bm.GetCellOccupant(cell);
        if (IsInvalid(bm, pieceId)) return;
        if ((byte)bm.GetPieceOwner(pieceId) != offerBuild.query.playerId) return;

        byte actorType = bm.GetPieceType(pieceId);

        if (PieceDefinition.move_enabled[actorType]) MoveAction.CreateActions(in scratch, pieceId, actorType, cell, offerBuild);
        if (PieceDefinition.shoot_enabled[actorType]) ShootAction.CreateActions(in scratch, pieceId, actorType, cell, offerBuild);
        if (PieceDefinition.captureVP_enabled[actorType]) CaptureVPAction.CreateActions(pieceId, actorType, cell, offerBuild);
        if (PieceDefinition.coreDamage_enabled[actorType]) CoreDamageAction.CreateActions(in scratch, pieceId, actorType, cell, offerBuild);
        if (PieceDefinition.push_enabled[actorType]) PushAction.CreateActions(in scratch, pieceId, actorType, cell, offerBuild);
        if (PieceDefinition.groupBuild_enabled[actorType]) GroupBuildAction.CreateActions(in scratch, pieceId, actorType, cell, offerBuild);
        if (PieceDefinition.launcher_enabled[actorType]) LauncherAction.CreateActions(in scratch, pieceId, actorType, cell, offerBuild);
        if (PieceDefinition.spawn_enabled[actorType]) SpawnAction.CreateActions();
        if (PieceDefinition.sacrificeFactory_enabled[actorType]) SacrificeFactoryAction.CreateActions(in scratch, pieceId, actorType, cell, offerBuild);
        if (PieceDefinition.conversionFactory_enabled[actorType]) ConversionFactoryAction.CreateActions(in scratch, pieceId, actorType, cell, offerBuild);
        //need to add Piece driven Create actions here
    }

    private static void GeneralActions()
    {
        CreateAction.CreateActions();
    }

    private static void EndTurnAction(OfferBuild offerBuild)
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
    public static void Emit(
        ref Action theAction, OfferBuild offerBuild)
    {
        offerBuild.total++;
        if (offerBuild.write < offerBuild.cap)
        {
            offerBuild.outActions[offerBuild.write] = theAction;
            WriteCostMask(theAction, q, outCosts, outMask, write, gameIndex, player);
            offerBuild.write++;
        }
    }
    #endregion
}
