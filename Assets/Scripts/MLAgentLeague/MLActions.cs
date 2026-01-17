using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Game.Core; // Action, OfferQuery, GameState, PlayerState
using static MLActions.MLState;
using System.Collections.Generic;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public static class MLActions
{
    public enum MLState
    {
        ChoosingKind = 0,
        ChoosingActorsCell = 2,
        ChoosingTargetCell = 3,
        ChoosingPieceType = 4,
        ChoosingWallConfig = 5,
        ChoosingInstakeCellID = 6,
    }

    

    



    private static int BuildOffersForCurrentPlayer(MLSam MLSam)
    {
        var gameState = GameRegistry.game[MLSam.gameIndex].gameState;
        // Build OfferQuery: (bm, pcs, ps, playerId, cost)
        // GameActions.GetMultiCreateState(out bool mcActive, out byte mcType, out bool mcBorder, out int mcRemaining, out int[] mcCells, out int mcCellCount, gameIndex);
        var query = new OfferQuery(MLSam.playerId, gameState.PieceLimitEnabled, gameState.pieceLimitPerPlayer
            /*mcActive, mcType, mcBorder, mcRemaining, mcCells, mcCellCount */);

        var acts = MLSam.Offers.AsSpan();
        var costs = MLSam.Quoted.AsSpan();
        var mask = MLSam.ActionMask.AsSpan();

        OfferBuild offerBuild;
        offerBuild.query = query;
        offerBuild.outActions = acts;
        offerBuild.outCosts = costs;
        offerBuild.outMask = mask;
        offerBuild.gameIndex = MLSam.gameIndex;
        offerBuild.write = 0;
        offerBuild.total = 0;
        offerBuild.cap = 0;

        int total = OfferProvider.BuildActionList(ref offerBuild);
        // We only allow the emitted prefix to be selectable by the policy
        return Math.Min(total, acts.Length);
    }

    public static readonly HashSet<int> ActionOriginal = new HashSet<int>();
    public static void GiveMeDescreteMask(ref IDiscreteActionMask actionMask, MLSam MLSam)
    {
        ActionOriginal.Clear();
        MLSam.NumberOfOffers = BuildOffersForCurrentPlayer(MLSam);

        switch (MLSam.mlState) 
        {
             case ChoosingKind:
                {
                    MaskKinds(ref actionMask, MLSam);
                    break;
                }
            case ChoosingActorsCell:
                {
                    MaskActorsCell(ref actionMask, MLSam);
                    break;
                }
            case ChoosingTargetCell:
                {
                    MaskTargetCell(ref actionMask, MLSam);
                    break;
                }
            case ChoosingPieceType:
                {
                    MaskPieceType(ref actionMask, MLSam);
                    break;
                }
            case ChoosingWallConfig:
                {
                    MaskWallConfig(ref actionMask, MLSam);
                    break;
                }
            case ChoosingInstakeCellID:
                {
                    MaskInstakeCellID(ref actionMask, MLSam);
                    break;
                }
        }
        MaskUnusedActions(ref actionMask, MLSam);
    }

    private static void MaskKinds(ref IDiscreteActionMask actionMask, MLSam MLSam)
    {
        for (int theAction = 0; theAction < MLSam.NumberOfOffers; theAction++) { if (!ActionOriginal.Add(MLSam.Offers[theAction].kind) || MLSam.ActionMask[theAction] == 0) actionMask.SetActionEnabled((int)ChoosingKind, MLSam.Offers[theAction + 1].kind, false); }
    }
    private static void MaskActorsCell(ref IDiscreteActionMask actionMask, MLSam MLSam)
    {
        for (int theAction = 0; theAction < MLSam.NumberOfOffers; theAction++) 
        { 
            if (ActionOriginal.Add(MLSam.Offers[theAction].ActorsCellId) && MLSam.ActionMask[theAction] != 0 && MLSam.Offers[theAction].kind == MLSam.ChosenAction[0]) continue;
            actionMask.SetActionEnabled((int)ChoosingActorsCell, MLSam.Offers[theAction + 1].ActorsCellId, false); 
        }
    }
    private static void MaskTargetCell(ref IDiscreteActionMask actionMask, MLSam MLSam)
    {
        for (int theAction = 0; theAction < MLSam.NumberOfOffers; theAction++) 
        { 
            if (ActionOriginal.Add(MLSam.Offers[theAction].TargetCellId) && MLSam.ActionMask[theAction] != 0 && MLSam.Offers[theAction].kind == MLSam.ChosenAction[0] && MLSam.Offers[theAction].ActorsCellId == MLSam.ChosenAction[1]) continue;
            actionMask.SetActionEnabled((int)ChoosingTargetCell, MLSam.Offers[theAction + 1].TargetCellId, false); 
        }
    }
    private static void MaskPieceType(ref IDiscreteActionMask actionMask, MLSam MLSam)
    {
        for (int theAction = 0; theAction < MLSam.NumberOfOffers; theAction++) 
        { 
            if (ActionOriginal.Add(MLSam.Offers[theAction].pieceType) && MLSam.ActionMask[theAction] != 0 && MLSam.Offers[theAction].kind == MLSam.ChosenAction[0] && MLSam.Offers[theAction].ActorsCellId == MLSam.ChosenAction[1] && MLSam.Offers[theAction].TargetCellId == MLSam.ChosenAction[2]) continue;
            actionMask.SetActionEnabled((int)ChoosingPieceType, MLSam.Offers[theAction + 1].pieceType, false); 
        }
    }
    private static void MaskWallConfig(ref IDiscreteActionMask actionMask, MLSam MLSam)
    {
        for (int theAction = 0; theAction < MLSam.NumberOfOffers; theAction++) 
        { 
            if (ActionOriginal.Add(MLSam.Offers[theAction].pieceType) && MLSam.ActionMask[theAction] != 0 && MLSam.Offers[theAction].kind == MLSam.ChosenAction[0] && MLSam.Offers[theAction].ActorsCellId == MLSam.ChosenAction[1] && MLSam.Offers[theAction].TargetCellId == MLSam.ChosenAction[2] && MLSam.Offers[theAction].pieceType == MLSam.ChosenAction[3]) continue;
            actionMask.SetActionEnabled((int)ChoosingWallConfig, MLSam.Offers[theAction + 1].aux, false); 
        }
    }
    private static void MaskInstakeCellID(ref IDiscreteActionMask actionMask, MLSam MLSam)
    {
        
    }

    private static void MaskUnusedActions(ref IDiscreteActionMask actionMask, MLSam MLSam)
        {
            if (MLSam.mlState != ChoosingKind) for (int i = 1; i < OfferProviderCopy.ActionKinds.Length - 1; i++) actionMask.SetActionEnabled(0, i, false);
            if (MLSam.mlState != ChoosingTargetCell) for (int i = 1; i < OfferProviderCopy.ActorsCelID.Length - 1; i++) actionMask.SetActionEnabled(0, i, false);
            if (MLSam.mlState != ChoosingPieceType) for (int i = 1; i < OfferProviderCopy.TargetCellID.Length - 1; i++) actionMask.SetActionEnabled(0, i, false);
            if (MLSam.mlState != ChoosingWallConfig) for (int i = 1; i < OfferProviderCopy.PieceType.Length - 1; i++) actionMask.SetActionEnabled(0, i, false);
            if (MLSam.mlState != ChoosingInstakeCellID) for (int i = 1; i < OfferProviderCopy.WallConfig.Length - 1; i++) actionMask.SetActionEnabled(0, i, false);
        }    

    public static void ReceiveAction(ActionBuffers actions, MLSam MLSam)
    {
        if (MLSam.mlState == ChoosingKind) 
        {
            MLSam.ChosenAction[(int)ChoosingKind] = actions.DiscreteActions[(int)ChoosingKind];
        }
        switch (MLSam.ChosenAction[(int)ChoosingKind])
        {
            case CaptureVP:
            case EndTurn:
                {
                    for (int theAction = 0; theAction < MLSam.NumberOfOffers; theAction++)
                    {
                        if (MLSam.Offers[theAction].kind != MLSam.ChosenAction[(int)ChoosingKind]) continue;
                        peformAction(MLSam.Offers[theAction], MLSam);
                    }
                }
                break;
            case CoreDamage:
            case Explosive:
            case ConversionFactory:
                switch (MLSam.mlState)
                {
                    //I Need Just ActorsCell
                    case ChoosingKind:
                    {
                        MLSam.mlState = ChoosingActorsCell;
                        break;    
                    } 
                    case ChoosingActorsCell:
                    {
                        MLSam.ChosenAction[(int)ChoosingActorsCell] = actions.DiscreteActions[(int)ChoosingActorsCell];
                        for (int theAction = 0; theAction < MLSam.NumberOfOffers; theAction++)
                        {
                            if (MLSam.Offers[theAction].kind != MLSam.ChosenAction[(int)ChoosingKind]) continue;
                            if (MLSam.Offers[theAction].ActorsCellId != MLSam.ChosenAction[(int)ChoosingActorsCell]) continue;
                            peformAction(MLSam.Offers[theAction], MLSam);
                        }
                    }
                    break;
                }
                break;
            case Move:
            case Shoot:
            case Push:
            case Sniper:
            case SacrificeFactory:
            case NecroSpawn:
                switch (MLSam.mlState)
                {
                    //I Need ActorsCellID and TargetCell
                    case ChoosingKind:
                    {
                        MLSam.mlState = ChoosingActorsCell;
                        break;
                    } 
                    case ChoosingActorsCell:
                    {
                        MLSam.ChosenAction[(int)ChoosingActorsCell] = actions.DiscreteActions[(int)ChoosingActorsCell];
                        MLSam.mlState = ChoosingTargetCell;
                        break;
                    }
                    case ChoosingTargetCell:
                    {
                        MLSam.ChosenAction[(int)ChoosingTargetCell] = actions.DiscreteActions[(int)ChoosingTargetCell];
                        for (int theAction = 0; theAction < MLSam.NumberOfOffers; theAction++)
                        {
                            if (MLSam.Offers[theAction].kind != MLSam.ChosenAction[(int)ChoosingKind]) continue;
                            if (MLSam.Offers[theAction].ActorsCellId != MLSam.ChosenAction[(int)ChoosingActorsCell]) continue;
                            if (MLSam.Offers[theAction].TargetCellId != MLSam.ChosenAction[(int)ChoosingTargetCell]) continue;
                            peformAction(MLSam.Offers[theAction], MLSam);
                        }
                        break;
                    }
                }
                break;
            case Create:
                //I need TargetCell, PieceType, maybe WallConfig.
                switch (MLSam.mlState)
                {
                    case ChoosingKind:
                    {
                        MLSam.mlState = ChoosingTargetCell;
                        break;
                    } 
                    case ChoosingTargetCell:
                    {
                        MLSam.ChosenAction[(int)ChoosingTargetCell] = actions.DiscreteActions[(int)ChoosingTargetCell];
                        MLSam.mlState = ChoosingPieceType;
                        break;
                    }
                    case ChoosingPieceType:
                    {
                        MLSam.ChosenAction[(int)ChoosingPieceType] = actions.DiscreteActions[(int)ChoosingPieceType];
                        if (Piece.connectors_enabled[MLSam.ChosenAction[(int)ChoosingWallConfig]])
                        {
                            MLSam.mlState = ChoosingWallConfig;
                        }
                        else
                        {
                            MLSam.ChosenAction[(int)ChoosingTargetCell] = actions.DiscreteActions[(int)ChoosingTargetCell];
                            for (int theAction = 0; theAction < MLSam.NumberOfOffers; theAction++)
                            {
                                if (MLSam.Offers[theAction].kind != MLSam.ChosenAction[(int)ChoosingKind]) continue;
                                if (MLSam.Offers[theAction].pieceType != MLSam.ChosenAction[(int)ChoosingPieceType]) continue;
                                if (MLSam.Offers[theAction].TargetCellId != MLSam.ChosenAction[(int)ChoosingTargetCell]) continue;
                                peformAction(MLSam.Offers[theAction], MLSam);
                            }
                            break;
                        }
                        break;
                    }
                    case ChoosingWallConfig:
                    {
                        MLSam.ChosenAction[(int)ChoosingWallConfig] = actions.DiscreteActions[(int)ChoosingWallConfig];
                        for (int theAction = 0; theAction < MLSam.NumberOfOffers; theAction++)
                            {
                                if (MLSam.Offers[theAction].kind != MLSam.ChosenAction[(int)ChoosingKind]) continue;
                                if (MLSam.Offers[theAction].pieceType != MLSam.ChosenAction[(int)ChoosingPieceType]) continue;
                                if (MLSam.Offers[theAction].TargetCellId != MLSam.ChosenAction[(int)ChoosingTargetCell]) continue;
                                if (MLSam.Offers[theAction].aux != MLSam.ChosenAction[(int)ChoosingWallConfig]) continue;
                                peformAction(MLSam.Offers[theAction], MLSam);
                            }
                    }
                    break;
                }
                break;
            case Upgrade:
                //I need ActorsCellID, PieceType, maybe WallConfig.
                switch (MLSam.mlState)
                {
                    case ChoosingKind:
                    {
                        MLSam.mlState = ChoosingActorsCell;
                        break;
                    } 
                    case ChoosingActorsCell:
                    {
                        MLSam.ChosenAction[(int)ChoosingActorsCell] = actions.DiscreteActions[(int)ChoosingActorsCell];
                        MLSam.mlState = ChoosingPieceType;
                        break;
                    }
                    case ChoosingPieceType:
                    {
                        MLSam.ChosenAction[(int)ChoosingPieceType] = actions.DiscreteActions[(int)ChoosingPieceType];
                        for (int theAction = 0; theAction < MLSam.NumberOfOffers; theAction++)
                            {
                                if (MLSam.Offers[theAction].kind != MLSam.ChosenAction[(int)ChoosingKind]) continue;
                                if (MLSam.Offers[theAction].ActorsCellId != MLSam.ChosenAction[(int)ChoosingActorsCell]) continue;
                                if (MLSam.Offers[theAction].pieceType != MLSam.ChosenAction[(int)ChoosingPieceType]) continue;
                                peformAction(MLSam.Offers[theAction], MLSam);
                            }
                        break;
                    }  
                }
                break;
            case Spawner:
             //I need ActorsCellID, TargetCell, PieceType, maybe WallConfig.
                switch (MLSam.mlState)
                {
                    case ChoosingKind:
                        {
                            MLSam.mlState = ChoosingActorsCell;
                            break;
                        }
                    case ChoosingActorsCell:
                        {
                            MLSam.ChosenAction[(int)ChoosingActorsCell] = actions.DiscreteActions[(int)ChoosingActorsCell];
                            MLSam.mlState = ChoosingTargetCell;
                            break;
                        }
                    case ChoosingTargetCell:
                        {
                            MLSam.ChosenAction[(int)ChoosingTargetCell] = actions.DiscreteActions[(int)ChoosingTargetCell];
                            MLSam.mlState = ChoosingPieceType;
                            break;
                        }
                    case ChoosingPieceType:
                        {
                            MLSam.ChosenAction[(int)ChoosingPieceType] = actions.DiscreteActions[(int)ChoosingPieceType];
                            for (int theAction = 0; theAction < MLSam.NumberOfOffers; theAction++)
                            {
                                if (MLSam.Offers[theAction].kind != MLSam.ChosenAction[(int)ChoosingKind]) continue;
                                if (MLSam.Offers[theAction].ActorsCellId != MLSam.ChosenAction[(int)ChoosingActorsCell]) continue;
                                if (MLSam.Offers[theAction].TargetCellId != MLSam.ChosenAction[(int)ChoosingTargetCell]) continue;
                                if (MLSam.Offers[theAction].pieceType != MLSam.ChosenAction[(int)ChoosingPieceType]) continue;
                                peformAction(MLSam.Offers[theAction], MLSam);
                            }
                            break;
                        }
                        default:
                        {
                            Debug.LogWarning($"Unhandled ActionKind");
                            break;
                        }
                }
            break;
        }
        MLSam.TickMe();
    }

    private static void peformAction(Action theAction, MLSam MLSam)
    {
        var gameState = GameRegistry.game[MLSam.gameIndex].gameState;

        MLSam.mlState = ChoosingKind;
        Array.Clear(MLSam.ChosenAction, 0, MLSam.ChosenAction.Length);
        gameState.Perform(theAction, MLSam.Offers);
    }

    

}

public struct RewardsTuning
{
    public float rewardWin, rewardLoss, rewardDraw, rewardCaptureVP, rewardCoreDamage;
    public float moveTowardVpScale, costPenaltyScale, stepPenalty, endTurnPenalty;
}
