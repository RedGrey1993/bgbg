using UnityEngine;

public class Minion_23_ReflectorAI : CharacterBaseAI
{
    protected override void UpdateMoveInput() {}

    protected override void UpdateAttackInput() {}

    protected override void SubclassTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == Constants.bulletsLayer)
        {
            var bs = other.gameObject.GetBullet();
            if (bs.OwnerStatus == characterStatus || bs.OwnerStatus == characterStatus.Trainer)
                return;

            if (AggroTarget != null)
            {
                Vector2 tarDir = AggroTarget.transform.position - transform.position;

                var bulletStartPosition = other.transform.position;
                GameObject bullet = GameManager.Instance.GetObject(other.gameObject, bulletStartPosition);
                bullet.tag = gameObject.tag;
                if (bullet.layer == Constants.defaultLayer) bullet.layer = gameObject.layer;
                bullet.transform.localRotation = Quaternion.LookRotation(Vector3.forward, tarDir);
                bullet.transform.localScale = transform.localScale;

                var bulletScript = bullet.GetBullet();
                if (bulletScript)
                {
                    bulletScript.OwnerStatus = characterStatus;
                    bulletScript.StartPosition = bulletStartPosition;
                    bulletScript.BulletState = characterStatus.bulletState;
                    bulletScript.AggroTarget = AggroTarget;
                    bulletScript.rb.linearVelocity = tarDir.normalized * characterStatus.State.BulletSpeed;
                }
            }
        }
    }
}