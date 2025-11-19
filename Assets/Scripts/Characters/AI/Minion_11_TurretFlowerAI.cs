

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

public class Minion_11_TurretFlowerAI : CharacterBaseAI
{
    protected override void UpdateMoveInput() { }

    private Coroutine atkCoroutine = null;
    protected override void AttackAction()
    {
        if (atkCoroutine == null)
        {
            Vector2 lookInput = characterInput.LookInput;
            if (lookInput.sqrMagnitude < 0.1f) return;

            atkCoroutine = StartCoroutine(AttackShoot4Dir());
        }
    }

    private IEnumerator AttackShoot4Dir()
    {
        isAttack = true;
        animator.SetTrigger("Shoot");
        yield return new WaitForSeconds(1f);

        OneShotAudioSource.PlayOneShot(CharacterData.shootSound);
        Vector2[] lookDirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        // 获取Player的位置
        // 获取Player碰撞体的边界位置
        Bounds playerBounds = col2D.bounds;
        // 计算子弹的初始位置，稍微偏离玩家边界
        Vector2 bulletStartPosition = transform.position;

        var bulletState = characterStatus.bulletState;
        foreach (Vector2 startDir in lookDirs)
        {
            // Instantiate the bullet
            GameObject bullet = LevelManager.Instance.InstantiateTemporaryObject(CharacterData.bulletPrefab, bulletStartPosition);
            bullet.tag = gameObject.tag;
            if (bullet.layer == LayerMask.NameToLayer("Default")) bullet.layer = gameObject.layer;
            bullet.transform.localRotation = Quaternion.LookRotation(Vector3.forward, startDir);
            bullet.transform.localScale = transform.localScale;
            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript)
            {
                Vector2 bulletOffset = startDir.normalized * (playerBounds.extents.magnitude + 0.1f);

                bulletScript.OwnerStatus = characterStatus;
                bulletScript.StartPosition = bulletStartPosition + bulletOffset;
                bulletScript.BulletState = bulletState;
            }

            // Get the bullet's Rigidbody2D component
            Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
            // Set the bullet's velocity
            if (bulletRb) bulletRb.linearVelocity = startDir * characterStatus.State.BulletSpeed;
        }

        yield return new WaitForSeconds(2f);
        isAttack = false;
        atkCoroutine = null;
    }

    #region OnDeath
    public override void OnDeath()
    {
        animator.SetTrigger("Death");
        float deathDuration = 2.53f;
        Destroy(gameObject, deathDuration);
    }

    #endregion
}