

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

// Stomper不会对角线移动
public class Boss_3_0_PokeBoyAI : CharacterBaseAI
{
    public GameObject pokeball;
    public GameObject summonPokeEffectPrefab;
    public GameObject speedupEffectPrefab;
    public GameObject rageEffectPrefab;
    public List<GameObject> pokeMinionPrefabs;
    public GameObject capturedMinionCanvas;
    public int pokeMinionRebornTime = 15;
    public int pokeMinionBuffTime = 5;
    private int maxPokeMinionCount = 6;

    private List<float> pokeMinionDeadTime;

    protected override void SubclassStart()
    {
        if (!isAi)
        {
            pokeMinionPrefabs.Clear();
            foreach (var prefabInfo in characterStatus.State.CatchedMinions)
            {
                var levelData = LevelDatabase.Instance.GetLevelData(prefabInfo.StageId);
                var minionPrefab = levelData.normalMinionPrefabs[prefabInfo.PrefabId];
                pokeMinionPrefabs.Add(minionPrefab);
            }
        }

        pokeMinionDeadTime = new List<float>();
        foreach (var prefab in pokeMinionPrefabs)
        {
            pokeMinionDeadTime.Add(-pokeMinionRebornTime);
        }
    }

    #region Animation
    protected override void SetIdleAnimation(Direction dir)
    {
        if (animator)
        {
            animator.speed = 1;
            animator.SetFloat("Speed", 0);
        }
    }

    protected override void SetRunAnimation(Direction dir)
    {
        if (animator)
        {
            animator.speed = 1;
            animator.SetFloat("Speed", 1);
        }
    }
    #endregion

    #region Attack Action
    private List<(GameObject, int)> existingPokes = new();
    private Coroutine summonCoroutine;
    private Coroutine strengthenCoroutine;
    private float nextStrengthenTime = 0;
    protected override void AttackAction()
    {
        if (!isAttack)
        {
            // 所有技能都在释放中，则不能再释放技能
            if (summonCoroutine != null && strengthenCoroutine != null && shootCoroutine != null) { return; }
            if (characterInput.LookInput.sqrMagnitude < 0.1f) { return; }
            // if (Time.time < nextAtkTime) { return; }
            // nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

            foreach ((GameObject, int) poke in existingPokes)
            {
                if (poke.Item1 == null)
                {
                    pokeMinionDeadTime[poke.Item2] = Time.time;
                }
            }
            existingPokes.RemoveAll(obj => obj.Item1 == null);

            if (existingPokes.Count > 0 && strengthenCoroutine == null && Time.time > nextStrengthenTime)
            {
                nextStrengthenTime = Time.time + Random.Range(pokeMinionBuffTime * 3, pokeMinionBuffTime * 4);
                strengthenCoroutine = StartCoroutine(StrengthenPokes());
            }

            float hpRatio = (float)characterStatus.State.CurrentHp / characterStatus.State.MaxHp;
            if (pokeMinionPrefabs.Count > 0 && summonCoroutine == null)
            {
                // 召唤pokes 15s后会复活
                if (isAi)
                {
                    if (hpRatio > 0.6f)
                    {
                        if (existingPokes.Count < 1)
                        {
                            summonCoroutine = StartCoroutine(SummonPokes(1, characterInput.LookInput));
                        }
                    }
                    else if (hpRatio > 0.3f)
                    {
                        if (existingPokes.Count < 2)
                        {
                            summonCoroutine = StartCoroutine(SummonPokes(2, characterInput.LookInput));
                        }
                    }
                    else
                    {
                        if (existingPokes.Count < 5)
                        {
                            summonCoroutine = StartCoroutine(SummonPokes(5, characterInput.LookInput));
                        }
                    }
                }
                else
                {
                    if (existingPokes.Count < 6)
                    {
                        summonCoroutine = StartCoroutine(SummonPokes(6, characterInput.LookInput));
                    }
                }
            }

            if (strengthenCoroutine == null && shootCoroutine == null && (!isAi || AggroTarget != null))
            {
                bool throwBall = false;
                if (isAi && AggroTarget != null)
                {
                    var tarStatus = AggroTarget.GetComponent<CharacterStatus>();
                    float tarHpRatio = (float)tarStatus.State.CurrentHp / tarStatus.State.MaxHp;
                    throwBall = tarHpRatio < 0.25f;
                }
                if (!isAi || throwBall)
                {
                    shootCoroutine = StartCoroutine(ThrowPokeball(characterInput.LookInput));
                }
            }
        }
    }
    #endregion

