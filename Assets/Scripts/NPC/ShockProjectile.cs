using System.Collections.Generic;
using UnityEngine;

public class ShockProjectile : MonoBehaviour
{
    [SerializeField] private ParticleSystem hitEffect;
    [SerializeField] private float speed = 5f;
    [SerializeField] private LayerMask pMask;
    [SerializeField, Min(0.01f)] private float hitRadius = 0.25f;
    [SerializeField, Min(0.1f)] private float lifetime = 5f;

    private Vector2 direction = Vector2.right;
    private float damage = 10f;
    private HashSet<charStats> hitTargets;
    private float age;
    private bool hasHit;

    private void Awake()
    {
        hitTargets = new HashSet<charStats>();
    }

    public void Initialize(Vector2 travelDirection, float projectileDamage, HashSet<charStats> sharedHitTargets = null)
    {
        direction = travelDirection.sqrMagnitude > 0f ? travelDirection.normalized : Vector2.right;
        damage = projectileDamage;
        hitTargets = sharedHitTargets ?? hitTargets;
        transform.right = direction;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += (Vector3)(direction * speed * Time.deltaTime);

        if (hasHit) return;

        Collider2D target = Physics2D.OverlapCircle(transform.position, hitRadius, pMask);
        if (target == null) return;

        charStats targetStats = target.GetComponentInParent<charStats>();
        if (targetStats == null) return;

        if (!hitTargets.Add(targetStats)) return;

        hasHit = true;
        targetStats.TakeDamage(damage);
        if (TryGetComponent<Collider2D>(out Collider2D projectileCollider))
            projectileCollider.enabled = false;

        if (hitEffect != null)
        {
            hitEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, hitEffect.main.duration);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
