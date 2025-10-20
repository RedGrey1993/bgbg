

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// Stomper不会对角线移动
public class Boss_5_0_TheRulerAI : CharacterBaseAI
{
    public List<GameObject> prevBossPrefabs;
    public Boss_5_0_TheRulerAI(GameObject character) : base(character)
    {
        prevBossPrefabs = new List<GameObject>();
        foreach (int stage in GameManager.Instance.PassedStages)
        {
            LevelData levelData = LevelDatabase.Instance.GetLevelData(stage);
            foreach (var bossPrefab in levelData.bossPrefabs)
            {
                prevBossPrefabs.Add(bossPrefab);
            }
        }
        // 随机排序prevBossPrefabs；Fisher-Yates 洗牌算法，时间复杂度为 O(n)，且能保证每个排列出现的概率相等
        System.Random rng = new ();
        for (int i = prevBossPrefabs.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (prevBossPrefabs[i], prevBossPrefabs[j]) = (prevBossPrefabs[j], prevBossPrefabs[i]); // 交换元素
        }
    }

    #region ICharacterAI implementation
    private float nextAggroChangeTime = 0;
    protected override void GenerateAILogic()
    {
        if (GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            if (isAttacking) return;
            UpdateAggroTarget();
            UpdateMoveInput();
            UpdateAttackInput();
        }
    }
    #endregion

    // 不造成碰撞伤害

    #region Aggro
    private GameObject AggroTarget { get; set; } = null; // 当前仇恨目标
    private void UpdateAggroTarget()
    {
        if (Time.time >= nextAggroChangeTime)
        {
            nextAggroChangeTime = Time.time + CharacterData.AggroChangeInterval;
            AggroTarget = CharacterManager.Instance.FindNearestPlayerInRange(character, CharacterData.AggroRange);
            Debug.Log($"fhhtest, {character.name} aggro target: {AggroTarget?.name}");
        }
    }
    #endregion

    #region Move
    // 统治者不能移动，会坐在原地，然后召唤或使用一些全场技能
    private void UpdateMoveInput()
    {
        characterInput.MoveInput = Vector2.zero;
    }
    #endregion

    #region Attack
    private void UpdateAttackInput()
    {
        characterInput.LookInput = Vector2.zero;
        if (AggroTarget != null && LevelManager.Instance.InSameRoom(character, AggroTarget))
        {
            isAttacking = true; // 在这里设置是为了避免在还未执行FixedUpdate执行动作的时候，在下一帧Update就把LookInput设置为0的问题
        }
    }

    private bool isAttacking = false; // 攻击时无法移动
    // private HashSet<int> existingBosses = new HashSet<int>();
    private List<GameObject> existingBosses = new ();
    private int bossIdx = 0;
    protected override void AttackAction()
    {
        if (isAttacking)
        {
            if (Time.time < nextAtkTime) return;
            nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

            existingBosses.RemoveAll(obj => obj == null);
            float hpRatio = (float)characterStatus.State.CurrentHp / characterStatus.State.MaxHp;
            if (hpRatio > 0.4f)
            {
                int rndSkillId = Random.Range(0, 2);
                if (rndSkillId == 0 && bossIdx < prevBossPrefabs.Count && existingBosses.Count < 2)
                {
                    if (!isSummoning && !isExplosion)
                    {
                        Debug.Log("fhhtest, Summon::::::");
                        GameManager.Instance.StartCoroutine(Summon());
                    }
                } else
                {
                    if (!isSummoning && !isExplosion)
                    {
                        Debug.Log("fhhtest, Explosion::::::");
                        GameManager.Instance.StartCoroutine(Explosion());
                    }
                }
                
            } else if (hpRatio > 0.1f)
            {
                
            } else
            {
                
            }
        }
        isAttacking = false;
    }
    #endregion

    #region 技能1，召唤
    private bool isSummoning = false;
    private IEnumerator Summon()
    {
        isSummoning = true;
        var virtualScreen = character.transform.GetChild(2).gameObject;
        virtualScreen.SetActive(true);
        var screenAnim = virtualScreen.GetComponentInChildren<Animator>();
        var animClips = screenAnim.runtimeAnimatorController.animationClips;
        float showTime = 0.5f, dismissTime = 0.5f;
        foreach (var clip in animClips)
        {
            if (clip.name == "VirtualScreenShowing")
            {
                showTime = clip.length;
            }
            else if (clip.name == "VirtualScreenDismiss")
            {
                dismissTime = clip.length;
            }
        }

        yield return new WaitForSeconds(showTime);

        var rulerClips = animator.runtimeAnimatorController.animationClips;
        float summonTime = 0.5f;
        foreach (var clip in rulerClips)
        {
            if (clip.name == "Pointing")
            {
                summonTime = clip.length;
            }
        }
        animator.SetTrigger("Pointing");
        yield return new WaitForSeconds(summonTime);
        screenAnim.Play("VirtualScreenDismiss");
        yield return new WaitForSeconds(dismissTime);
        virtualScreen.SetActive(false);

        while (existingBosses.Count < 2 && bossIdx < prevBossPrefabs.Count)
        {
            // 召唤之前的boss
            int roomId = LevelManager.Instance.GetRoomNoByPosition(character.transform.position);
            var room = LevelManager.Instance.Rooms[roomId];
            var bossPrefab = prevBossPrefabs[bossIdx++];
            var charData = bossPrefab.GetComponent<CharacterStatus>().characterData;
            int extentsX = (int)charData.bound.extents.x, extentsY = (int)charData.bound.extents.y;
            int theRulerHeight = (int)CharacterData.bound.extents.y;
            var rndX = Random.Range(room.xMin + 1 + extentsX + 0.1f, room.xMin + room.width - extentsX - 0.1f);
            var rndY = Random.Range(room.yMin + 1 + extentsY + 0.1f, room.yMin + room.height - theRulerHeight - extentsY - 0.1f);
            Vector2 position = new Vector2(rndX, rndY);
            GameObject summonEffect = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.summonEffectPrefab, position);
            yield return new WaitForSeconds(1.5f);
            Object.Destroy(summonEffect);
            GameObject boss = LevelManager.Instance.InstantiateTemporaryObject(bossPrefab, position);
            existingBosses.Add(boss);
        }

