

using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public int pokeMinionBuffTime = 5;

    protected override void SubclassStart()
    {
        if (characterStatus.State.ActiveSkillId == 0)
        {
            characterStatus.State.ActiveSkillId = Constants.CompanionMasterSkillId;
            characterStatus.State.ActiveSkillCurCd = -1;
            if (characterStatus.State.PlayerId == CharacterManager.Instance.MyInfo.Id)
            {
                var spc = UIManager.Instance.GetComponent<StatusPanelController>();
                spc.UpdateMyStatusUI(characterStatus.State);
            }
        }

        if (isAi)
        {
            PokeMinionPrefabs.Clear();
            PokeMinionReviveTime.Clear();
            foreach (var prefab in pokeMinionPrefabs)
            {
                PokeMinionPrefabs.Add(prefab);
                PokeMinionReviveTime.Add(0);
            }
        }
    }

    protected override bool IsAtkCoroutineIdle()
    {
        return throwCoroutine == null 
            || (ActiveSkillCoroutine == null && PokeMinionPrefabs.Count > 0 && HasAliveNotSummonedPokePrefabs())
            || (strengthenCoroutine == null && ExistingPokes.Count > 0 && Time.time > nextStrengthenTime);
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

    private float baseMoveSpeed = 5;
    protected override void SetRunAnimation(Direction dir)
    {
        if (animator)
        {
            animator.speed = characterStatus.State.MoveSpeed / baseMoveSpeed;
            animator.SetFloat("Speed", 1);
        }
    }
    #endregion

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
            animator.Play("挥手冲锋");
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
    private Coroutine throwCoroutine = null;
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
        else
        {
            animator.speed = 1;
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
        yield return StartCoroutine(AttackShoot(lookInput, atkInterval - elapsedTime, 2));

        isAttack = false;
        animator.speed = 1;
        animator.Play("Male Walking");
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
        }
        throwCoroutine = null;
    }
    #endregion
}