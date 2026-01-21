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

public class MLSam : Agent
{
    [Header("Identity")]
    [SerializeField] public byte playerId = 0; // 0..3
    public Action[] Offers;
    public int NumberOfOffers;
    public float[] Quoted;
    public byte[] ActionMask;
    public int gameIndex;
    public int[] ChosenAction = new int[6];
    public float[] Observations;
    public int Count;
    public int MaxPlayers;
    public RewardsTuning rewards;
    public MLActions.MLState mlState = ChoosingKind;
    

    public void init() // to do
    {
        
    }

    

    public override void OnActionReceived(ActionBuffers actions)
    {
        MLActions.ReceiveAction(actions, this);
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        MLActions.GiveMeDescreteMask(ref actionMask, this);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        MLObservation.GiveMeObservations(sensor, this);
    }

    public void ApplyTerminal(byte winner)
    {
        if (winner == 255 || winner >= MaxPlayers)
        {
            AddReward(rewards.rewardDraw);
        }
        else if (winner == playerId)
        {
            AddReward(rewards.rewardWin);
        }
        else
        {
            AddReward(rewards.rewardLoss);
        }
        EndEpisode();
    }     


    
}
