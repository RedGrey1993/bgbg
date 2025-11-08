using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.EditorTools;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

// Stomper不会对角线移动
public class Boss_4_0_SysGuardianAI : CharacterBaseAI
{
    [Tooltip("浮动的炮塔")]
    public List<GameObject> floatingTurrets;
    [Tooltip("浮动的幅度 (高度)")]
    public float amplitude = 10f;
    [Tooltip("浮动的速度")]
    public float floatSpeed = 1.5f;
    public float turretRebornTime = 15f;
    public float turretMoveSpeed = 5f;
    public List<Transform> layser1ShootPositions;
    public List<Transform> layser2ShootPositions;
    public GameObject laser2BulletPrefab;
    public AudioClip laser2ShootSound;

    public GameObject shield;

    #region AI Logic / Update Input
    // 系统守护者不能移动，会一直坐落在房间最上方
    protected override void UpdateMoveInput()
    {
        characterInput.MoveInput = Vector2.zero;
    }

    private bool floatingTurretsHasAlive = true;
    private bool mainBodyAttack = false;
    // 系统守护者不需要设置LookInput，他在协程中直接攻击最新的目标位置
    protected override void UpdateAttackInput()
    {
        characterInput.LookInput = Vector2.zero;
        if (AggroTarget != null && LevelManager.Instance.InSameRoom(gameObject, AggroTarget)
            && !floatingTurretsHasAlive) // 炮台都死了，主体开始攻击玩家
        {
            mainBodyAttack = true; // 在这里设置是为了避免在还未执行FixedUpdate执行动作的时候，在下一帧Update就把LookInput设置为0的问题
        }
    }
    #endregion

    private Rect room;
    protected override void SubclassStart()
    {
        var roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        room = LevelManager.Instance.Rooms[roomId];

        Debug.Log($"{gameObject.name} in room {roomId}, {room}");
    }

    private bool playerFirstComingRoom = true;
    protected override void SubclassFixedUpdate()
    {
        float yDelta = Mathf.Sin(Time.time * floatSpeed) * amplitude * Time.deltaTime;
        transform.position += new Vector3(0, yDelta, 0);

        floatingTurretsHasAlive = floatingTurrets.Any(go => go.activeSelf);
        if (!floatingTurretsHasAlive)
        {
            StartCoroutine(Reborn());
            return;
        }

        for (int i = 0; i < floatingTurrets.Count; i++)
        {
            GameObject go = floatingTurrets[i];
            if (!go.activeSelf)
            {
                continue;
            }
            go.transform.position += new Vector3(0, yDelta, 0);
            if (AggroTarget != null)
            {
                // 2维，要让z为0，所以使用Vector2，否则在2维中朝向会有问题
                Vector2 diff = AggroTarget.transform.position - go.transform.position;
                diff.y /= 0.2588f; // 除以cos(75°)后朝向更精准
                // 为了不让碰撞体也跟着旋转
                go.transform.GetChild(1).localRotation = Quaternion.LookRotation(new Vector3(0, -0.9659f, 0.2588f), diff); // 75度
            }
        }

        if (AggroTarget != null && LevelManager.Instance.InSameRoom(gameObject, AggroTarget))
        {
            // 玩家第一次进入房间时，让浮动炮塔飞到房间的4个角落
            if (playerFirstComingRoom)
            {
                playerFirstComingRoom = false;
                for (int i = 0; i < floatingTurrets.Count; i++)
                {
                    GameObject go = floatingTurrets[i];
                    if (!go.activeSelf) continue;
                    Vector2 pos = go.transform.position;
                    var col = go.GetComponentInParent<Collider2D>();
                    if (i == 0) // 左下
                    {
                        pos.x = room.xMin + 2 + col.bounds.size.x;
                        pos.y = room.yMin + 2 + col.bounds.size.y;
                    }
                    else if (i == 1) // 右下
                    {
                        pos.x = room.xMax - 1 - col.bounds.size.x;
                        pos.y = room.yMin + 2 + col.bounds.size.y;
                    }
                    else if (i == 2) // 左上
                    {
                        pos.x = room.xMax - 1 - col.bounds.size.x;
                        pos.y = room.yMax - 3 - col.bounds.size.y - col2D.bounds.size.y;
                    }
                    else // (i == 3) // 右上
                    {
                        pos.x = room.xMin + 2 + col.bounds.size.x;
                        pos.y = room.yMax - 3 - col.bounds.size.y - col2D.bounds.size.y;
                    }
                    StartCoroutine(FlyToPos(go, pos));
                }
            }
            else if (flyiedToPos)
            {
                // 玩家和boss在同一个房间时，且已经飞到四角了，则浮动炮塔绕房间四周飞行，并每隔1s朝玩家方向发射半圆形弹幕（弹幕速度不快）
                float eps = 2 * amplitude + 0.5f;
                for (int i = 0; i < floatingTurrets.Count; i++)
                {
                    GameObject go = floatingTurrets[i];
                    if (!go.activeSelf) continue;
                    Vector2 shootDir = AggroTarget.transform.position - go.transform.position;
                    var turretAi = go.GetComponent<Boss_4_0_SysGuardianFloatingTurretAI>();
                    turretAi.TurretShoot(shootDir);

                    var col = go.GetComponentInParent<Collider2D>();
                    var pos = go.transform.position;
                    // 向右飞行
                    if (pos.y < room.yMin + 2 + col.bounds.size.y + eps && pos.x < room.xMax - 1 - col.bounds.size.x)
                    {
                        pos.x += Time.deltaTime * turretMoveSpeed;
                    }
                    // 向上飞行
                    else if (pos.x > room.xMax - 1 - col.bounds.size.x - eps && pos.y < room.yMax - 3 - col.bounds.size.y - col2D.bounds.size.y)
                    {
                        pos.y += Time.deltaTime * turretMoveSpeed;
                    }
                    // 向左飞行
                    else if (pos.y > room.yMax - 3 - col.bounds.size.y - col2D.bounds.size.y - eps && pos.x > room.xMin + 2 + col.bounds.size.x)
                    {
                        pos.x -= Time.deltaTime * turretMoveSpeed;
                    }
                    // 向下飞行
                    else // if (pos.x < room.xMin + 2 + col.bounds.size.x + eps && pos.y > room.yMin + 2 + col.bounds.size.y)
                    {
                        pos.y -= Time.deltaTime * turretMoveSpeed;
                    }
                    go.transform.position = pos;
                }
            }
        }
    }

