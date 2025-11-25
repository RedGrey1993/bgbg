

using System.Collections;
using UnityEngine;

public class Minion_17_WarpMageAI : CharacterBaseAI
{
    public GameObject teleportPrefab;
    public float teleportInterval = 3f;
    private SpriteRenderer sr = null;
    private Coroutine teleportCoroutine = null;
    private Coroutine atkCoroutine = null;

    protected override void SubclassStart()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    private float nxtTeleportTime = 0;
    protected override void SubclassCollisionEnter2D(Collision2D collision)
    {
        if (Time.time > nxtTeleportTime) {
            teleportCoroutine ??= StartCoroutine(Teleport(0.5f, AggroTarget));
            nxtTeleportTime = Time.time + teleportInterval;
        }
        if (CharacterData.causeCollisionDamage)
            ProcessCollisionDamage(collision);
    }

    protected override void SubclassTriggerEnter2D(Collider2D other)
    {
        if (Time.time > nxtTeleportTime) {
            teleportCoroutine ??= StartCoroutine(Teleport(0.5f, AggroTarget));
            nxtTeleportTime = Time.time + teleportInterval;
        }
    }

    protected override void AttackAction()
    {
        if (!isAttack)
        {
            if (characterInput.LookInput.sqrMagnitude < 0.1f) { return; }

            atkCoroutine ??= StartCoroutine(Attack_Shoot(characterInput.LookInput));
        }
    }

    protected override void LookToAction()
    {
        ref Vector2 moveInput = ref characterInput.MoveInput;
        ref Vector2 lookInput = ref characterInput.LookInput;
        if (isAttack || lookInput.sqrMagnitude >= 0.1f)
        {
            if (lookInput.sqrMagnitude < 0.1f) // 不修改之前的方向
                return;
            LookDir = lookInput;
        }
        else if (moveInput.sqrMagnitude >= 0.1f)
        {
            LookDir = moveInput;
        }

        Transform trans = transform.GetChild(0);
        trans.localRotation = Quaternion.identity;
        if (LookDir.x > 0)
        {
            var scale = trans.localScale;
            scale.x = -Mathf.Abs(scale.x);
            trans.localScale = scale;
        }
        else
        {
            var scale = trans.localScale;
            scale.x = Mathf.Abs(scale.x);
            trans.localScale = scale;
        }
    }

    private IEnumerator Teleport(float dismissDuration, GameObject aggroTarget)
    {
        var teleportEffect = GameManager.Instance.GetObject(teleportPrefab, transform.position);
        GameManager.Instance.RecycleObject(teleportEffect, dismissDuration);

        float elapsedTime = 0f;
        Color color = sr.color;
        while(elapsedTime < dismissDuration)
        {
            yield return null;
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / dismissDuration);
            color.a = alpha;
            sr.color = color;
        }
        Vector2[] dirs = new Vector2[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        Vector2 tarPos;
        if (aggroTarget != null)
        {
            int roomId = LevelManager.Instance.GetRoomNoByPosition(aggroTarget.transform.position);
            Rect room = LevelManager.Instance.Rooms[roomId];

            var dir = dirs[Random.Range(0, dirs.Length)];
            tarPos = (Vector2)aggroTarget.transform.position + dir * characterStatus.State.ShootRange;
            if (tarPos.x < room.xMin + 1 + col2D.bounds.extents.x) 
                tarPos.x = room.xMin + 1 + col2D.bounds.extents.x;
            if (tarPos.x > room.xMax - col2D.bounds.extents.x) 
                tarPos.x = room.xMax - col2D.bounds.extents.x;
            if (tarPos.y < room.yMin + 1 + col2D.bounds.extents.y) 
                tarPos.y = room.yMin + 1 + col2D.bounds.extents.y;
            if (tarPos.y > room.yMax - col2D.bounds.extents.y) 
                tarPos.y = room.yMax - col2D.bounds.extents.y;
        }
        else
        {
            int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
            tarPos = LevelManager.Instance.GetRandomPositionInRoom(roomId, col2D.bounds);
        }
        col2D.enabled = false;

        transform.position = tarPos;
        elapsedTime = 0f;
        while(elapsedTime < dismissDuration)
        {
            yield return null;
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsedTime / dismissDuration);
            color.a = alpha;
            sr.color = color;
        }
        col2D.enabled = true;

        if (AggroTarget != null)
        {
            var lookInput = AggroTarget.transform.position - transform.position;
            atkCoroutine ??= StartCoroutine(Attack_Shoot(lookInput));
        }

        teleportCoroutine = null;
    }

    private IEnumerator Attack_Shoot(Vector2 lookInput)
    {
        isAttack = true;

        animator.SetTrigger("Attack");
        yield return new WaitForSeconds(0.6f);
        // 调用父类方法
        yield return StartCoroutine(AttackShoot(lookInput, 1f / characterStatus.State.AttackFrequency));

        // isAttack = false后才能移动
        isAttack = false; // isAttack=false后就不再设置朝向为LookInput，而是朝向MoveInput
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            // 这时候 shootCoroutine 还不是null，所以不会再次进入攻击
            yield return new WaitForSeconds(Random.Range(1, 3f));
        }
        // shootCoroutine = null后才能再次使用该技能
        atkCoroutine = null;
    }

    protected override bool IsAtkCoroutineIdle()
    {
        return atkCoroutine == null;
    }

    protected override void Move_FollowAcrossRooms(GameObject target, bool followTrainer = false)
    {
        // Debug.Log($"fhhtest, char {transform.name}, mod {posXMod},{posYMod}");
        Constants.PositionToIndex(transform.position, out int sx, out int sy);
        Constants.PositionToIndex(target.transform.position, out int tx, out int ty);

        // 在同一间房间，直接追击
        if (LevelManager.Instance.RoomGrid[sx, sy] == LevelManager.Instance.RoomGrid[tx, ty])
        {
            Move_ChaseInRoom(target, followTrainer);
        }
        else
        {
            teleportCoroutine ??= StartCoroutine(Teleport(0.5f, characterStatus.Trainer.gameObject));
        }
    }
}