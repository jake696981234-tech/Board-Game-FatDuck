using UnityEngine;

public class MLLeague
{
    private LeagueConfig leagueConfig;
    public void Init(LeagueConfig theLeagueConfig)
    {
        leagueConfig = theLeagueConfig;
    }

    public string[] MLFilePath;
    public string[] MLName;

    public void SetPlayers()
    {
        // CheckIfAgentHasreachedTargetAndIfSo, Change the trained Agent. // If changing trained ML agent, I must find a way to communicate that to the ml agent package system

        //PickIts3Opponents

        // 
    }

    public void SetWhoControlsPlayers(byte seat)
    {
        setLearningPlayer(seat);
        setThreeOtherPlayers();
    }

    private void setThreeOtherPlayers()
    {
        
    }

    private void setLearningPlayer(byte seat)
    {
        var go = new GameObject($"MLAgent_Player_{seat}");

        // --- Auto inject Behavior Parameters based on config ---
        var bp = go.AddComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        bp.BehaviorName = GameBootstrapper.hub.mlBehavior.name;
        bp.UseChildSensors = GameBootstrapper.hub.mlBehavior.useChildSensors;
        bp.BrainParameters.VectorObservationSize = GameBootstrapper.hub.mlBehavior.obsSize;
        bp.BrainParameters.ActionSpec = Unity.MLAgents.Actuators.ActionSpec.MakeDiscrete(GameBootstrapper.hub.mlBehavior.actionBranchSize);
        bp.TeamId = (seat < GameBootstrapper.hub.player_team.Length)
            ? GameBootstrapper.hub.player_team[seat]
            : seat;
        // Now add the Agent so Awake() reads the configured BehaviorParameters
        var ml = go.AddComponent<MLSam>();
        // mlControllers[seat] = ml;
    }
}
