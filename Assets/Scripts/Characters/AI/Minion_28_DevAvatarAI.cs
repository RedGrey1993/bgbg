using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// 开发者替身 (Dev Avatar)
// 特征： 一个疲惫的程序员形象，黑眼圈极重。
// 机制： 拥有“作弊码”。每隔10s会无敌3秒，头上状态会显示"Cheat Activated"
// 平时像尸体一样不动，如果你攻击他，他会变为小Boss，血量*20，开启狂暴追杀你。彩蛋怪。
// 会跨房间追逐
public class Minion_28_DevAvatarAI : CharacterBaseAI
{
    public GameObject backgroundPrefab;
    public GameObject spriteObject;
    public GameObject meshObject;
    public Collider2D shieldCollider;
    public float invincibleTime;
    public float invincibleInterval;

    [Header("彩虹设置")]
    [Tooltip("颜色循环的速度，值越大变得越快")]
    public float cycleSpeed = 2f;
    // [Tooltip("饱和度 (0-1): 1是最鲜艳，0是黑白")]
    private float saturation = 1f;
    // [Tooltip("明度 (0-1): 1是最亮，0是黑色")]
    private float lightness = 1f;

    private Animator spriteAnimator;
    private bool firstBeAttacked = false;
    protected override void SubclassStart()
    {
        MiniStatusCanvas.gameObject.SetActive(false);
        shieldCollider.enabled = false;
        var back = LevelManager.Instance.InstantiateTemporaryObject(backgroundPrefab, transform.position);
        back.transform.localScale = backgroundPrefab.transform.localScale * characterStatus.State.Scale;
        characterStatus.State.MoveSpeed = 0;

        var animators = GetComponentsInChildren<Animator>(true);
        animator = null;
        foreach (var anim in animators)
        {
            if (anim.name == "Minion_28_DevAvatar_Sprite")
                spriteAnimator = anim;
            else if (anim.name == "Minion_28_DevAvatar_Mesh")
                animator = anim;
        }
    }

    private bool firstFindAggroTarget = true;
    // 寻找距离最近的仇恨目标，不需要在同一个房间
    protected override void UpdateAggroTarget()
    {
        if (Time.time >= nextAggroChangeTime)
        {
            nextAggroChangeTime = Time.time + CharacterData.AggroChangeInterval;
            AggroTarget = CharacterManager.Instance.FindNearestEnemyInAngle(gameObject, LookDir, 180, firstFindAggroTarget);
            if (AggroTarget != null) firstFindAggroTarget = false;
            Debug.Log($"fhhtest, {name} aggro target: {AggroTarget?.name}");
        }
    }
    protected override void UpdateMoveInput()
    {
        if (firstBeAttacked)
        {
            base.UpdateMoveInput();
        }
    }
    protected override void UpdateAttackInput()
    {
        if (firstBeAttacked)
        {
            base.UpdateAttackInput();
        }
    }

    protected override bool IsAtkCoroutineIdle()
    {
        return throwCoroutine == null;
    }

    protected override void AttackAction()
    {
        if (IsAtkCoroutineIdle())
        {
            Vector2 lookInput = characterInput.LookInput;
            if (lookInput.sqrMagnitude < 0.1f) return;

            throwCoroutine = StartCoroutine(AttackThrow(lookInput));
        }
    }

    private Coroutine throwCoroutine = null;
    private IEnumerator AttackThrow(Vector2 lookInput)
    {
        isAttack = true;
        float startTime = Time.time;
        float atkInterval = 1f / characterStatus.State.AttackFrequency;
        float throwTime = 0.87f;
        float speed = 1;
        if (atkInterval < throwTime)
        {
            speed = throwTime / atkInterval;
        }
        SetShootAnimation(speed);
        if (atkInterval >= throwTime)
        {
            yield return new WaitForSeconds(throwTime);
        }
        else
        {
            yield return new WaitForSeconds(atkInterval);
        }
        float elapsedTime = Time.time - startTime;
        yield return StartCoroutine(AttackShoot(lookInput, atkInterval - elapsedTime, 1));

        isAttack = false;
        if (isAi)
        {
            // 攻击完之后给1-3s的移动，避免呆在原地一直攻击
            yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));
        }
        throwCoroutine = null;
    }

    private float nxtInvincibleTime = 0;
    protected override void SubclassFixedUpdate()
    {
        if (!firstBeAttacked && changeTo3DCoroutine == null && characterStatus.State.CurrentHp < characterStatus.State.MaxHp)
        {
            MiniStatusCanvas.gameObject.SetActive(true);

            changeTo3DCoroutine = StartCoroutine(ChangeTo3D());
        }
        else if (firstBeAttacked && Time.time >= nxtInvincibleTime && invincibleCoroutine == null)
        {
            invincibleCoroutine = StartCoroutine(Invincible(invincibleTime));
            nxtInvincibleTime = Time.time + invincibleInterval;
        }
    }

    private Coroutine changeTo3DCoroutine = null;
    private IEnumerator ChangeTo3D()
    {
        spriteAnimator.SetTrigger("ChangeTo3D");
        yield return new WaitForSeconds(2.4f);
        spriteObject.SetActive(false);
        meshObject.SetActive(true);
        characterStatus.State.MoveSpeed = CharacterData.MoveSpeed;
        characterStatus.State.MaxHp = CharacterData.MaxHp * characterStatus.State.Scale * 10;
        characterStatus.HealthChanged(characterStatus.State.MaxHp);
        skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        firstBeAttacked = true;
    }

    private Coroutine invincibleCoroutine = null;
    private IEnumerator Invincible(float duration)
    {
        shieldCollider.enabled = true;
        Color originalColor = characterStatus.State.Color.ToColor();
        float stopTime = Time.time + duration;
        while (Time.time < stopTime)
        {
            // 1. 计算当前的色相 (Hue)
            // Time.time * cycleSpeed 让时间动起来
            // Mathf.Repeat(..., 1f) 确保结果永远在 0 到 1 之间循环 (相当于取余数)
            float currentHue = Mathf.Repeat(Time.time * cycleSpeed, 1f);

            // 2. 将 HSV 转换为 Unity 能用的 RGB 颜色
            Color rainbowColor = Color.HSVToRGB(currentHue, saturation, lightness);

            // 3. 应用颜色
            characterStatus.SetColor(rainbowColor);
            yield return null;
        }
        characterStatus.SetColor(originalColor);
        shieldCollider.enabled = false;
        invincibleCoroutine = null;
    }
}