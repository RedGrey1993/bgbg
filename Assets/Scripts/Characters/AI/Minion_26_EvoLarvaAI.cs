using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Minion_26_EvoLarvaAI : CharacterBaseAI
{
    public int envolveTime = 10;
    public GameObject vfxObject;
    public GameObject energyWavePrefab;
    public AudioClip energyWaveAccumulateSound;
    public AudioClip energyWaveShootSound;
    
    private Slider envolveSlider;
    private bool envolved = false;
    private float startEnvolveTime;
    private bool firstSameRoom = false;

    protected override void SubclassStart()
    {
        var sliders = GetComponentsInChildren<Slider>(true);
        foreach (var slider in sliders)
        {
            if (slider.name == "ExpSlider")
            {
                envolveSlider = slider;
                break;
            }
        }
        envolveSlider.gameObject.SetActive(true);

        envolveSlider.maxValue = envolveTime;
        envolveSlider.value = 0;

        characterStatus.State.CurrentHp = characterStatus.State.MaxHp = CharacterData.MaxHp / 10;
        characterStatus.State.ExpGiven = CharacterData.ExpGiven / 10;
        characterStatus.State.ShootRange = 0;
        characterStatus.State.MoveSpeed = CharacterData.MoveSpeed * 2f;
    }

    protected override void LookToAction()
    {
        if (envolved) {
            Transform trans = transform.GetChild(0);
            trans.localRotation = Quaternion.identity;

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

            if (LookDir.x > 0)
            {
                var scale = transform.localScale;
                scale.x = -Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
            else
            {
                var scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
        }
        else
        {
            base.LookToAction();
        }
    }

    protected override void SubclassFixedUpdate()
    {
        if (!firstSameRoom && LevelManager.Instance.InSameRoom(gameObject, 
            CharacterManager.Instance.GetMyselfGameObject()))
        {
            firstSameRoom = true;
            startEnvolveTime = Time.time;
        }
        else if (!envolved && firstSameRoom)
        {
            float elapsedTime = Time.time - startEnvolveTime;
            if (elapsedTime >= envolveTime)
            {
                envolved = true;
                envolveSlider.gameObject.SetActive(false);
                characterStatus.State.CurrentHp = characterStatus.State.MaxHp = CharacterData.MaxHp;
                characterStatus.State.ExpGiven = CharacterData.ExpGiven;
                characterStatus.State.ShootRange = CharacterData.ShootRange;
                characterStatus.State.MoveSpeed = CharacterData.MoveSpeed;
                animator.SetTrigger("Envolve");
            }
            else {
                envolveSlider.maxValue = envolveTime;
                envolveSlider.value = elapsedTime;
            }
        }
    }

    protected override void Move_ChaseInRoom(GameObject target, bool followTrainer = false)
    {
        if (envolved || followTrainer)
        {
            base.Move_ChaseInRoom(target, followTrainer);
        }
        else
        {
            int roomId = LevelManager.Instance.GetRoomNoByPosition(target.transform.position);
            Rect room = LevelManager.Instance.Rooms[roomId];
            Vector2 tarPos = room.center;
            if (target.transform.position.x < tarPos.x) tarPos.x = room.xMax - col2D.bounds.extents.x;
            else tarPos.x = room.xMin + col2D.bounds.extents.x;

            if (target.transform.position.y < tarPos.y) tarPos.y = room.yMax - col2D.bounds.extents.y;
            else tarPos.y = room.yMin + col2D.bounds.extents.y;

            var diff = tarPos - (Vector2)transform.position;
            // 不能斜向攻击或移动，优先走距离短的那个方向，直到处于同一个水平或竖直方向
            if ((!CharacterData.canAttackDiagonally || !CharacterData.canMoveDiagonally)
                && Mathf.Min(Mathf.Abs(diff.x), Mathf.Abs(diff.y)) > Mathf.Min(col2D.bounds.extents.x, col2D.bounds.extents.y))
            {
                if (Mathf.Abs(diff.x) < Mathf.Abs(diff.y) && !XNearWall())
                {
                    // diff.x *= 10;
                    diff.y = 0;
                }
                else if (Mathf.Abs(diff.y) < Mathf.Abs(diff.x) && !YNearWall())
                {
                    diff.x = 0;
                    // diff.y *= 10;
                }
            }

            characterInput.MoveInput = diff.normalized;
        }
    }

    private Coroutine atkCoroutine = null;
    protected override void AttackAction()
    {
        if (IsAtkCoroutineIdle() && envolved && Time.time - startEnvolveTime > envolveTime + 3)
        {
            Vector2 lookInput = characterInput.LookInput;
            if (lookInput.sqrMagnitude < 0.1f || AggroTarget == null) return;

            atkCoroutine ??= StartCoroutine(Attack_HyperBeam(AggroTarget));
        }
    }

    private IEnumerator Attack_HyperBeam(GameObject aggroTarget)
    {
        isAttack = true;
        
        // animator.SetTrigger("Attack");

        vfxObject.SetActive(true);
        OneShotAudioSource.PlayOneShot(energyWaveAccumulateSound);
        yield return new WaitForSeconds(0.7f);
        var dir = (aggroTarget.transform.position - transform.position).normalized;
        yield return new WaitForSeconds(0.5f);

        vfxObject.SetActive(false);

        var energeWave = LevelManager.Instance.InstantiateTemporaryObject(energyWavePrefab, vfxObject.transform.position);
        EnergyWave energyWaveScript = energeWave.GetComponent<EnergyWave>();
        energyWaveScript.PosOffset = Vector2.zero;
        energyWaveScript.Direction = dir;
        energyWaveScript.OwnerStatus = characterStatus;
        energyWaveScript.FollowOwner = false;
        energyWaveScript.Rotate = 0;

        OneShotAudioSource.PlayOneShot(energyWaveShootSound);

        Destroy(energeWave, 3f);
        yield return new WaitForSeconds(3f);

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
}