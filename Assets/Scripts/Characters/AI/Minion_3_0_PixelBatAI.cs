
using UnityEngine;

// 只会在房间中随机飞行
public class Minion_3_0_PixelBatAI : CharacterBaseAI
{
    #region AI Logic / Update Input
    // 永远不会发动主动攻击（意义上相当于一直占用着一个空的atkCoroutine，实际上什么也不做）
    // CanAttack永远==false，就会一直随机移动，而不会追踪目标
    protected override bool IsAtkCoroutineIdle()
    {
        return false;
    }

    protected override void UpdateAttackInput()
    {
        characterInput.LookInput = Vector2.zero;
    }
    #endregion
}