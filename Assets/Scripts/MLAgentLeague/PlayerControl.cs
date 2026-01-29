using UnityEngine;
using Game.Core;
using System;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;
using static Info.ControlMode;



public class PlayerControl
{
    public PlayerManager playerManager;
    private int gameIndex;
    private GameState gameState;
    private bool isLearning = false;
    private Info.ControlMode playerType;
    private MLSam MLsam;
    private BehaviorParameters MyMLParamters;
    GameObject MLObjectRoot;
    private DumbGregBotPolicy dumbGreg;
    
    public byte playerIndex;
    public void tickPlayer(int playerId, GameState gameState)
    {
        if (playerIndex != playerId) return;
        switch (playerType)
        {
            case DumbGreg:
                gameState.Perform(dumbGreg.bot.Offers[dumbGreg.PickAction()], dumbGreg.bot.Offers);
                break;
            case FrozenML:
            case LearningML:
                MLsam.RequestDecision();
                break;
            default:
                Debug.LogWarning($"This is Broken!");
                break;
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

    

 
    public void init(Info.ControlMode thePlayerType, byte thePlayerIndex, int theGameIndex, PlayerManager thePlayerManager, ModelAsset MyBrain = null, string behaviorName = null)
    {
        gameIndex = theGameIndex;
        playerManager = thePlayerManager;
        gameState = GameRegistry.game[gameIndex].gameState;
        playerIndex = thePlayerIndex;
        playerType = thePlayerType; 

        subscribePlayer();

        switch(playerType)
        {
            case FrozenML:
                initAsMLFrozenBrain(MyBrain);
                break;
            case LearningML:
                initAsMLLearning(behaviorName);
                break;
            case DumbGreg:
                initAsDumbGreg();
                break;
        }
    }
   

    private void initAsDumbGreg()
    {
        var bot = new Bot(playerIndex, gameIndex);
        dumbGreg = new DumbGregBotPolicy(bot);
    }

    private void initAsMLFrozenBrain(ModelAsset MyBrain)
    {
        MLsam.bot = new Bot(playerIndex, gameIndex);
        prepMLBot();
        MLsam = new MLSam();
        isLearning = false;

        MyMLParamters.BehaviorName = Info.behaviorName; // this might not need this
        MyMLParamters.DeterministicInference = true;
        MyMLParamters.BehaviorType = BehaviorType.InferenceOnly;
        MyMLParamters.Model = MyBrain;
    }
    private void initAsMLLearning(string behaviorName)
    {
        MLsam = new MLSam();
        isLearning = true;

        MyMLParamters.BehaviorName = behaviorName;
        MyMLParamters.DeterministicInference = false;
        MLsam.bot = new Bot(playerIndex, gameIndex); 
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

    

    public struct BootSpecficPlayerInfo
    {
        public ModelAsset MyBrain;
        public string behaviorName;
    }

}