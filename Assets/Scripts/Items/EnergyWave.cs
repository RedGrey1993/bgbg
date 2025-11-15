using UnityEngine;

public class EnergyWave : MonoBehaviour
{
    public Vector2 PosOffset { get; set; }
    public Vector2 Direction { get; set; }
    public int Rotate { get; set; } = 0;
    public float scaleSpeed = 20f;
    public float rotateSpeed = 20f;
    public float pushForce = 5f; // 推力大小
    public CharacterStatus OwnerStatus { get; set; }
    public bool FollowOwner { get; set; } = true;
    public float damageInterval = 0.3f;
    public int minDamage = 2;
    private float nextDamageTime = 0;
    private float curScale = 1;
    private bool scaleUp = true;
    private bool scaleDown = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameObject.transform.localRotation = Quaternion.LookRotation(Vector3.forward, Direction);
    }

    void FixedUpdate()
    {
        if (OwnerStatus != null && FollowOwner)
        {
            Vector2 tarPos = OwnerStatus.gameObject.transform.position;
            Vector2 offset = transform.up * PosOffset.magnitude;
            tarPos += offset;
            gameObject.transform.position = tarPos;
        }

        if (scaleUp)
        {
            curScale += Time.deltaTime * scaleSpeed;
            gameObject.transform.localScale = Vector3.one * curScale;
        }
        else if (scaleDown)
        {
            curScale -= Time.deltaTime * scaleSpeed;
            gameObject.transform.localScale = Vector3.one * curScale;
        }

        if (Rotate != 0)
        {
            gameObject.transform.Rotate(0, 0, Rotate * Time.deltaTime * rotateSpeed);
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(Constants.TagWall))
        {
            scaleUp = false;
            scaleDown = false;
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag(Constants.TagWall))
        {
            scaleUp = true;
            scaleDown = false;
        }
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag(Constants.TagWall))
        {
            scaleUp = false;
            scaleDown = true;
        }
        else if (collision.IsPlayerOrEnemy())
        {
            CharacterStatus tarStatus = collision.GetCharacterStatus();
            if (tarStatus == null || (OwnerStatus!= null && OwnerStatus.IsFriendlyUnit(tarStatus)))
            { // 如果是碰撞到Player或Enemy发射/生成的道具或物品（Tag和创建者相同），也不做任何处理
                return; // 不伤害自己
            }

            Vector2 diff = Direction * Time.deltaTime * pushForce;
            if (collision.TryGetComponent<CharacterInput>(out CharacterInput tarInput))
            {
                tarInput.MoveAdditionalInput += diff;
            }
            if (Time.time > nextDamageTime)
            {
                if (OwnerStatus != null) // // 如果物体的主人已经死亡，则不再造成伤害
                {
                    var damage = Mathf.Max(minDamage, OwnerStatus.State.Damage);
                    tarStatus.TakeDamage_Host(damage, OwnerStatus, DamageType.Bullet);
                }
                nextDamageTime = Time.time + damageInterval;
            }
        }
    }
}
