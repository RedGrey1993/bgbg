using UnityEngine;

public class AirPoisonEffect : MonoBehaviour
{
    public float damageInterval = 1f;
    public float damage = 1f;
    public CharacterStatus OwnerStatus { get; set; }
    public Rigidbody2D rb { get; private set; }

    private float nextDamageTime = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void ContinueDamage(Collider2D other)
    {
        if (Time.time > nextDamageTime && other.IsPlayerOrEnemy())
        {
            CharacterStatus tarStatus = other.GetCharacterStatus();
            if (tarStatus == null || (OwnerStatus != null && OwnerStatus.IsFriendlyUnit(tarStatus)))
                return;

            tarStatus.TakeDamage_Host(damage, null, DamageType.Collision);
            nextDamageTime = Time.time + damageInterval;
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
