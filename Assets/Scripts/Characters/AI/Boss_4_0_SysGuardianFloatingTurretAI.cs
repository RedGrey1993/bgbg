using System.Collections;
using Unity.AppUI.Core;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]

// Stomper不会对角线移动
public class Boss_4_0_SysGuardianFloatingTurretAI : CharacterBaseAI
{
    [Tooltip("炮弹间隔角度")]
    public int stepAngle = 30;
    public GameObject shootEffect;

    protected override void UpdateMoveInput()
    {
        characterInput.MoveInput = Vector2.zero;
    }

    protected override void UpdateAttackInput()
    {
        characterInput.LookInput = Vector2.zero;
    }

    public override void OnDeath()
    {
        gameObject.SetActive(false);
    }

    public void TurretShoot(Vector2 shootDir)
    {
        if (Time.time < nextAtkTime) return;
        nextAtkTime = Time.time + 1f / characterStatus.State.AttackFrequency;

        StartCoroutine(TurretShootCoroutine(shootDir));
    }

    public IEnumerator TurretShootCoroutine(Vector2 shootDir)
    {
        if (shootEffect == null) yield break;
        shootEffect.SetActive(true);
        yield return new WaitForSeconds(1f);
        Vector2 bulletStartPosition = shootEffect.transform.position;
        shootEffect.SetActive(false);

        if (CharacterData.shootSound)
        {
            var audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.PlayOneShot(CharacterData.shootSound);
            Destroy(audioSrc, CharacterData.shootSound.length);
        }

        var startDir = Quaternion.Euler(0, 0, -60) * shootDir.normalized;
        int num = 120 / stepAngle;
        Quaternion rotationPlus = Quaternion.Euler(0, 0, stepAngle);

        for (int i = 0; i <= num; i++)
        {
            GameObject bullet = GameManager.Instance.GetObject(CharacterData.bulletPrefab, bulletStartPosition);
            bullet.tag = gameObject.tag;
            if (bullet.layer == Constants.defaultLayer) bullet.layer = gameObject.layer;
            bullet.transform.localRotation = Quaternion.LookRotation(Vector3.forward, startDir);
            bullet.transform.localScale = transform.localScale;
            var bulletScript = bullet.GetBullet();
            if (bulletScript)
            {
                bulletScript.OwnerStatus = characterStatus;
                bulletScript.StartPosition = bulletStartPosition;
                bulletScript.rb.linearVelocity = startDir.normalized * characterStatus.State.BulletSpeed;
            }

            startDir = rotationPlus * startDir;
        }
    }
}