    private bool flyiedToPos = false;
    private IEnumerator FlyToPos(GameObject go, Vector2 pos, float duration = 2f)
    {
        Vector2 startPos = go.transform.position;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            go.transform.position = Vector2.Lerp(startPos, pos, t);
            yield return null;
        }
        go.transform.position = pos;
        flyiedToPos = true;
    }

    private IEnumerator Reborn()
    {
        yield return new WaitForSeconds(turretRebornTime);
        foreach (GameObject go in floatingTurrets)
        {
            var status = go.GetComponent<CharacterStatus>();
            status.HealthChanged(status.State.MaxHp);
            go.SetActive(true);
            shield.SetActive(true);
        }
        playerFirstComingRoom = true; // 复活后重新回到原位
        flyiedToPos = false;
    }

    #region Attack Actioin
    // 浮动炮塔都死去后，boss护盾消失，浮动炮塔在全部死亡后开始同时隔15s复活
    private Coroutine coreShootLaser1Coroutine = null;
    private Coroutine coreShootLaser2Coroutine = null;
    protected override void AttackAction()
    {
        if (mainBodyAttack)
        {
            mainBodyAttack = false;
            if (coreShootLaser1Coroutine != null && coreShootLaser2Coroutine != null) return;
            if (Time.time < nextAtkTime) { return; }
            nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

            shield.SetActive(false);
            coreShootLaser1Coroutine ??= StartCoroutine(CoreShootLaser1());
            coreShootLaser2Coroutine ??= StartCoroutine(CoreShootLaser2());
        }
    }
    // TODO：boss护盾消失后，向玩家轮流1. 发射扇形交替的能量弹，2. 快速旋转的、没有死角的螺旋弹幕
    private int stepAngle = 40;
    private int shootAngle = 160;
    public IEnumerator CoreShootLaser1()
    {
        var audioSrc = gameObject.AddComponent<AudioSource>();
        int num = shootAngle / stepAngle;
        Quaternion rotationPlus = Quaternion.Euler(0, 0, -stepAngle);

        var diff = Vector2.down;
        for (int shootPosIdx = 0; shootPosIdx < layser1ShootPositions.Count; shootPosIdx++)
        {
            Vector2 bulletStartPosition = layser1ShootPositions[shootPosIdx].position;
            if (AggroTarget != null)
            {
                diff = AggroTarget.transform.position - layser1ShootPositions[shootPosIdx].position;
            }
            var startDir = Quaternion.Euler(0, 0, shootAngle / 2) * diff;
            for (int i = 0; i <= num; i++)
            {
                audioSrc.PlayOneShot(CharacterData.shootSound);

                GameObject bullet = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.bulletPrefab, bulletStartPosition);
                bullet.transform.localRotation = Quaternion.LookRotation(Vector3.forward, startDir);
                Bullet bulletScript = bullet.GetComponent<Bullet>();
                if (bulletScript)
                {
                    bulletScript.OwnerStatus = characterStatus;
                    bulletScript.StartPosition = bulletStartPosition;
                }

                // Get the bullet's Rigidbody2D component
                Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
                // Set the bullet's velocity
                if (bulletRb) bulletRb.linearVelocity = startDir.normalized * characterStatus.State.BulletSpeed;

                startDir = rotationPlus * startDir;

                yield return new WaitForSeconds(0.3f);
            }
        }

        Destroy(audioSrc, CharacterData.shootSound.length);
        coreShootLaser1Coroutine = null;
    }
    
    public IEnumerator CoreShootLaser2()
    {
        var audioSrc = gameObject.AddComponent<AudioSource>();

        int num = shootAngle / stepAngle;
        Quaternion rotationPlus = Quaternion.Euler(0, 0, -stepAngle);

        for (int shootPosIdx = 0; shootPosIdx < layser2ShootPositions.Count; shootPosIdx++)
        {
            var startDir = Quaternion.Euler(0, 0, shootAngle / 2) * new Vector2(0, -1);
            Vector2 bulletStartPosition = layser2ShootPositions[shootPosIdx].position;
            for (int i = 0; i <= num; i++)
            {
                audioSrc.PlayOneShot(laser2ShootSound);

                GameObject bullet = LevelManager.Instance.InstantiateTemporaryObject(laser2BulletPrefab, bulletStartPosition);
                bullet.transform.localRotation = Quaternion.LookRotation(Vector3.forward, startDir);
                Bullet bulletScript = bullet.GetComponent<Bullet>();
                if (bulletScript)
                {
                    bulletScript.OwnerStatus = characterStatus;
                    bulletScript.StartPosition = bulletStartPosition;
                }

                // Get the bullet's Rigidbody2D component
                Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
                // Set the bullet's velocity
                if (bulletRb) bulletRb.linearVelocity = startDir.normalized * characterStatus.State.BulletSpeed;

                startDir = rotationPlus * startDir;
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        Destroy(audioSrc, CharacterData.shootSound.length);
        coreShootLaser2Coroutine = null;
    }
    #endregion
}