    #region 技能1，召唤小怪
    private int pokeIdx = 0;
    private IEnumerator SummonPokes(int count, Vector2 lookInput)
    {
        isAttack = true;

        int needCount = count - existingPokes.Count;
        // 存活，且没有被召唤到场上的小怪
        List<(GameObject, int)> aliveNotSummonedPokePrefabs = new();
        int initPokeIdx = pokeIdx;
        while (true)
        {
            if (Time.time > pokeMinionDeadTime[pokeIdx] + pokeMinionRebornTime)
            {
                aliveNotSummonedPokePrefabs.Add((pokeMinionPrefabs[pokeIdx], pokeIdx));
                pokeMinionDeadTime[pokeIdx] = Time.time + 10000f;
            }
            pokeIdx = (pokeIdx + 1) % pokeMinionPrefabs.Count;
            if (aliveNotSummonedPokePrefabs.Count >= needCount) break;
            if (pokeIdx == initPokeIdx) break;
        }
        if (aliveNotSummonedPokePrefabs.Count <= 0)
        {
            isAttack = false;

            animator.Play("Male Walking");
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
            summonCoroutine = null;
            yield break;
        }

        animator.Play("Throw Object");
        yield return new WaitForSeconds(0.87f);

        int idx = 0;
        while (existingPokes.Count < count && idx < aliveNotSummonedPokePrefabs.Count)
        {
            // 召唤小弟
            int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
            var room = LevelManager.Instance.Rooms[roomId];
            var pokePrefab = aliveNotSummonedPokePrefabs[idx++];
            Vector2 summonPosition = transform.position;
            summonPosition += lookInput * CharacterData.ShootRange;
            if (summonPosition.x < room.xMin + 2) summonPosition.x = room.xMin + 2;
            else if (summonPosition.x > room.xMax - 1) summonPosition.x = room.xMax - 1;
            if (summonPosition.y < room.yMin + 2) summonPosition.y = room.yMin + 2;
            else if (summonPosition.y > room.yMax - 1) summonPosition.y = room.yMax - 1;

            GameObject summonEffect = LevelManager.Instance.InstantiateTemporaryObject(summonPokeEffectPrefab, summonPosition);
            yield return new WaitForSeconds(1.5f);
            Destroy(summonEffect);
            GameObject pokeMinion = CharacterManager.Instance.InstantiateCompanionObject(pokePrefab.Item1, summonPosition);
            pokeMinion.name += pokePrefab.Item2;
            pokeMinion.tag = gameObject.tag;
            if (pokeMinion.layer == LayerMask.NameToLayer("Default")) pokeMinion.layer = gameObject.layer;
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
            pokeStatus.Trainer = characterStatus;
            if (!isAi)
            {
                if (pokePrefab.Item2 >= characterStatus.State.CatchedMinionStates.Count)
                {
                    Debug.LogError($"fhhtest, pokePrefab.Item2: {pokePrefab.Item2}, "
                        + $"{characterStatus.State.CatchedMinionStates.Count}, "
                        + $"{pokeMinionPrefabs.Count}, {pokeMinionDeadTime.Count}");
                }
                var pokeState = characterStatus.State.CatchedMinionStates[pokePrefab.Item2];
                pokeState.Position = null;
                pokeState.CurrentHp = pokeState.MaxHp;
                pokeState.Damage = characterStatus.State.Damage;
                pokeStatus.SetState(pokeState);
            }
            existingPokes.Add((pokeMinion, pokePrefab.Item2));
        }

        isAttack = false;
        animator.Play("Male Walking");
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
        }
        summonCoroutine = null;
    }
    #endregion

    #region 技能2，强化小弟
    private IEnumerator StrengthenPokes()
    {
        isAttack = true;
        if (existingPokes.Count > 0)
        {
            characterInput.LookInput = existingPokes[0].Item1.transform.position - transform.position;
            animator.Play("挥手冲锋");
            yield return new WaitForSeconds(0.67f);

            foreach (var (poke, idx) in existingPokes)
            {
                if (poke == null) continue;
                var rnd = Random.Range(0, 2);
                if (rnd == 0) // speedup
                {
                    var speedupEffect = LevelManager.Instance.InstantiateTemporaryObject(speedupEffectPrefab, poke.transform.position);
                    Destroy(speedupEffect, 3);
                    StartCoroutine(SpeedUp(poke));
                }
                else // rage
                {
                    var rageEffect = LevelManager.Instance.InstantiateTemporaryObject(rageEffectPrefab, poke.transform.position);
                    Destroy(rageEffect, 3);
                    StartCoroutine(Rage(poke));
                }
            }
        }

        isAttack = false;
        animator.Play("Male Walking");
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
        }
        strengthenCoroutine = null;
    }

    private IEnumerator SpeedUp(GameObject poke)
    {
        if (poke.TryGetComponent<CharacterStatus>(out var status))
        {
            status.State.MoveSpeed *= 2;

            yield return new WaitForSeconds(pokeMinionBuffTime);
            if (status == null) yield break;
            status.State.MoveSpeed /= 2;
        }
    }

    private IEnumerator Rage(GameObject poke)
    {
        if (poke.TryGetComponent<CharacterStatus>(out var status))
        {
            status.State.Damage *= 2;
            var initialColor = status.State.Color.ToColor();
            status.SetColor(Color.red, false);

            yield return new WaitForSeconds(pokeMinionBuffTime);
            if (status == null) yield break;
            status.State.Damage /= 2;
            status.SetColor(initialColor, false);
        }
    }
    #endregion

    #region 技能3，扔红白球
    private IEnumerator ThrowPokeball(Vector2 lookInput)
    {
        isAttack = true;
        float startTime = Time.time;
        float atkInterval = 1f / characterStatus.State.AttackFrequency;
        float throwTime = 0.87f;
        if (atkInterval < throwTime)
        {
            animator.speed = throwTime / atkInterval;
        }
        animator.Play("Throw Object");
        pokeball.SetActive(true);
        if (atkInterval >= throwTime)
        {
            yield return new WaitForSeconds(throwTime);
        }
        else
        {
            yield return new WaitForSeconds(atkInterval);
        }
        pokeball.SetActive(false);
        float elapsedTime = Time.time - startTime;
        // TODO: fixedDamage修改回2
        yield return StartCoroutine(AttackShoot(lookInput, atkInterval - elapsedTime, 20));

        isAttack = false;
        animator.speed = 1;
        animator.Play("Male Walking");
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
        }
        shootCoroutine = null;
    }
    #endregion

    private int circularIdx = 0;
    public override void Killed(CharacterStatus enemy)
    {
        if (CharacterManager.Instance.minionObjects.ContainsKey(enemy.State.PlayerId))
        {
            var prefabInfo = CharacterManager.Instance.minionPrefabInfos[enemy.State.PlayerId];
            var levelData = LevelDatabase.Instance.GetLevelData(prefabInfo.StageId);
            var minionPrefab = levelData.normalMinionPrefabs[prefabInfo.PrefabId];

            if (characterStatus.State.CatchedMinions.Count == maxPokeMinionCount)
            {
                characterStatus.State.CatchedMinions[circularIdx] = prefabInfo;
                characterStatus.State.CatchedMinionStates[circularIdx] = enemy.State.Clone();
                pokeMinionPrefabs[circularIdx] = minionPrefab;
                pokeMinionDeadTime[circularIdx] = -pokeMinionRebornTime;
                circularIdx = (circularIdx + 1) % maxPokeMinionCount;
            }
            else
            {
                characterStatus.State.CatchedMinions.Add(prefabInfo);
                characterStatus.State.CatchedMinionStates.Add(enemy.State.Clone());
                pokeMinionPrefabs.Add(minionPrefab);
                pokeMinionDeadTime.Add(-pokeMinionRebornTime);
            }
        }
    }
}