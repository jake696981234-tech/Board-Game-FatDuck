using UnityEngine;
using System.Collections.Generic;


[CreateAssetMenu(fileName = "CurriculumConfig", menuName = "Game/CurriculumConfig", order = 2)]
public class PerGameConfig : ScriptableObject
{

    [Header("Curriculum Config")]
    public List<restrictionGoal> Curriculum = new();

    public whichPlayerToTrain playerToTrain;



    [Header("Game Authoring")]
    public bool pieceLimitEnabled = false;

    [Min(1)] public int pieceLimit = 50;




}



public enum whichPlayerToTrain
{
    player0,
    player1,
    player2,
    player3,
    non
}

public enum curriculumRestriction
{
    onePiece,
    twoPlayer,
    oneAction, //this should just make the secound action mask out everything but endturn
}

[System.Serializable]
public class restrictionGoal
{
    public curriculumRestriction restriction;
    public int goalRequirement;
}

