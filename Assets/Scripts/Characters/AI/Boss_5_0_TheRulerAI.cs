

using System.Collections;
using UnityEngine;

// Stomper不会对角线移动
public class Boss_5_0_TheRulerAI : CharacterBaseAI
{
    public Boss_5_0_TheRulerAI(GameObject character) : base(character)
    {
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
        
    }
    #endregion

    #region Attack
    private void UpdateAttackInput()
    {
        if (AggroTarget != null)
        {
            var diff = AggroTarget.transform.position - character.transform.position;
            var atkRange = characterStatus.State.ShootRange;
            // 进入攻击距离，攻击，会斜向攻击
            if (diff.sqrMagnitude <= atkRange * atkRange)
            {
                characterInput.MoveInput = Vector2.zero;
                characterInput.LookInput = diff.normalized;
                isAttacking = true; // 在这里设置是为了避免在还未执行FixedUpdate执行动作的时候，在下一帧Update就把LookInput设置为0的问题
                return;
            }
        }
        characterInput.LookInput = Vector2.zero;
    }

    private bool isAttacking = false; // 攻击时无法移动
    protected override void AttackAction()
    {
        // base.AttackAction();
        isAttacking = false;
    }
    #endregion
}