

using System.Collections;
using System.Collections.Generic;
using NetworkMessageProto;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

// Stomper不会对角线移动
public class Boss_3_0_PokeBoyAI : CharacterBaseAI
{
    public GameObject pokeball;
    public GameObject summonPokeEffectPrefab;
    public GameObject speedupEffectPrefab;
    public GameObject rageEffectPrefab;
    public List<CharacterSpawnConfigSO> pokeMinionSpawnConfigs;
    public GameObject capturedMinionCanvas;
    public int pokeMinionBuffTime = 5;

    protected override void SubclassStart()
    {
        if (isAi)
        {
            PokeMinionSpawnConfigIds.Clear();
            PokeMinionReviveTime.Clear();
            foreach (var cfg in pokeMinionSpawnConfigs)
            {
                PokeMinionSpawnConfigIds.Add(cfg.ID);
                PokeMinionReviveTime.Add(0);
            }
        }
        else if (PokeMinionSpawnConfigIds.Count == 0 && pokeMinionSpawnConfigs.Count > 0)
        {
            var cfg = pokeMinionSpawnConfigs[0];

            PokeMinionSpawnConfigIds.Add(cfg.ID);
            PokeMinionReviveTime.Add(0);

            characterStatus.State.CatchedMinionStates.Clear();
            
            var prefabStatus = cfg.prefab.GetComponent<CharacterStatus>();
            var state = prefabStatus.characterData.ToState();
            state.CharacterSpawnConfigId = cfg.ID;
            characterStatus.State.CatchedMinionStates.Add(state);
        }
    }

    protected override bool IsAtkCoroutineIdle()
    {
        return throwCoroutine == null 
            && ActiveSkillCoroutine == null
            && strengthenCoroutine == null;
    }

    #region Attack Action
    private Coroutine strengthenCoroutine;
    private float nextStrengthenTime = 0;
    protected override void AttackAction()
    {
        if (IsAtkCoroutineIdle())
        {
            if (characterInput.LookInput.sqrMagnitude < 0.1f) { return; }

            if (ExistingPokes.Count > 0 && strengthenCoroutine == null && Time.time > nextStrengthenTime)
            {
                nextStrengthenTime = Time.time + Random.Range(pokeMinionBuffTime * 3, pokeMinionBuffTime * 4);
                strengthenCoroutine = StartCoroutine(StrengthenPokes());
                return;
            }

            if (isAi && HasAliveNotSummonedPokePrefabs() && ActiveSkillCoroutine == null)
            {
                // 召唤pokes 15s后会复活
                var skillData = SkillDatabase.Instance.GetActiveSkill(Constants.CompanionMasterSkillId);
                skillData.executor.ExecuteSkill(gameObject, skillData);
                return;
            }

            if (throwCoroutine == null) // && (!isAi || AggroTarget != null))
            {
                // bool throwBall = false;
                // if (isAi && AggroTarget != null)
                // {
                //     var tarStatus = AggroTarget.GetComponent<CharacterStatus>();
                //     float tarHpRatio = (float)tarStatus.State.CurrentHp / tarStatus.State.MaxHp;
                //     throwBall = tarHpRatio < 0.25f;
                // }
                // if (!isAi || throwBall)
                {
                    throwCoroutine = StartCoroutine(ThrowPokeball(characterInput.LookInput));
                }
            }
        }
    }
    #endregion

    #region 技能1，召唤小怪
    // 实现在主动技能中
    #endregion

    #region 技能2，强化小弟
    private IEnumerator StrengthenPokes()
    {
        isAttack = true;
        if (ExistingPokes.Count > 0)
        {
            if (ExistingPokes[0].Item1 == null)
            {
                isAttack = false;
                strengthenCoroutine = null;
                yield break;
            }
            characterInput.LookInput = ExistingPokes[0].Item1.transform.position - transform.position;
            PlayAnimationAllLayers("挥手冲锋");
            yield return new WaitForSeconds(0.67f);

            foreach (var (poke, idx) in ExistingPokes)
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
        // animator.SetBool("Strengthen", false);
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
    private Coroutine throwCoroutine = null;
    private IEnumerator ThrowPokeball(Vector2 lookInput)
    {
        isAttack = true;
        float startTime = Time.time;
        float atkInterval = 1f / characterStatus.State.AttackFrequency;
        float throwTime = 0.87f;
        float speed = 1;
        if (atkInterval < throwTime)
        {
            speed = throwTime / atkInterval;
        }
        SetShootAnimation(speed);
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
        yield return StartCoroutine(AttackShoot(lookInput, atkInterval - elapsedTime, 1));

        isAttack = false;
        // animator.speed = 1;
        // animator.Play("Male Walking");
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
        }
        throwCoroutine = null;
    }
    #endregion

    protected override void SubclassFixedUpdate()
    {
        // 主要是针对玩家操作的情况，将玩家的输入置空
        // 攻击时不要改变朝向，只有不攻击时才改变（避免用户操作时持续读取Input导致朝向乱变）
        if (isAttack && !isAi)
        {
            // characterInput.MoveInput = Vector2.zero;
            characterInput.LookInput = Vector2.zero;
        }
    }
}