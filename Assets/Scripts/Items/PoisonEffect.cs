using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    private float nextDamageTime = 0;
    private void ContinueDamage(Collider2D collision)
    {
        if (collision.gameObject.CompareTag(Constants.TagPlayerFeet))
        {
            if (Time.time > nextDamageTime)
            {
                var status = collision.gameObject.GetComponentInParent<CharacterStatus>();
                status.TakeDamage_Host(1, null);
                nextDamageTime = Time.time + 1f;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        ContinueDamage(collision);
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        ContinueDamage(collision);
    }
}
