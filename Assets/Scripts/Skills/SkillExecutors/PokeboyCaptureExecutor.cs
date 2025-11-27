using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "PokeboyCaptureExecutor", menuName = "Skills/Effects/12 Pokeboy Capture")]
public class PokeboyCaptureExecutor : SkillExecutor
{
    public float captureHpRatio = 0.2f;
    public int maxPokeMinionCount = 6;
    public int pokeMinionRebornTime = 15;
    public GameObject summonPokeEffectPrefab;
    public GameObject capturedMinionCanvas;
    public GameObject pokeballPrefab;
    private CharacterBaseAI aiScript = null;
    private Vector2 lookInput = Vector2.zero;

    public override void ExecuteSkill(GameObject playerObj, SkillData skillData)
    {
        Debug.Log($"{playerObj.name} uses {skillData.skillName}!");

        aiScript = playerObj.GetComponent<CharacterBaseAI>();

        if (aiScript.UpdateExistingPokesCoroutine == null)
        {
            aiScript.UpdateExistingPokesCoroutine = aiScript.StartCoroutine(aiScript.UpdateExistingPokes(pokeMinionRebornTime));
        }

        if (aiScript.ActiveSkillCoroutine != null) return;

        lookInput = aiScript.characterInput.LookInput;
        if (lookInput.sqrMagnitude < 0.1f) lookInput = aiScript.LookDir;
        GameObject tarEnemy = null;
        if (!aiScript.isAi)
            tarEnemy = CharacterManager.Instance.FindNearestMinionInAngleWithHpRatio(playerObj, aiScript.LookDir, 180, true, captureHpRatio);

        string info = "";
        if (tarEnemy != null)
        {
            // Throw poke ball to capture and then this one will be summoned
            // TakeDamage
            var tarStatus = tarEnemy.GetComponent<CharacterStatus>();
            // Capture
            aiScript.ActiveSkillCoroutine = aiScript.StartCoroutine(CaptureCompanion(tarStatus));
        }
        else
        {
            info += $"No enemy's health is below {captureHpRatio * 100:F0}%.\n";
            if (aiScript.PokeMinionPrefabs.Count == 0)
            {
                info += "You have no companion now.\n";
                aiScript.characterStatus.State.ActiveSkillCurCd = skillData.cooldown;
                UIManager.Instance.UpdateMyStatusUI(aiScript.characterStatus);
            }
            else
            {
                var (aliveNotSummonedPokePrefabs, minReviveTime, minReviveIdx) = aiScript.GetAliveNotSummonedPokePrefabs();
                if (aliveNotSummonedPokePrefabs.Count > 0)
                {
                    aiScript.ActiveSkillCoroutine = aiScript.StartCoroutine(SummonPokes(aliveNotSummonedPokePrefabs, lookInput));
                    info = "";
                    if (minReviveTime < pokeMinionRebornTime)
                        info += $"Companion # {minReviveIdx} will revive in {minReviveTime:F1} seconds";
                }
                else
                {
                    if (minReviveTime < pokeMinionRebornTime)
                        info += $"Companion # {minReviveIdx} will revive in {minReviveTime:F1} seconds";
                    else
                        info += "All companions are by your side.";
                    aiScript.characterStatus.State.ActiveSkillCurCd = skillData.cooldown;
                    UIManager.Instance.UpdateMyStatusUI(aiScript.characterStatus);
                }
            }
        }

        if (info != "")
            UIManager.Instance.ShowInfoPanel(info, Color.white, 3);
    }

