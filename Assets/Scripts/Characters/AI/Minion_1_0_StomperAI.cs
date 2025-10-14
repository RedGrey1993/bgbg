

using System.Collections;
using UnityEngine;

// Stomper不会对角线移动
public class Minion_1_0_StomperAI : CharacterBaseAI
{
    public Minion_1_0_StomperAI(GameObject character) : base(character)
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

    #region Collision
    private float nextDamageTime = 0;
    public override void OnCollisionEnter(Collision2D collision)
    {
        if (GameManager.Instance.IsLocalOrHost() && IsAlive())
        {
            if (!collision.gameObject.CompareTag(Constants.TagPlayer))
            {
                // Debug.Log($"fhhtest, {character.name} collided with {collision.gameObject.name}, bounce back");
                // characterInput.MoveInput.x = -characterInput.MoveInput.x;
                // characterInput.MoveInput.y = -characterInput.MoveInput.y;
            }
            else
            {
                if (Time.time > nextDamageTime)
                {
                    var status = collision.gameObject.GetComponent<CharacterStatus>();
                    if (isJumpingDown)
                    {
                        status.TakeDamage_Host(CharacterData.Damage * 2, null);
                    }
                    else
                    {
                        status.TakeDamage_Host(CharacterData.Damage, null);
                    }
                    nextDamageTime = Time.time + 1f / CharacterData.AttackFrequency;
                }
            }
        }
    }

    public override void OnCollisionStay(Collision2D collision)
    {
        OnCollisionEnter(collision);
    }
    #endregion

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
    private float nextMoveInputChangeTime = 0;
    private Vector3 targetPos = Vector3.zero;
    private void UpdateMoveInput()
    {
        if (Time.time > nextMoveInputChangeTime)
        {
            if (AggroTarget == null)
            {
                if (targetPos == Vector3.zero || Vector3.Distance(character.transform.position, targetPos) < 1)
                {
                    var roomId = LevelManager.Instance.GetRoomNoByPosition(character.transform.position);
                    var room = LevelManager.Instance.Rooms[roomId];
                    var rndX = Random.Range(room.xMin, room.xMin + room.width);
                    var rndY = Random.Range(room.yMin, room.yMin + room.height);
                    targetPos = new Vector2(rndX, rndY);
                }
                Move_RandomMoveToTarget(targetPos);
            }
            else
            {
                Move_ChaseInRoom();
            }
        }
    }

    private float chaseMoveInputInterval = 0;
    private void Move_ChaseInRoom()
    {
        float posXMod = character.transform.position.x.PositiveMod(Constants.RoomStep);
        float posYMod = character.transform.position.y.PositiveMod(Constants.RoomStep);
        const float nearWallLowPos = Constants.WallMaxThickness + Constants.CharacterMaxRadius;
        const float nearWallHighPos = Constants.RoomStep - Constants.CharacterMaxRadius;

        bool XNearWall(float d = 0) => posXMod < nearWallLowPos + d || posXMod > nearWallHighPos - d;
        bool YNearWall(float d = 0) => posYMod < nearWallLowPos + d || posYMod > nearWallHighPos - d;
        bool NearWall(float d = 0)
        {
            return XNearWall(d) || YNearWall(d);
        }

        // 在墙壁边缘时，需要尽快改变追击路线，避免来回横跳
        if (NearWall())
        {
            chaseMoveInputInterval = 0;
        }
        else
        {
            chaseMoveInputInterval = Random.Range(CharacterData.minChaseMoveInputInterval, CharacterData.maxChaseMoveInputInterval);
        }
        nextMoveInputChangeTime = Time.time + chaseMoveInputInterval;

        var diff = AggroTarget.transform.position - character.transform.position;
        var diffNormalized = diff.normalized;
        var sqrShootRange = characterStatus.State.ShootRange * characterStatus.State.ShootRange;
        // Debug.Log($"fhhtest, char {transform.name}, mod {posXMod},{posYMod}");
        Constants.PositionToIndex(character.transform.position, out int sx, out int sy);
        Constants.PositionToIndex(AggroTarget.transform.position, out int tx, out int ty);

        // 在同一间房间，直接追击
        if (LevelManager.Instance.RoomGrid[sx, sy] == LevelManager.Instance.RoomGrid[tx, ty])
        {
            // // 优先穿过门，不管是否在攻击范围内，即在墙边时先快速远离墙
            // if (XNearWall())
            // {
            //     characterInput.MoveInput = new Vector2(posXMod < nearWallLowPos ? 1 : -1, 0);
            // }
            // else if (YNearWall())
            // {
            //     characterInput.MoveInput = new Vector2(0, posYMod < nearWallLowPos ? 1 : -1);
            // }
            // 有仇恨目标时，朝仇恨目标移动，直到进入攻击范围
            if (diff.sqrMagnitude > sqrShootRange)
            {
                if (Mathf.Abs(diffNormalized.x) > 0.1f)
                {
                    if (!XNearWall())
                        diffNormalized.x *= 10; // 优先横着走，在直着走，避免横竖快速跳转
                }
                characterInput.MoveInput = diffNormalized.normalized;
            }
            else // 进入攻击范围
            {
                if (XNearWall(1))
                {
                    characterInput.MoveInput = new Vector2(0, diffNormalized.y);
                }
                else if (YNearWall(1))
                {
                    characterInput.MoveInput = new Vector2(diffNormalized.x, 0);
                }
                else
                {
                    characterInput.MoveInput = diffNormalized;
                }
                // 在攻击距离内左右横跳拉扯
                // characterInput.MoveInput = Mathf.Abs(diff.x) < Mathf.Abs(diff.y) ? new Vector2(diff.x > 0 ? 1 : -1, 0) : new Vector2(0, diff.y > 0 ? 1 : -1);
            }
        }
        else
        {
            // 在不同房间，随机移动
            if (targetPos == Vector3.zero || Vector3.Distance(character.transform.position, targetPos) < 1)
            {
                var roomId = LevelManager.Instance.GetRoomNoByPosition(character.transform.position);
                var room = LevelManager.Instance.Rooms[roomId];
                var rndX = Random.Range(room.xMin, room.xMin + room.width);
                var rndY = Random.Range(room.yMin, room.yMin + room.height);
                targetPos = new Vector2(rndX, rndY);
            }
            Move_RandomMoveToTarget(targetPos);
            AggroTarget = null; // 取消仇恨，等待下次重新搜索
        }
    }
    #endregion

