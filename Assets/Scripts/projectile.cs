using System.Collections.Generic;
using UnityEngine;

public class projectile : MonoBehaviour
{
    private Vector2 direction;
    private float speed = 10f;
    private float damage = 10f;
    private float knockbackForce = 1f;
    private int remainingPierce = 0;
    private charStats attackerStats;
    private bool isInitialized = false;
    private Rigidbody2D rb;

    [SerializeField] private float lifetime = 5f;
    private readonly HashSet<Collider2D> hitColliders = new HashSet<Collider2D>();

    public void Setup(Vector2 dir, float spd, float dmg = 10f, float knockback = 1f, int pierce = 0,
        charStats attacker = null)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        knockbackForce = knockback;
        remainingPierce = pierce;
        attackerStats = attacker;
        isInitialized = true;

        // Rotate projectile to match shot direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // Automatically clean up bullet after lifetime expires
        Destroy(gameObject, lifetime);

        if (TryGetComponent<Rigidbody2D>(out rb))
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = direction * speed;
        }
    }

    void Update()
    {
        if (!isInitialized) return;

        // Move bullet via transform if no Rigidbody2D is present
        if (rb == null)
        {
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Ignore collisions with player and player children
        if (collision.transform.IsChildOf(transform.root) || collision.CompareTag("Player"))
        {
            return;
        }

        // Prevent repeated hits on the same collider during penetration
        if (hitColliders.Contains(collision))
        {
            return;
        }

        if (collision.CompareTag("Enemy"))
        {
            hitColliders.Add(collision);

            if (collision.TryGetComponent<charStats>(out var enemyStats))
            {
                float appliedDamage = enemyStats.TakeDamageAndGetApplied(damage, direction, knockbackForce);
                if (appliedDamage > 0f && attackerStats != null && attackerStats.VampirismPercent > 0f)
                {
                    attackerStats.Heal(appliedDamage * attackerStats.VampirismPercent);
                }
            }

            if (remainingPierce > 0)
            {
                remainingPierce--;
            }
            else
            {
                Destroy(gameObject);
            }
        }
        else if (collision.CompareTag("Obstacle"))
        {
            Destroy(gameObject);
        }
    }
}