    private IEnumerator CaptureCompanion(CharacterStatus enemy)
    {
        aiScript.isAttack = true;

        GameObject pokeball;
        float holdBallTime = 0.87f;

        var lookInput = enemy.transform.position - aiScript.transform.position;
        if (aiScript.CharacterData.Is3DModel())
        {
            aiScript.RotateTo(aiScript.LookToForwardDir, lookInput);
            aiScript.PlayAnimationAllLayers("Throw Object");
            var rightHandTransform = aiScript.animator.GetBoneTransform(HumanBodyBones.RightHand);
            pokeball = Instantiate(pokeballPrefab, rightHandTransform);
            pokeball.transform.localScale = Vector3.one * 0.3f
                * aiScript.transform.lossyScale.x / pokeball.transform.lossyScale.x;
            yield return new WaitForSeconds(holdBallTime);
        }
        else
        {
            aiScript.RotateTo(Vector3.forward, lookInput);
            pokeball = Instantiate(pokeballPrefab, aiScript.transform);
            Vector2 pos = pokeball.transform.position;
            pokeball.transform.position = pos + aiScript.characterInput.LookInput.normalized
                * (aiScript.col2D.bounds.extents.magnitude + 0.1f);
            pokeball.transform.localScale = Vector3.one * 0.3f;
        }
        aiScript.TobeDestroyed.Add(pokeball);

        float throwBallTime = 1.13f;
        var startPos = pokeball.transform.position;
        float elapsedTime = 0;
        while (elapsedTime < throwBallTime)
        {
            elapsedTime += Time.deltaTime;
            pokeball.transform.position = Vector3.Lerp(startPos, enemy.transform.position, elapsedTime / throwBallTime);
            yield return null;
        }
        pokeball.transform.position = enemy.transform.position;
        Destroy(pokeball);
        aiScript.TobeDestroyed.Remove(pokeball);

        var prefabInfo = CharacterManager.Instance.minionPrefabInfos[enemy.State.PlayerId];
        var levelData = GameManager.Instance.GetStageConfig(prefabInfo.StageId).stageData;
        var minionPrefab = levelData.normalMinionPrefabs[prefabInfo.PrefabId];

        if (aiScript.characterStatus.State.CatchedMinions.Count == maxPokeMinionCount)
        {
            aiScript.characterStatus.State.CatchedMinions[aiScript.CircularIdx] = prefabInfo;
            aiScript.characterStatus.State.CatchedMinionStates[aiScript.CircularIdx] = enemy.State.Clone();
            aiScript.PokeMinionPrefabs[aiScript.CircularIdx] = minionPrefab;
            aiScript.PokeMinionReviveTime[aiScript.CircularIdx] = 0;
            aiScript.CircularIdx = (aiScript.CircularIdx + 1) % maxPokeMinionCount;
        }
        else
        {
            aiScript.characterStatus.State.CatchedMinions.Add(prefabInfo);
            aiScript.characterStatus.State.CatchedMinionStates.Add(enemy.State.Clone());
            aiScript.PokeMinionPrefabs.Add(minionPrefab);
            aiScript.PokeMinionReviveTime.Add(0);
        }

        enemy.TakeDamage_Host(100000000, aiScript.characterStatus, DamageType.Capture);
        aiScript.isAttack = false;
        aiScript.ActiveSkillCoroutine = null;
    }

