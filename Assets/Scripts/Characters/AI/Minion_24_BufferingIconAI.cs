using UnityEngine;

public class Minion_24_BufferingIconAI : CharacterBaseAI
{
    public float slowDownTime = 3f;

    protected override void UpdateAttackInput() {}

    protected override void AttackAction() {}

    protected override void SubclassTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == Constants.bulletsLayer)
        {
            var bs = other.gameObject.GetBullet();
            bs.rb.linearVelocity /= 2;
        }
        else if (other.IsPlayerOrEnemy())
        {
            CharacterStatus tarStatus = other.GetCharacterStatus();
            if (tarStatus == null || characterStatus.IsFriendlyUnit(tarStatus))
            {
                return;
            }

            tarStatus.SlowdownTime = Time.time + slowDownTime;
            tarStatus.slowdownCoroutine ??= tarStatus.StartCoroutine(tarStatus.SlowdownCoroutine());
        }
    }

    protected override void SubclassTriggerStay2D(Collider2D other)
    {
        if (other.IsPlayerOrEnemy())
        {
            CharacterStatus tarStatus = other.GetCharacterStatus();
            if (tarStatus == null || characterStatus.IsFriendlyUnit(tarStatus))
            {
                return;
            }

            tarStatus.SlowdownTime = Time.time + slowDownTime;
            tarStatus.slowdownCoroutine ??= tarStatus.StartCoroutine(tarStatus.SlowdownCoroutine());
        }
    }
}