    #region Attack
    private float nextJudgeAtkTime = 0;
    private void UpdateAttackInput()
    {
        if (AggroTarget != null)
        {
            var diff = AggroTarget.transform.position - character.transform.position;
            var atkRange = characterStatus.State.ShootRange;
            // 进入攻击距离，攻击，Stomper只会水平/垂直攻击
            if ((Mathf.Abs(diff.x) <= atkRange && Mathf.Abs(diff.y) < 0.5f) || (Mathf.Abs(diff.y) <= atkRange && Mathf.Abs(diff.x) < 0.5f))
            {
                if (Time.time >= nextJudgeAtkTime)
                {
                    nextJudgeAtkTime = Time.time + 1f;
                    // characterInput.LookInput = diff;
                    // 50% 概率朝目标移动（Stomper通过碰撞造成伤害）
                    int probability = Random.Range(0, 100);
                    // 处于水平一条线时，50% 概率跳跃踩踏攻击
                    if (probability < 100 && Mathf.Abs(diff.y) < 0.5f)
                    {
                        characterInput.MoveInput = Vector2.zero;
                        characterInput.LookInput = diff.normalized;
                        isAttacking = true; // 在这里设置是为了避免在还未执行FixedUpdate执行动作的时候，在下一帧Update就把LookInput设置为0的问题
                        return;
                    }
                }
            }
        }
        characterInput.LookInput = Vector2.zero;
    }

    private bool isAttacking = false;
    private bool isJumpingDown = false;
    private Coroutine jumpCoroutine = null;
    protected override void AttackAction()
    {
        if (jumpCoroutine != null) return;

        ref Vector2 lookInput = ref characterInput.LookInput;
        if (lookInput.sqrMagnitude < 0.1f) return;
        if (Time.time < nextAtkTime) return;
        nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

        NormalizeLookInput(ref lookInput);
        jumpCoroutine = GameManager.Instance.StartCoroutine(JumpToTarget(AggroTarget.transform.position, 5));
    }
    
    private IEnumerator JumpToTarget(Vector3 targetPos, float jumpHeight, float jumpDuration = 4.3f)
    {
        float elapsedTime = 0;
        var collider2D = character.GetComponent<Collider2D>();
        var characterBound = collider2D.bounds;
        var shadowPos = character.transform.position;
        shadowPos.y -= characterBound.extents.y;
        var shadowObj = Object.Instantiate(CharacterData.shadowPrefab, shadowPos, Quaternion.identity);
        LevelManager.Instance.ToRemoveBeforeNewStage.Add(shadowObj);

        animator.SetTrigger("Jump");
        var audioSrc = character.AddComponent<AudioSource>();
        audioSrc.PlayOneShot(CharacterData.jumpSound);
        Object.Destroy(audioSrc, CharacterData.jumpSound.length);

        float prepareJumpDuration = 2.3f;
        float afterJumpDuration = jumpDuration - 3.1f;
        yield return new WaitForSeconds(prepareJumpDuration);
        Vector3 startPos = character.transform.position;
        jumpDuration -= prepareJumpDuration;
        jumpDuration -= afterJumpDuration;
        while (elapsedTime < jumpDuration)
        {
            float x = Mathf.Lerp(startPos.x, targetPos.x, elapsedTime / jumpDuration);
            float y = Mathf.Lerp(startPos.y, targetPos.y, elapsedTime / jumpDuration);
            // 抛物线
            float z = -(jumpHeight - jumpHeight * 4 / (jumpDuration * jumpDuration) * (elapsedTime - jumpDuration / 2) * (elapsedTime - jumpDuration / 2));
            if (characterBound.size.y < Mathf.Abs(z))
            {
                collider2D.isTrigger = true;
            }
            else
            {
                collider2D.isTrigger = false;
            }
            character.transform.position = new Vector3(x, y, z);
            shadowObj.transform.position = new Vector3(x, y - characterBound.extents.y, 0);
            if (elapsedTime > jumpDuration / 2)
                isJumpingDown = true;

            elapsedTime += Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(afterJumpDuration);
        character.transform.position = targetPos;
        characterInput.LookInput = Vector2.zero;
        isAttacking = false;
        isJumpingDown = false;
        Object.Destroy(shadowObj);
        jumpCoroutine = null;
    }
    #endregion
}