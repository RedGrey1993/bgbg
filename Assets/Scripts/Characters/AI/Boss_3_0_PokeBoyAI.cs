

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

// Stomper不会对角线移动
public class Boss_3_0_PokeBoyAI : CharacterBaseAI
{
    public GameObject pokeball;
    public GameObject summonPokeEffectPrefab;
    public List<GameObject> pokeMinionPrefabs;
    public int pokeMinionRebornTime = 15;

    private List<float> pokeMinionDeadTime;

    protected override void SubclassStart()
    {
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
            animator.SetFloat("Speed", 0);
        }
    }

    protected override void SetRunAnimation(Direction dir)
    {
        if (animator)
        {
            animator.SetFloat("Speed", 1);
        }
    }
    #endregion

    #region Attack Action
    private List<(GameObject, int)> existingPokes = new();
    private Coroutine summonCoroutine;
    protected override void AttackAction()
    {
        if (isAiming && !isAttack)
        {
            isAiming = false;
            if (summonCoroutine != null) { return; }
            ref Vector2 lookInput = ref characterInput.LookInput;
            if (lookInput.sqrMagnitude < 0.1f) { return; }
            if (Time.time < nextAtkTime) { return; }

            nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;
            characterInput.NormalizeLookInput();

            foreach ((GameObject, int) poke in existingPokes)
            {
                if (poke.Item1 == null)
                {
                    pokeMinionDeadTime[poke.Item2] = Time.time;
                }
            }
            existingPokes.RemoveAll(obj => obj.Item1 == null);

            float hpRatio = (float)characterStatus.State.CurrentHp / characterStatus.State.MaxHp;
            // 召唤pokes 15s后会复活
            if (hpRatio > 0.6f)
            {
                if (existingPokes.Count < 1)
                {
                    StartCoroutine(SummonPokes(1));
                }
            }
            else if (hpRatio > 0.3f)
            {
                if (existingPokes.Count < 2)
                {
                    StartCoroutine(SummonPokes(2));
                }
            }
            else
            {
                if (existingPokes.Count < 3)
                {
                    StartCoroutine(SummonPokes(3));
                }
            }

            if (existingPokes.Count > 0)
            {
                // TODO: 随机强化存在的Pokes：加速(SpeedUp)/狂暴(Rage)
            }

            if (AggroTarget != null)
            {
                var tarStatus = AggroTarget.GetComponent<CharacterStatus>();
                float tarHpRatio = (float)tarStatus.State.CurrentHp / tarStatus.State.MaxHp;
                if (tarHpRatio < 0.25f)
                {
                    // TODO: 扔红白球攻击
                }
            }
        }
    }
    #endregion

    #region 技能1，召唤小怪
    private int pokeIdx = 0;
    private IEnumerator SummonPokes(int count)
    {
        isAttack = true;

        int needCount = count - existingPokes.Count;
        List<(GameObject, int)> alivePokePrefabs = new();
        int initPokeIdx = pokeIdx;
        while (true)
        {
            if (Time.time > pokeMinionDeadTime[pokeIdx] + pokeMinionRebornTime)
            {
                alivePokePrefabs.Add((pokeMinionPrefabs[pokeIdx], pokeIdx));
                pokeMinionDeadTime[pokeIdx] = Time.time + 10000f;
            }
            pokeIdx = (pokeIdx + 1) % pokeMinionPrefabs.Count;
            if (alivePokePrefabs.Count >= needCount) break;
            if (pokeIdx == initPokeIdx) break;
        }
        if (alivePokePrefabs.Count <= 0)
        {
            summonCoroutine = null;
            isAttack = false;

            characterInput.LookInput = Vector2.zero; // 避免移动时不改变朝向
            animator.Play("Male Walking");
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
            yield break;
        }

        animator.Play("Throw Object");
        yield return new WaitForSeconds(0.87f);

        int idx = 0;
        while (existingPokes.Count < count && idx < alivePokePrefabs.Count)
        {
            // 召唤小弟
            int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
            var room = LevelManager.Instance.Rooms[roomId];
            var pokePrefab = alivePokePrefabs[idx++];
            Vector2 summonPosition = transform.position;
            summonPosition += characterInput.LookInput * CharacterData.ShootRange;
            if (summonPosition.x < room.xMin + 2) summonPosition.x = room.xMin + 2;
            else if (summonPosition.x > room.xMax - 1) summonPosition.x = room.xMax - 1;
            if (summonPosition.y < room.yMin + 2) summonPosition.y = room.yMin + 2;
            else if (summonPosition.y > room.yMax - 1) summonPosition.y = room.yMax - 1;

            GameObject summonEffect = LevelManager.Instance.InstantiateTemporaryObject(summonPokeEffectPrefab, summonPosition);
            yield return new WaitForSeconds(1.5f);
            Destroy(summonEffect);
            GameObject pokeMinion = LevelManager.Instance.InstantiateTemporaryObject(pokePrefab.Item1, summonPosition);
            existingPokes.Add((pokeMinion, pokePrefab.Item2));
        }

        summonCoroutine = null;
        isAttack = false;

        characterInput.LookInput = Vector2.zero; // 避免移动时不改变朝向
        animator.Play("Male Walking");
        // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
        yield return new WaitForSeconds(Random.Range(1, 3f));
    }
    #endregion
}