using UnityEngine;

// Stomper不会对角线移动
public class Minion_4_1_KamikazeShipAI : CharacterBaseAI
{
    public GameObject explosionEffect;
    public GameObject spriteObject;
    public GameObject miniStatusObject;
    public AudioClip explosionSound;

    protected override void BounceBack(Collision2D collision)
    {
        if (Time.time > nextBounceTime && isAi && GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            nextBounceTime = Time.time + 1f;
            isBouncingBack = true;
            // 碰到墙还好，不反弹，碰到角色时很可能会互相卡住，所以需要反弹分开
            if (collision.gameObject.CompareTag(Constants.TagEnemy))
            {
                Debug.Log($"fhhtest, {name} collided with {collision.gameObject.name}, bounce back");
                if (Mathf.Abs(characterInput.MoveInput.x) > 0.1f && Mathf.Abs(characterInput.MoveInput.y) > 0.1f)
                {
                    // 对角线方向，随机翻转水平或垂直方向
                    if (Random.value < 0.5f)
                    {
                        characterInput.MoveInput.x = -characterInput.MoveInput.x;
                        characterInput.MoveInput.y = 0;
                    }
                    else
                    {
                        characterInput.MoveInput.x = 0;
                        characterInput.MoveInput.y = -characterInput.MoveInput.y;
                    }
                }
                else if (Mathf.Abs(characterInput.MoveInput.x) > 0.1f)
                {
                    characterInput.MoveInput.x = -characterInput.MoveInput.x;
                }
                else if (Mathf.Abs(characterInput.MoveInput.y) > 0.1f)
                {
                    characterInput.MoveInput.y = -characterInput.MoveInput.y;
                }
            }
        }
    }

    private void Explosion(Collision2D collision)
    {
        // 碰到墙还好，不反弹，碰到角色时很可能会互相卡住，所以需要反弹分开
        if (collision.gameObject.CompareTag(Constants.TagPlayer))
        {
            var status = collision.gameObject.GetComponent<CharacterStatus>();
            status.TakeDamage_Host(characterStatus.State.Damage, null);
            OnDeath();
        }
    }

    public override void OnDeath()
    {
        rb.linearVelocity = Vector2.zero;
        characterInput.MoveInput = Vector2.zero;
        var audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.PlayOneShot(explosionSound);
        Destroy(audioSrc, explosionSound.length);
        explosionEffect.SetActive(true);
        spriteObject.SetActive(false);
        miniStatusObject.SetActive(false);
        Destroy(gameObject, 2.5f);
    }

    protected override void SubclassCollisionEnter2D(Collision2D collision)
    {
        BounceBack(collision);
        Explosion(collision);
    }

    #region AI Logic / Update Input
    protected override void UpdateAggroTarget()
    {
        if (Time.time >= nextAggroChangeTime)
        {
            nextAggroChangeTime = Time.time + CharacterData.AggroChangeInterval;
            AggroTarget = CharacterManager.Instance.FindSamelinePlayerInRange(gameObject, CharacterData.AggroRange);
            Debug.Log($"fhhtest, {name} aggro target: {AggroTarget?.name}");
        }
    }

    protected override void UpdateMoveInput()
    {
        if (Time.time > nextMoveInputChangeTime)
        {
            if (AggroTarget == null || !LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
            {
                if (targetPos == Vector3.zero || Time.time > nextTargetPosChangeTime)
                {
                    var roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
                    targetPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, col2D.bounds);
                    nextTargetPosChangeTime = Time.time + Random.Range(CharacterData.randomMoveToTargetInterval.min, CharacterData.randomMoveToTargetInterval.max);
                }
                Move_RandomMoveToTarget(targetPos);
                curForce = 0;
            }
            else
            {
                if (isBouncingBack) isBouncingBack = false;
                else Move_ChaseInRoom(AggroTarget);
            }
            nextMoveInputChangeTime = Time.time + Random.Range(CharacterData.chaseMoveInputInterval.min, CharacterData.chaseMoveInputInterval.max);;
        }
    }

    private float curForce = 0;
    protected override void Move_ChaseInRoom(GameObject target, bool followTrainer = false)
    {
        Vector2 diff = (target.transform.position - transform.position).normalized;
        curForce = 1;

        characterInput.MoveInput.x = Mathf.Abs(diff.x) > Mathf.Abs(diff.y) ? diff.x : 0;
        characterInput.MoveInput.y = Mathf.Abs(diff.y) > Mathf.Abs(diff.x) ? diff.y : 0;
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
            if (curForce > 0)
            {
                rb.AddForce(moveInput * curForce, ForceMode2D.Impulse);
            }
            else
            {
                rb.linearVelocity = (moveInput + characterInput.MoveAdditionalInput) * characterStatus.State.MoveSpeed;
            }
            characterInput.MoveAdditionalInput = Vector2.zero;
        }
    }

    protected override void UpdateAttackInput()
    {
        characterInput.LookInput = Vector2.zero;
    }
    #endregion

}