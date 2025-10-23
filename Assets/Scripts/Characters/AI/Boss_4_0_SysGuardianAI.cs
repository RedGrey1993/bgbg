using UnityEngine;

[RequireComponent(typeof(Collider2D))]

// Stomper不会对角线移动
public class Boss_4_0_SysGuardianAI : CharacterBaseAI
{
    #region AI Logic / Update Input
    // 系统守护者不能移动，会一直坐落在房间最上方
    protected override void UpdateMoveInput()
    {
        characterInput.MoveInput = Vector2.zero;
    }

    // 系统守护者不需要设置LookInput，他在协程中直接攻击最新的目标位置
    protected override void UpdateAttackInput()
    {
        characterInput.LookInput = Vector2.zero;
        if (AggroTarget != null && LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
        {
            isAiming = true; // 在这里设置是为了避免在还未执行FixedUpdate执行动作的时候，在下一帧Update就把LookInput设置为0的问题
        }
    }
    #endregion
}