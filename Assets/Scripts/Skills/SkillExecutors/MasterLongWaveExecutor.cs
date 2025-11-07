using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "MasterLongWaveExecutor", menuName = "Skills/Effects/Master Long Wave")]
public class MasterLongWaveExecutor : SkillExecutor
{
    public static int rotateDir = 1;
    public float waveDuration = 3f;
    public GameObject vfxPrefab;
    public AudioClip energyWaveAccumulateSound;
    public AudioClip energyWaveShootSound;
    public GameObject energyWavePrefab;
    private CharacterBaseAI aiScript = null;
    private Vector2 lookInput = Vector2.zero;

    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");

        aiScript = playerObj.GetComponent<CharacterBaseAI>();
        if (aiScript.ActiveSkillCoroutine != null) return;
        lookInput = aiScript.characterInput.LookInput;
        if (lookInput.sqrMagnitude < 0.1f) lookInput = aiScript.LookDir;
        var tarEnemy = CharacterManager.Instance.FindNearestEnemyInAngle(playerObj, aiScript.LookDir, 180);
        int count = 1;
        float hpRatio = (float)aiScript.characterStatus.State.CurrentHp / aiScript.characterStatus.State.MaxHp;
        if (hpRatio < 0.5f) count = 9;
        aiScript.ActiveSkillCoroutine = aiScript.StartCoroutine(Attack_EnergyWave(playerObj, tarEnemy, count));
    }

    private bool IsMasterLong()
    {
        return aiScript.CharacterData.CharacterType == CharacterType.Boss_2_0_MasterTurtle;
    }
    

    private IEnumerator Attack_EnergyWave(GameObject owner, GameObject target, int count)
    {
        aiScript.isAttack = true;
        GameObject vfx;
        if (IsMasterLong())
        {
            aiScript.animator.speed = 1;
            aiScript.animator.Play("施法并扔出");
            yield return new WaitForSeconds(0.5f);
            vfx = aiScript.transform.GetChild(0).GetChild(0).gameObject;
            vfx.SetActive(true);
        }
        else
        {
            vfx = LevelManager.Instance.InstantiateTemporaryObject(vfxPrefab, aiScript.transform.position);
            aiScript.TobeDestroyed.Add(vfx);
        }
        if (energyWaveAccumulateSound)
        {
            if (aiScript.OneShotAudioSource == null)
                aiScript.OneShotAudioSource = aiScript.gameObject.AddComponent<AudioSource>();
            aiScript.OneShotAudioSource.PlayOneShot(energyWaveAccumulateSound);
        }
        float waitTime = 1.6f;
        float tarTime = Time.time + waitTime;
        while (Time.time < tarTime)
        {
            if (!IsMasterLong())
            {
                vfx.transform.position = aiScript.transform.position;
            }
            yield return null;
        }
        if (aiScript.isAi && target != null && LevelManager.Instance.InSameRoom(owner, target))
            lookInput = aiScript.characterInput.LookInput = target.transform.position - aiScript.transform.position;
        if (aiScript.isAi)
        {
            // 攻击0.5s之前的位置，给玩家一些缓冲时间
            yield return new WaitForSeconds(0.5f);
        }
        if (IsMasterLong())
        {
            vfx.SetActive(false);
        }
        else
        {
            Destroy(vfx);
            aiScript.TobeDestroyed.Remove(vfx);
        }

        // 获取Player碰撞体的边界位置
        Bounds playerBounds = aiScript.GetComponentInChildren<Collider2D>().bounds;

        float angle = 360f / count;
        Quaternion rotationPlus = Quaternion.Euler(0, 0, angle);
        for (int i = 0; i < count; i++)
        {
            // 计算子弹的初始位置，稍微偏离玩家边界
            Vector2 waveOffset = lookInput.normalized * (playerBounds.extents.magnitude + 0.1f);
            Vector2 waveStartPosition =aiScript.transform.position;
            waveStartPosition += waveOffset;

            var energeWave = LevelManager.Instance.InstantiateTemporaryObject(energyWavePrefab, waveStartPosition);
            EnergyWave energyWaveScript = energeWave.GetComponent<EnergyWave>();
            energyWaveScript.PosOffset = waveOffset;
            energyWaveScript.Direction = lookInput.normalized;
            energyWaveScript.OwnerStatus = aiScript.characterStatus;
            energyWaveScript.Rotate = count > 1 ? rotateDir : 0;

            lookInput = rotationPlus * lookInput;
            Destroy(energeWave, waveDuration);
        }
        rotateDir = -rotateDir;

        if (energyWaveShootSound)
        {
            if (aiScript.OneShotAudioSource == null)
                aiScript.OneShotAudioSource = aiScript.gameObject.AddComponent<AudioSource>();
            aiScript.OneShotAudioSource.PlayOneShot(energyWaveShootSound);
        }

        yield return new WaitForSeconds(waveDuration);

        aiScript.isAttack = false;
        if (IsMasterLong())
        {
            if (count > 1)
            {
                aiScript.animator.Play("闪到老腰");
                yield return new WaitForSeconds(3f);
            }
            aiScript.animator.Play("Mutant Walking");
        }

        // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
        if (aiScript.isAi)
        {
            // 这时候 Coroutine 还不是null，所以不会再次进入攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
        }

        aiScript.ActiveSkillCoroutine = null;
    }
}