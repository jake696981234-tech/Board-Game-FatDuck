// Assets/Scripts/Core/Config.cs
using UnityEngine;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;
using System.Collections.Generic;


[CreateAssetMenu(fileName = "LeagueConfig", menuName = "Game/LeagueConfig", order = 4)]
public class LeagueConfig : ScriptableObject
{
    public int HowManyGamesForEachBot;
    public int HowManyBotsToTrain;

    
}
