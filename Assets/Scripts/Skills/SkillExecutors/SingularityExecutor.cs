using UnityEngine;

[CreateAssetMenu(fileName = "SingularityExecutor", menuName = "Skills/Effects/Singularity")]
public class SingularityExecutor : SkillExecutor
{
    public float damagePerSecond;
    public float damageInterval;
    public float totalInterval;
    public GameObject blackHolePrefab;

    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");

        // 在玩家位置生成黑洞
        Vector3 spawnPosition = playerObj.transform.position;
        GameObject blackHole = LevelManager.Instance.InstantiateTemporaryObject(blackHolePrefab, spawnPosition);
        LevelManager.Instance.BlackHole = blackHole;
        var blackHoleComponent = blackHole.GetComponent<BlackHole>();
        if (blackHoleComponent != null)
        {
            blackHoleComponent.DamagePerSecond = damagePerSecond;
            blackHoleComponent.DamageInterval = damageInterval;
            blackHoleComponent.TotalInterval = totalInterval;
            blackHoleComponent.Owner = playerObj;
            blackHoleComponent.StartDamageCoroutine();
        }
    }
}