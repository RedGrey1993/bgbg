

using System.Collections;
using UnityEngine;

public class Minion_14_HypnoMothAI : CharacterBaseAI
{
    private Coroutine atkCoroutine = null;
    protected override void AttackAction()
    {
        if (atkCoroutine == null)
        {
            Vector2 lookInput = characterInput.LookInput;
            if (lookInput.sqrMagnitude < 0.1f) return;

            atkCoroutine = StartCoroutine(AttackShootSuperSonic(lookInput));
        }
    }

    private IEnumerator AttackShootSuperSonic(Vector2 lookInput)
    {
        isAttack = true;
        // animator.SetTrigger("Shoot");
        // yield return new WaitForSeconds(1f);
        OneShotAudioSource.PlayOneShot(CharacterData.shootSound);

        Bounds playerBounds = col2D.bounds;
        // 计算子弹的初始位置，稍微偏离玩家边界
        Vector2 bulletOffset = lookInput.normalized * (playerBounds.extents.magnitude + 0.1f);
        Vector2 bulletStartPosition = transform.position;
        bulletStartPosition += bulletOffset;

        var bulletState = characterStatus.bulletState;
        // Instantiate the bullet
        GameObject bullet = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.bulletPrefab, bulletStartPosition);
        bullet.tag = gameObject.tag;
        if (bullet.layer == LayerMask.NameToLayer("Default")) bullet.layer = gameObject.layer;
        bullet.transform.localRotation = Quaternion.LookRotation(Vector3.forward, lookInput);
        bullet.transform.localScale = transform.localScale;
        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript)
        {
            bulletScript.OwnerStatus = characterStatus;
            bulletScript.StartPosition = bulletStartPosition;
            bulletScript.BulletState = bulletState;
        }

        // Get the bullet's Rigidbody2D component
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        // Set the bullet's velocity
        if (bulletRb) bulletRb.linearVelocity = lookInput * characterStatus.State.BulletSpeed;

        yield return new WaitForSeconds(2f);
        isAttack = false;
        atkCoroutine = null;
    }

    #region OnDeath
    public override void OnDeath()
    {
        col2D.isTrigger = true;
        Destroy(gameObject);
    }
    #endregion
}