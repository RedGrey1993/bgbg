using UnityEngine;

// Stomper不会对角线移动
public class Minion_4_1_KamikazeShipAI : CharacterBaseAI
{
    public GameObject explosionEffectPrefab;
    public AudioClip explosionSound;

    private void Explosion(Collision2D collision)
    {
        // 碰到墙还好，不反弹，碰到角色时很可能会互相卡住，所以需要反弹分开
        if (collision.gameObject.IsPlayerOrEnemy())
        {
            var tarStatus = collision.GetCharacterStatus();
            if (tarStatus != null)
            {
                if (characterStatus.IsFriendlyUnit(tarStatus))
                    return;

                tarStatus.TakeDamage_Host(characterStatus.State.Damage, null);
                characterStatus.TakeDamage_Host(100000000, tarStatus);
            }
        }
    }

    public override void OnDeath()
    {
        rb.linearVelocity = Vector2.zero;
        characterInput.MoveInput = Vector2.zero;
        GameManager.Instance.audioSource.PlayOneShot(explosionSound);
        var explosionEffect = LevelManager.Instance.InstantiateTemporaryObject(explosionEffectPrefab, transform.position);
        Destroy(gameObject);
        Destroy(explosionEffect, 2.5f);
    }

    protected override void SubclassCollisionEnter2D(Collision2D collision)
    {
        BounceBack(collision);
        Explosion(collision);
    }

    #region AI Logic / Update Input
    private float addMoveSpeed = 0;
    protected override void Move_ChaseInRoom(GameObject target, bool followTrainer = false)
    {
        if (characterStatus.Trainer != null && target == characterStatus.Trainer.gameObject)
        {
            base.Move_ChaseInRoom(target);
        }
        else
        { 
            Vector2 diff = target.transform.position - transform.position;
            if (Mathf.Abs(diff.y) < col2D.bounds.extents.y || Mathf.Abs(diff.x) < col2D.bounds.extents.x)
            {
                characterInput.MoveInput = diff.normalized;
                addMoveSpeed++;
                if (addMoveSpeed > characterStatus.State.MoveSpeed) addMoveSpeed = characterStatus.State.MoveSpeed;
            }
            else
            {
                if (targetPos == Vector3.zero || Time.time > nextTargetPosChangeTime)
                {
                    var roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
                    targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, col2D.bounds);
                    nextTargetPosChangeTime = Time.time + Random.Range(CharacterData.randomMoveToTargetInterval.min, CharacterData.randomMoveToTargetInterval.max);
                }
                Move_RandomMoveToTarget(targetPos);
                addMoveSpeed = 0;
            }
        }
    }
    
    protected override void MoveAction()
    {
        Vector2 moveInput = characterInput.MoveInput;
        if (moveInput.sqrMagnitude >= 0.1f)
        {
            if (audioSource && !audioSource.isPlaying) audioSource.Play();
        }
        else
        {
            if (audioSource && audioSource.isPlaying) audioSource.Stop();
        }

        // Apply movement directly
        // velocity is deprecated, use linearVelocity instead
        if (rb.bodyType != RigidbodyType2D.Static)
        {
            rb.linearVelocity = (moveInput + characterInput.MoveAdditionalInput) * (characterStatus.State.MoveSpeed + addMoveSpeed);
            characterInput.MoveAdditionalInput = Vector2.zero;
        }
    }

    protected override void UpdateAttackInput()
    {
        characterInput.LookInput = Vector2.zero;
    }
    #endregion

}