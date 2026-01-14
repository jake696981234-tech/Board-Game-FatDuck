using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Game.Core; // Action, OfferQuery, GameState, PlayerState
using static theMLSam.MLState;
using System.Collections.Generic;
using Action = Game.Core.Action;
using static Game.Core.ActionKind; // import enum values

public class theMLSam : MLAgentController
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

    public int[] ChosenAction = new int[6];

    private void peformAction(Action theAction)
    {
        mlState = ChoosingKind;
        Array.Clear(ChosenAction, 0, ChosenAction.Length);
        _gs.Perform(theAction, _offers);
    }

    MLState mlState = ChoosingKind;

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (mlState == ChoosingKind) 
        {
            ChosenAction[(int)ChoosingKind] = actions.DiscreteActions[(int)ChoosingKind];
        }
        switch (ChosenAction[(int)ChoosingKind])
        {
            case CaptureVP:
            case EndTurn:
                {
                    for (int theAction = 0; theAction < _emitCount; theAction++)
                    {
                        if (_offers[theAction].kind != ChosenAction[(int)ChoosingKind]) continue;
                        peformAction(_offers[theAction]);
                    }
                }
                break;
            case CoreDamage:
            case Explosive:
            case ConversionFactory:
                switch (mlState)
                {
                    //I Need Just ActorsCell
                    case ChoosingKind:
                    {
                        mlState = ChoosingActorsCell;
                        break;    
                    } 
                    case ChoosingActorsCell:
                    {
                        ChosenAction[(int)ChoosingActorsCell] = actions.DiscreteActions[(int)ChoosingActorsCell];
                        for (int theAction = 0; theAction < _emitCount; theAction++)
                        {
                            if (_offers[theAction].kind != ChosenAction[(int)ChoosingKind]) continue;
                            if (_offers[theAction].ActorsCellId != ChosenAction[(int)ChoosingActorsCell]) continue;
                            peformAction(_offers[theAction]);
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
                switch (mlState)
                {
                    //I Need ActorsCellID and TargetCell
                    case ChoosingKind:
                    {
                        mlState = ChoosingActorsCell;
                        break;
                    } 
                    case ChoosingActorsCell:
                    {
                        ChosenAction[(int)ChoosingActorsCell] = actions.DiscreteActions[(int)ChoosingActorsCell];
                        mlState = ChoosingTargetCell;
                        break;
                    }
                    case ChoosingTargetCell:
                    {
                        ChosenAction[(int)ChoosingTargetCell] = actions.DiscreteActions[(int)ChoosingTargetCell];
                        for (int theAction = 0; theAction < _emitCount; theAction++)
                        {
                            if (_offers[theAction].kind != ChosenAction[(int)ChoosingKind]) continue;
                            if (_offers[theAction].ActorsCellId != ChosenAction[(int)ChoosingActorsCell]) continue;
                            if (_offers[theAction].TargetCellId != ChosenAction[(int)ChoosingTargetCell]) continue;
                            peformAction(_offers[theAction]);
                        }
                        break;
                    }
                }
                break;
            case Create:
                //I need TargetCell, PieceType, maybe WallConfig.
                switch (mlState)
                {
                    case ChoosingKind:
                    {
                        mlState = ChoosingTargetCell;
                        break;
                    } 
                    case ChoosingTargetCell:
                    {
                        ChosenAction[(int)ChoosingTargetCell] = actions.DiscreteActions[(int)ChoosingTargetCell];
                        mlState = ChoosingPieceType;
                        break;
                    }
                    case ChoosingPieceType:
                    {
                        ChosenAction[(int)ChoosingPieceType] = actions.DiscreteActions[(int)ChoosingPieceType];
                        if (Piece.connectors_enabled[ChosenAction[(int)ChoosingWallConfig]])
                        {
                            mlState = ChoosingWallConfig;
                        }
                        else
                        {
                            ChosenAction[(int)ChoosingTargetCell] = actions.DiscreteActions[(int)ChoosingTargetCell];
                            for (int theAction = 0; theAction < _emitCount; theAction++)
                            {
                                if (_offers[theAction].kind != ChosenAction[(int)ChoosingKind]) continue;
                                if (_offers[theAction].pieceType != ChosenAction[(int)ChoosingPieceType]) continue;
                                if (_offers[theAction].TargetCellId != ChosenAction[(int)ChoosingTargetCell]) continue;
                                peformAction(_offers[theAction]);
                            }
                            break;
                        }
                        break;
                    }
                    case ChoosingWallConfig:
                    {
                        ChosenAction[(int)ChoosingWallConfig] = actions.DiscreteActions[(int)ChoosingWallConfig];
                        for (int theAction = 0; theAction < _emitCount; theAction++)
                            {
                                if (_offers[theAction].kind != ChosenAction[(int)ChoosingKind]) continue;
                                if (_offers[theAction].pieceType != ChosenAction[(int)ChoosingPieceType]) continue;
                                if (_offers[theAction].TargetCellId != ChosenAction[(int)ChoosingTargetCell]) continue;
                                if (_offers[theAction].aux != ChosenAction[(int)ChoosingWallConfig]) continue;
                                peformAction(_offers[theAction]);
                            }
                    }
                    break;
                }
                break;
            case Upgrade:
                //I need ActorsCellID, PieceType, maybe WallConfig.
                switch (mlState)
                {
                    case ChoosingKind:
                    {
                        mlState = ChoosingActorsCell;
                        break;
                    } 
                    case ChoosingActorsCell:
                    {
                        ChosenAction[(int)ChoosingActorsCell] = actions.DiscreteActions[(int)ChoosingActorsCell];
                        mlState = ChoosingPieceType;
                        break;
                    }
                    case ChoosingPieceType:
                    {
                        ChosenAction[(int)ChoosingPieceType] = actions.DiscreteActions[(int)ChoosingPieceType];
                        for (int theAction = 0; theAction < _emitCount; theAction++)
                            {
                                if (_offers[theAction].kind != ChosenAction[(int)ChoosingKind]) continue;
                                if (_offers[theAction].ActorsCellId != ChosenAction[(int)ChoosingActorsCell]) continue;
                                if (_offers[theAction].pieceType != ChosenAction[(int)ChoosingPieceType]) continue;
                                peformAction(_offers[theAction]);
                            }
                        break;
                    }  
                }
                break;
            case Spawner:
             //I need ActorsCellID, TargetCell, PieceType, maybe WallConfig.
                switch (mlState)
                {
                    case ChoosingKind:
                        {
                            mlState = ChoosingActorsCell;
                            break;
                        }
                    case ChoosingActorsCell:
                        {
                            ChosenAction[(int)ChoosingActorsCell] = actions.DiscreteActions[(int)ChoosingActorsCell];
                            mlState = ChoosingTargetCell;
                            break;
                        }
                    case ChoosingTargetCell:
                        {
                            ChosenAction[(int)ChoosingTargetCell] = actions.DiscreteActions[(int)ChoosingTargetCell];
                            mlState = ChoosingPieceType;
                            break;
                        }
                    case ChoosingPieceType:
                        {
                            ChosenAction[(int)ChoosingPieceType] = actions.DiscreteActions[(int)ChoosingPieceType];
                            for (int theAction = 0; theAction < _emitCount; theAction++)
                            {
                                if (_offers[theAction].kind != ChosenAction[(int)ChoosingKind]) continue;
                                if (_offers[theAction].ActorsCellId != ChosenAction[(int)ChoosingActorsCell]) continue;
                                if (_offers[theAction].TargetCellId != ChosenAction[(int)ChoosingTargetCell]) continue;
                                if (_offers[theAction].pieceType != ChosenAction[(int)ChoosingPieceType]) continue;
                                peformAction(_offers[theAction]);
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
        //Prompt Agent for a action      
    }

    public readonly HashSet<int> ActionOriginal = new HashSet<int>();
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        ActionOriginal.Clear();
        _emitCount = BuildOffersForCurrentPlayer();

        switch (mlState) 
        {
             case ChoosingKind:
                {
                    MaskKinds(ref actionMask);
                    break;
                }
            case ChoosingActorsCell:
                {
                    MaskActorsCell(ref actionMask);
                    break;
                }
            case ChoosingTargetCell:
                {
                    MaskTargetCell(ref actionMask);
                    break;
                }
            case ChoosingPieceType:
                {
                    MaskPieceType(ref actionMask);
                    break;
                }
            case ChoosingWallConfig:
                {
                    MaskWallConfig(ref actionMask);
                    break;
                }
            case ChoosingInstakeCellID:
                {
                    MaskInstakeCellID(ref actionMask);
                    break;
                }
        }
        MaskUnusedActions(ref actionMask);
    }







    private void MaskKinds(ref IDiscreteActionMask actionMask)
    {
        for (int theAction = 0; theAction < _emitCount; theAction++) { if (!ActionOriginal.Add(_offers[theAction].kind) || _mask[theAction] == 0) actionMask.SetActionEnabled((int)ChoosingKind, _offers[theAction + 1].kind, false); }
    }
    private void MaskActorsCell(ref IDiscreteActionMask actionMask)
    {
        for (int theAction = 0; theAction < _emitCount; theAction++) 
        { 
            if (ActionOriginal.Add(_offers[theAction].ActorsCellId) && _mask[theAction] != 0 && _offers[theAction].kind == ChosenAction[0]) continue;
            actionMask.SetActionEnabled((int)ChoosingActorsCell, _offers[theAction + 1].ActorsCellId, false); 
        }
    }
    private void MaskTargetCell(ref IDiscreteActionMask actionMask)
    {
        for (int theAction = 0; theAction < _emitCount; theAction++) 
        { 
            if (ActionOriginal.Add(_offers[theAction].TargetCellId) && _mask[theAction] != 0 && _offers[theAction].kind == ChosenAction[0] && _offers[theAction].ActorsCellId == ChosenAction[1]) continue;
            actionMask.SetActionEnabled((int)ChoosingTargetCell, _offers[theAction + 1].TargetCellId, false); 
        }
    }
    private void MaskPieceType(ref IDiscreteActionMask actionMask)
    {
        for (int theAction = 0; theAction < _emitCount; theAction++) 
        { 
            if (ActionOriginal.Add(_offers[theAction].pieceType) && _mask[theAction] != 0 && _offers[theAction].kind == ChosenAction[0] && _offers[theAction].ActorsCellId == ChosenAction[1] && _offers[theAction].TargetCellId == ChosenAction[2]) continue;
            actionMask.SetActionEnabled((int)ChoosingPieceType, _offers[theAction + 1].pieceType, false); 
        }
    }
    private void MaskWallConfig(ref IDiscreteActionMask actionMask)
    {
        for (int theAction = 0; theAction < _emitCount; theAction++) 
        { 
            if (ActionOriginal.Add(_offers[theAction].pieceType) && _mask[theAction] != 0 && _offers[theAction].kind == ChosenAction[0] && _offers[theAction].ActorsCellId == ChosenAction[1] && _offers[theAction].TargetCellId == ChosenAction[2] && _offers[theAction].pieceType == ChosenAction[3]) continue;
            actionMask.SetActionEnabled((int)ChoosingWallConfig, _offers[theAction + 1].aux, false); 
        }
    }
    private void MaskInstakeCellID(ref IDiscreteActionMask actionMask)
    {
        
    }

    private void MaskUnusedActions(ref IDiscreteActionMask actionMask)
        {
            if (mlState != ChoosingKind) for (int i = 1; i < OfferProviderCopy.ActionKinds.Length - 1; i++) actionMask.SetActionEnabled(0, i, false);
            if (mlState != ChoosingTargetCell) for (int i = 1; i < OfferProviderCopy.ActorsCelID.Length - 1; i++) actionMask.SetActionEnabled(0, i, false);
            if (mlState != ChoosingPieceType) for (int i = 1; i < OfferProviderCopy.TargetCellID.Length - 1; i++) actionMask.SetActionEnabled(0, i, false);
            if (mlState != ChoosingWallConfig) for (int i = 1; i < OfferProviderCopy.PieceType.Length - 1; i++) actionMask.SetActionEnabled(0, i, false);
            if (mlState != ChoosingInstakeCellID) for (int i = 1; i < OfferProviderCopy.WallConfig.Length - 1; i++) actionMask.SetActionEnabled(0, i, false);
        }        
}
