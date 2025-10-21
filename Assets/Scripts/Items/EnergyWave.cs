using UnityEngine;

public class EnergyWave : MonoBehaviour
{
    public Vector2 StartPosition { get; set; }
    public Vector2 Direction { get; set; }
    public int Rotate { get; set; } = 0;
    public float scaleSpeed = 20f;
    public float rotateSpeed = 20f;
    public float pushForce = 5f; // 推力大小
    public CharacterStatus OwnerStatus { get; set; }
    public float damageInterval = 1;
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
        else if (collision.CompareTag(Constants.TagPlayer))
        {
            Vector2 diff = Direction * Time.deltaTime * pushForce;
            var tarInput = collision.GetComponent<CharacterInput>();
            tarInput.MoveAdditionalInput += diff;
            if (Time.time > nextDamageTime)
            {
                var tarStatus = collision.GetComponent<CharacterStatus>();
                tarStatus.TakeDamage_Host(OwnerStatus);
                nextDamageTime = Time.time + damageInterval;
            }
        }
    }
}
