using UnityEngine;

public static class MLLeague
{
    private static LeagueConfig leagueConfig;
    public static void Init(LeagueConfig theLeagueConfig)
    {
        leagueConfig = theLeagueConfig;
    }

    public static string[] MLFilePath;
    public static string[] MLName;

    public static void SetPlayers()
    {
        // CheckIfAgentHasreachedTargetAndIfSo, Change the trained Agent. // If changing trained ML agent, I must find a way to communicate that to the ml agent package system

        //PickIts3Opponents

        // 

    }

}
