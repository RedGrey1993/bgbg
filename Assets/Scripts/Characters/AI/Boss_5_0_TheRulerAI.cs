

using System.Collections;
using System.Collections.Generic;
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
    // private HashSet<int> existingBosses = new HashSet<int>();
    private List<GameObject> existingBosses = new ();
    private int bossIdx = 0;
    private void UpdateAttackInput()
    {
        if (Time.time < nextAtkTime) return;
        nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

        existingBosses.RemoveAll(obj => obj == null);

        characterInput.LookInput = Vector2.zero;
        if (AggroTarget != null && LevelManager.Instance.InSameRoom(character, AggroTarget))
        {
            isAttacking = true; // 在这里设置是为了避免在还未执行FixedUpdate执行动作的时候，在下一帧Update就把LookInput设置为0的问题

            // float hpRatio = (float)characterStatus.State.CurrentHp / characterStatus.State.MaxHp;
            // if (hpRatio > 0.7f)
            // {
            //     int rndSkillId = Random.Range(0, 2);
            //     if (rndSkillId == 0)
            //     {
            //         while (existingBosses.Count < 2 && bossIdx < prevBossPrefabs.Count)
            //         {
            //             // 召唤之前的boss
            //             int roomId = LevelManager.Instance.GetRoomNoByPosition(character.transform.position);
            //             var room = LevelManager.Instance.Rooms[roomId];
            //             int extentsX = 3, extentsY = 3;
            //             int theRulerHeight = 3;
            //             var rndX = Random.Range(room.xMin + 1 + extentsX + 0.1f, room.xMin + room.width - extentsX - 0.1f);
            //             var rndY = Random.Range(room.yMin + 1 + extentsY + 0.1f, room.yMin + room.height - theRulerHeight - extentsY - 0.1f);
            //             Vector2 position = new Vector2(rndX, rndY);
            //             GameObject boss = Object.Instantiate(prevBossPrefabs[bossIdx++], position, Quaternion.identity);
            //             existingBosses.Add(boss);
            //         }
            //     } else
            //     {
                    
            //     }
                
            // } else if (hpRatio > 0.3f)
            // {
                
            // } else
            // {
                
            // }
        }
    }

    private bool isAttacking = false; // 攻击时无法移动
    protected override void AttackAction()
    {
        // base.AttackAction();
        isAttacking = false;
    }
    #endregion
}