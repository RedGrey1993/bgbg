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
    public float damageInterval = 0.3f;
    public int minDamage = 10;
    private float nextDamageTime = 0;
    private float curScale = 1;
    private bool scaleUp = true;
    private bool scaleDown = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameObject.transform.localRotation = Quaternion.LookRotation(Vector3.forward, Direction);
    }

    // Update is called once per frame
    void Update()
    {
        if (OwnerStatus != null)
        {
            Vector2 tarPos = OwnerStatus.gameObject.transform.position;
            tarPos += PosOffset;
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
        else if (collision.gameObject.CompareThisAndParentTag(Constants.TagPlayer)
                || collision.gameObject.CompareThisAndParentTag(Constants.TagEnemy))
        {
            CharacterStatus tarStatus = collision.gameObject.GetComponentInParent<CharacterStatus>();
            if (tarStatus == null || tarStatus == OwnerStatus || tarStatus.Trainer == OwnerStatus)
            { // 如果是碰撞到Player或Enemy发射/生成的道具或物品（Tag和创建者相同），也不做任何处理
                return; // 不伤害自己
            }

            // 敌人之间不互相伤害；
            if (tarStatus.gameObject.CompareTag(Constants.TagEnemy) == true
                && (OwnerStatus == null || OwnerStatus.gameObject.CompareTag(Constants.TagEnemy) == true))
            // 只有TheRuler不设置OwnerStatus，因为统治者调用这个技能时，是固定在房间中间发射
            {
                return;
            }

            Vector2 diff = Direction * Time.deltaTime * pushForce;
            var tarInput = collision.GetComponent<CharacterInput>();
            tarInput.MoveAdditionalInput += diff;
            if (Time.time > nextDamageTime)
            {
                var damage = minDamage;
                if (OwnerStatus != null)
                {
                    damage = Mathf.Max(minDamage, OwnerStatus.State.Damage);
                }
                tarStatus.TakeDamage_Host(damage, OwnerStatus);
                nextDamageTime = Time.time + damageInterval;
            }
        }
    }
}
