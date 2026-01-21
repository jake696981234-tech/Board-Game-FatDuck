// Assets/Scripts/Core/Config.cs
using UnityEngine;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;
using System.Collections.Generic;


[CreateAssetMenu(fileName = "LeagueConfig", menuName = "Game/LeagueConfig", order = 4)]
public class LeagueConfig : ScriptableObject
{
    public bool EnableMLLeague;

    public enum GraduationRequirment
    {
        WinANumberOfGames = 0,
        PlayerANumberOfGames = 1,
    }
    public GraduationRequirment graduationRequirment;
    public int HowManyGamesPlayedToGraduate = 10000;
    public int HowManyGamesWonToGraduate = 100;

    public int HowManyBotsToTrain;


    [Header("Brains to Learn")]
    public string[] LearningPlayersBehaviorNames;

    [Header("Pool of Enemys")]
    public List<DumbGregAuthoring[]> DumbGregs;
     //add other bot policys here if wanted
    public List<ModelAsset> FrozenBrians = new(); // need way to add the generated brains to here during runtime.
   


    
    


    // public struct LearningPlayers
    

}