    private IEnumerator SummonPokes(List<(GameObject, int)> aliveNotSummonedPokePrefabs, Vector2 lookInput)
    {
        aiScript.isAttack = true;

        GameObject pokeball = null;
        if (aiScript.CharacterData.Is3DModel())
        {
            float holdBallTime = 0.87f;
            aiScript.PlayAnimationAllLayers("Throw Object");
            var rightHandTransform = aiScript.animator.GetBoneTransform(HumanBodyBones.RightHand);
            pokeball = Instantiate(pokeballPrefab, rightHandTransform);
            pokeball.transform.localScale = Vector3.one * 0.3f
                * aiScript.transform.lossyScale.x / pokeball.transform.lossyScale.x;
            aiScript.TobeDestroyed.Add(pokeball);
            yield return new WaitForSeconds(holdBallTime);
        }

        for (int idx = 0; idx < aliveNotSummonedPokePrefabs.Count; idx++)
        {
            // 召唤小弟
            int roomId = LevelManager.Instance.GetRoomNoByPosition(aiScript.transform.position);
            var room = LevelManager.Instance.Rooms[roomId];
            var pokePrefab = aliveNotSummonedPokePrefabs[idx];
            Vector2 summonPosition = aiScript.transform.position;
            summonPosition += lookInput * 5;
            if (summonPosition.x < room.xMin + 2) summonPosition.x = room.xMin + 2;
            else if (summonPosition.x > room.xMax - 1) summonPosition.x = room.xMax - 1;
            if (summonPosition.y < room.yMin + 2) summonPosition.y = room.yMin + 2;
            else if (summonPosition.y > room.yMax - 1) summonPosition.y = room.yMax - 1;

            float summonTime = 1.5f;
            if (idx == 0)
            {
                GameObject summonEffect = LevelManager.Instance.InstantiateTemporaryObject(summonPokeEffectPrefab, summonPosition);
                if (aiScript.CharacterData.Is3DModel())
                {
                    var startPos = pokeball.transform.position;
                    float elapsedTime = 0;
                    while (elapsedTime < summonTime)
                    {
                        elapsedTime += Time.deltaTime;
                        pokeball.transform.position = Vector3.Lerp(startPos, summonPosition, elapsedTime / summonTime);
                        yield return null;
                    }
                    pokeball.transform.position = summonPosition;

                    Destroy(pokeball);
                    aiScript.TobeDestroyed.Remove(pokeball);
                }
                else
                {
                    yield return new WaitForSeconds(summonTime);
                }
                Destroy(summonEffect);
            }

            GameObject pokeMinion = CharacterManager.Instance.InstantiateCompanionObject(pokePrefab.Item1, summonPosition);
            pokeMinion.name += pokePrefab.Item2;
            pokeMinion.tag = aiScript.gameObject.tag;
            if (pokeMinion.layer == LayerMask.NameToLayer("Default")) pokeMinion.layer = aiScript.gameObject.layer;
            Physics2D.SyncTransforms();
            var col2D = pokeMinion.GetComponentInChildren<Collider2D>();
            var tarPos = pokeMinion.transform.position;
            tarPos.y += col2D.bounds.extents.y + 0.5f;
            // 将血条显示到对象的头上
            var miniStatusCanvas = pokeMinion.GetComponentInChildren<Canvas>();
            if (miniStatusCanvas == null)
            {
                var obj1 = Instantiate(CharacterManager.Instance.miniStatusPrefab, tarPos, Quaternion.identity);
                obj1.transform.SetParent(pokeMinion.transform);
                miniStatusCanvas = obj1.GetComponent<Canvas>();
            }
            if (pokeMinion.CompareTag(Constants.TagPlayer))
            {
                var playerNameText = miniStatusCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
                if (playerNameText != null)
                {
                    playerNameText.gameObject.SetActive(true);
                    playerNameText.text = $"Companion #{pokePrefab.Item2 + 1}";
                }
                var obj2 = Instantiate(capturedMinionCanvas, tarPos, Quaternion.identity);
                obj2.transform.SetParent(pokeMinion.transform);
            }
            var pokeStatus = pokeMinion.GetComponent<CharacterStatus>();
            pokeStatus.Trainer = aiScript.characterStatus;
            if (!aiScript.isAi)
            {
                var pokeState = aiScript.characterStatus.State.CatchedMinionStates[pokePrefab.Item2];
                pokeState.Position = null;
                pokeState.CurrentHp = pokeState.MaxHp;
                pokeState.Damage = (int)(aiScript.characterStatus.State.Damage * pokeState.Scale / 2);
                if (pokeState.Damage < 1) pokeState.Damage = 1;
                pokeStatus.SetState(pokeState);
            }
            aiScript.ExistingPokes.Add((pokeMinion, pokePrefab.Item2));
        }

        aiScript.isAttack = false;

        if (aiScript.isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
        }

        aiScript.ActiveSkillCoroutine = null;
    }
}