using UnityEngine;
using Game.Core;
using System;
using Unity.MLAgents.Policies;
using static PlayerManager.PlayerType;
using Unity.InferenceEngine;


public class PlayerControl
{
    private bool isLearning = false;


    private PlayerManager.PlayerType playerType;
    private MLSam MLsam = new MLSam();
    private BehaviorParameters MyMLParamters;
    GameObject MLObjectRoot;
    private DumbGregBotPolicy dumbGreg = new DumbGregBotPolicy();
    public PlayerManager playerManager;
    public int playerIndex;
    public void tickPlayer(int playerId)
    {
        if (playerIndex != playerId) return;
        switch (playerType)
        {
            case DumbGreg:
                dumbGreg.DecideAndAct();
                break;
            case ML:
                MLsam.RequestDecision();
                break;
            default:
                {
                    Debug.LogWarning($"This is Broken!");
                    break;
                }
        }
    }

    public void subscribePlayer()
    {
        playerManager.TickPlayerIndex += tickPlayer;
    }
    public void unSubscribePlayer()
    {
        playerManager.TickPlayerIndex -= tickPlayer;
    }

    public void OnTerminal(byte winner)
    {
        if (isLearning) MLsam.ApplyTerminal(winner);
    }

 

    public void start()
    {
        prepMLBot();
    }

    public void initAsDumbGreg(int PlayerIndex)
    {
        playerIndex = PlayerIndex;
        playerType = DumbGreg; //to do- finish this method
    }



    public void initAsMLFrozenBrain(ModelAsset MyBrain, int PlayerIndex)
    {
        playerIndex = PlayerIndex;
        isLearning = false;

        MyMLParamters.BehaviorName = Info.behaviorName; // this might not need this
        MyMLParamters.DeterministicInference = true;
        MyMLParamters.BehaviorType = BehaviorType.InferenceOnly;
        MyMLParamters.Model = MyBrain;   
        MLsam.init();
    }
    public void initAsMLLearning(string BehaviorName, int PlayerIndex)
    {
        playerIndex = PlayerIndex;
        isLearning = true;

        MyMLParamters.BehaviorName = Info.behaviorName;
        MyMLParamters.DeterministicInference = false;
        MLsam.init();
    }

    private void prepMLBot()
    {
        MLObjectRoot = new GameObject($"MLAgent_Player_{playerIndex}");
        setGeneralBehaviorParameters(playerIndex, MLObjectRoot);
        MLsam = MLObjectRoot.AddComponent<MLSam>();
    }

    private void setGeneralBehaviorParameters(int seat, GameObject MLObjectRoot)
    {
        MyMLParamters = MLObjectRoot.AddComponent<BehaviorParameters>();
        MyMLParamters.UseChildSensors = Info.useChildSensors;
        MyMLParamters.BrainParameters.VectorObservationSize = Info.vectorObservationSize;
        MyMLParamters.BrainParameters.ActionSpec = Unity.MLAgents.Actuators.ActionSpec.MakeDiscrete(Info.actionBranchSize);
        MyMLParamters.TeamId = Info.Players[seat].team;

    }



}