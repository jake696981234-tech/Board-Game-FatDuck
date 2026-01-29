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
    public Bot bot;
    public float[] Observations;
    public int Count;
    public RewardsTuning rewards;
    public int[] ChosenAction = new int[6];
    public MLActions.MLState mlState = ChoosingKind;

    public void TickMe()
    {
        RequestDecision();
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
        if (winner == 255 || winner >= Info.playerCount)
        {
            AddReward(rewards.rewardDraw);
        }
        else if (winner == bot.playerId)
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
