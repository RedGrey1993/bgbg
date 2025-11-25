using System.Collections;
using UnityEngine;

public class Minion_25_SporeShroomAI : CharacterBaseAI
{
    public GameObject poisonFogPrefab;
    public AudioClip poisonFogSound;

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
            scale.x = Mathf.Abs(scale.x);
            trans.localScale = scale;
        }
        else
        {
            var scale = trans.localScale;
            scale.x = -Mathf.Abs(scale.x);
            trans.localScale = scale;
        }
    }

    protected override void UpdateMoveInput() {}

    private Coroutine atkCoroutine = null;
    protected override void AttackAction()
    {
        if (IsAtkCoroutineIdle())
        {
            Vector2 lookInput = characterInput.LookInput;
            if (lookInput.sqrMagnitude < 0.1f) return;

            atkCoroutine ??= StartCoroutine(Attack_PoisonFog(lookInput));
        }
    }

    private IEnumerator Attack_PoisonFog(Vector2 lookInput)
    {
        isAttack = true;
        
        animator.SetTrigger("Attack");

        yield return new WaitForSeconds(1f);

        Vector2 poisonFogOffset = lookInput.normalized * (col2D.bounds.extents.magnitude + 0.1f);
        Vector2 poisonFogtPosition = transform.position;
        poisonFogtPosition += poisonFogOffset;

        var poisonFog = LevelManager.Instance.InstantiateTemporaryObject(poisonFogPrefab, poisonFogtPosition);
        AirPoisonEffect poisonFogScript = poisonFog.GetComponent<AirPoisonEffect>();
        poisonFogScript.OwnerStatus = characterStatus;
        poisonFogScript.rb.linearVelocity = lookInput * characterStatus.State.BulletSpeed;

        OneShotAudioSource.PlayOneShot(poisonFogSound);

        Destroy(poisonFog, 3f);

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