        isSummoning = false;
    }
    #endregion

    #region 技能2，爆炸
    private bool isExplosion = false;
    private IEnumerator Explosion()
    {
        isExplosion = true;
        var virtualScreen = character.transform.GetChild(2).gameObject;
        virtualScreen.SetActive(true);
        var screenAnim = virtualScreen.GetComponentInChildren<Animator>();
        var animClips = screenAnim.runtimeAnimatorController.animationClips;
        float showTime = 0.5f, dismissTime = 0.5f;
        foreach (var clip in animClips)
        {
            if (clip.name == "VirtualScreenShowing")
            {
                showTime = clip.length;
            }
            else if (clip.name == "VirtualScreenDismiss")
            {
                dismissTime = clip.length;
            }
        }

        yield return new WaitForSeconds(showTime);

        var rulerClips = animator.runtimeAnimatorController.animationClips;
        float summonTime = 0.5f;
        foreach (var clip in rulerClips)
        {
            if (clip.name == "Pointing")
            {
                summonTime = clip.length;
            }
        }
        animator.SetTrigger("Pointing");
        yield return new WaitForSeconds(summonTime);

        int roomId = LevelManager.Instance.GetRoomNoByPosition(character.transform.position);
        var room = LevelManager.Instance.Rooms[roomId];

        Bounds bossBound = CharacterData.bound;
        var bossPos = character.transform.position;
        float explosionRatio = 0.6f;
        int tileNumber = (int)((room.width - 1) * (room.height - 1) * explosionRatio);
        List<Vector2Int> tilePositions = new List<Vector2Int>();
        Vector2Int startPos = new Vector2Int((int)AggroTarget.transform.position.x, (int)AggroTarget.transform.position.y);
        tilePositions.Add(startPos);
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        visited.Add(startPos);

        for (int i = 0; i < tileNumber; i++)
        {
            int dir = Random.Range(0, 4);
            Vector2Int from = tilePositions[Random.Range(0, tilePositions.Count)];
            Vector2Int to = from;
            switch (dir)
            {
                case 0:
                    to = from + Vector2Int.up;
                    break;
                case 1:
                    to = from + Vector2Int.down;
                    break;
                case 2:
                    to = from + Vector2Int.left;
                    break;
                case 3:
                    to = from + Vector2Int.right;
                    break;
            }
            if (visited.Contains(to)
                || to.x > bossPos.x - bossBound.extents.x && to.x < bossPos.x + bossBound.extents.x && to.y > bossPos.y - bossBound.extents.y && to.y < bossPos.y + bossBound.extents.y)
            {
                continue;
            }
            if (to.x > (int)room.xMin && to.x < (int)room.xMax && to.y > (int)room.yMin && to.y < (int)room.yMax)
            {
                tilePositions.Add(to);
                visited.Add(to);
            }
        }

        foreach (var tilePos in tilePositions)
        {
            LevelManager.Instance.SetFloorTileExplosionWarning(new Vector3Int(tilePos.x, tilePos.y, 0));
        }
        yield return new WaitForSeconds(1f);

        screenAnim.Play("VirtualScreenDismiss");
        yield return new WaitForSeconds(dismissTime);
        virtualScreen.SetActive(false);

        foreach (var tilePos in tilePositions)
        {
            GameManager.Instance.StartCoroutine(PlayExplosionEffect(tilePos));
            yield return new WaitForSeconds(0.1f);
        }

        yield return new WaitForSeconds(1f);
        foreach (var tilePos in tilePositions)
        {
            LevelManager.Instance.ResetFloorTile(new Vector3Int(tilePos.x, tilePos.y, 0));
        }

        isExplosion = false;
    }

    private IEnumerator PlayExplosionEffect(Vector2Int position)
    {
        var explosionEffect = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.explosionEffectPrefab, new Vector2(position.x, position.y));
        var particleSystem = explosionEffect.GetComponentInChildren<ParticleSystem>();
        // LevelManager.Instance.SetFloorTileDestroyedAndCantPass(new Vector3Int(position.x, position.y, 0));
        yield return new WaitForSeconds(particleSystem.main.duration);
        Object.Destroy(explosionEffect);
    }
    #endregion
}