using System.Collections;
using UnityEngine;

// 管理员的凝视 (Administrator's Gaze/激光炮塔)
// 第5层每个房间都会有，只会生成在房间的角落或边缘，
// 先是一条无视碰撞的红色光柱显示攻击路径，一秒后发射出无视碰撞的激光
public class Minion_29_AdminGazeAI : CharacterBaseAI
{
    public GameObject vfxPrefab;
    public GameObject energyWavePrefab;
    public AudioClip energyWaveAccumulateSound;
    public AudioClip energyWaveShootSound;
    public Transform laserStartPoint;
    public SimpleLaser simpleLaser;

    public float rotateSpeed;
    private float rotateZ = 0;
    private bool isRotate = true;
    private Transform child;
    private Coroutine atkCoroutine = null;
    private Rect room;
    private float maxDis = 10f;

    protected override void SubclassStart()
    {
        child = transform.GetChild(0);

        simpleLaser.StartPoint = laserStartPoint;

        int roomId = LevelManager.Instance.GetRoomNoByPosition(transform.position);
        room = LevelManager.Instance.Rooms[roomId];
    }

    protected override void SubclassFixedUpdate()
    {
        if (isRotate) 
        {
            rotateZ += rotateSpeed * Time.fixedDeltaTime;
            rotateZ %= 360;

            Vector2 lookDir = new Vector2(Mathf.Cos(rotateZ * Mathf.Deg2Rad), Mathf.Sin(rotateZ * Mathf.Deg2Rad)).normalized;

            float ratio = 1000;
            if (lookDir.x > 0.1f) ratio = Mathf.Min(ratio, (room.xMax - laserStartPoint.position.x) / lookDir.x);
            if (lookDir.x < -0.1f) ratio = Mathf.Min(ratio, (room.xMin + 1 - laserStartPoint.position.x) / lookDir.x);
            if (lookDir.y > 0.1f) ratio = Mathf.Min(ratio, (room.yMax - laserStartPoint.position.y) / lookDir.y);
            if (lookDir.y < -0.1f) ratio = Mathf.Min(ratio, (room.yMin + 1 - laserStartPoint.position.y) / lookDir.y);

            Vector2 tarPos = (Vector2)laserStartPoint.position + (lookDir * ratio);
            maxDis = (tarPos - (Vector2)transform.position).magnitude;
            
            simpleLaser.dir = lookDir;
            simpleLaser.MaxDistance = maxDis;
            // lookDir.y /= 0.2588f; // 除以cos(75°)后朝向更精准
            // 为了不让碰撞体也跟着旋转
            child.localRotation = Quaternion.LookRotation(new Vector3(0, -0.71711f, 0.71711f), lookDir); // 45度

            if (AggroTarget != null 
                && Vector2.Angle(lookDir, (Vector2)(AggroTarget.transform.position - transform.position)) < 1)
            {
                characterInput.LookInput = lookDir;
                isRotate = false;
            }
        }
    }

    protected override void UpdateMoveInput() {}
    protected override void UpdateAttackInput() {}

    protected override void AttackAction()
    {
        if (IsAtkCoroutineIdle())
        {
            Vector2 lookInput = characterInput.LookInput;
            if (lookInput.sqrMagnitude < 0.1f) return;

            atkCoroutine ??= StartCoroutine(Attack_LaserBeam(lookInput));
        }
    }

    private IEnumerator Attack_LaserBeam(Vector2 lookInput)
    {
        isAttack = true;
        
        var vfx = Instantiate(vfxPrefab, laserStartPoint);
        vfx.transform.localScale = Vector3.one * 0.3f;
        TobeDestroyed.Add(vfx);
        OneShotAudioSource.PlayOneShot(energyWaveAccumulateSound);

        yield return new WaitForSeconds(7f);

        Destroy(vfx);
        TobeDestroyed.Remove(vfx);

        var energeWave = LevelManager.Instance.InstantiateTemporaryObject(energyWavePrefab, laserStartPoint.position);
        EnergyWave energyWaveScript = energeWave.GetComponent<EnergyWave>();
        energyWaveScript.FollowOwner = false;
        energyWaveScript.Direction = lookInput.normalized;
        energyWaveScript.OwnerStatus = characterStatus;
        energyWaveScript.PenetrateWall = true;
        energyWaveScript.MaxLength = maxDis;

        OneShotAudioSource.PlayOneShot(energyWaveShootSound);

        Destroy(energeWave, 3f);
        yield return new WaitForSeconds(3f);

        isAttack = false; // isAttack=false后就不再设置朝向为LookInput，而是朝向MoveInput
        characterInput.LookInput = Vector2.zero;
        isRotate = true;
        // shootCoroutine = null后才能再次使用该技能
        atkCoroutine = null;
    }

    protected override bool IsAtkCoroutineIdle()
    {
        return atkCoroutine == null;
    }
}