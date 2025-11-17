using UnityEngine;
using System.Collections.Generic;


[CreateAssetMenu(fileName = "CurriculumConfig", menuName = "Game/CurriculumConfig", order = 2)]
public class CurriculumConfig : ScriptableObject
{
    public List<restrictionGoal> RestrictionGoal = new();
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


