using UnityEngine;

public abstract class SkillExecutor : ScriptableObject
{
    public abstract void ExecuteSkill(GameObject user, SkillData skillData);
}
