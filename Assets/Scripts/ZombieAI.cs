using System.Collections;
using UnityEngine;

[RequireComponent(typeof(charStats))]
public class ZombieAI : MonoBehaviour
{
    public enum ZombieState
    {
        Chase,
        WindupAttack,
        EndlagRecovery,
        Stunned
    }

    [Header("State & Targeting")]
    [SerializeField] private ZombieState currentState = ZombieState.Chase;
    [SerializeField] private Transform targetPlayer;

    [Header("Attack Settings")]
    [SerializeField] private float attackWindupDuration = 0.3f;
    [SerializeField] private float attackEndlagDuration = 0.6f;
    [SerializeField] private float attackHitRadius = 1.3f;
    [SerializeField] string[] attackSounds;

    [Header("Drops Configuration")]
    [SerializeField] private GameObject weaponPickupPrefab;
    [SerializeField] private GameObject healthPickupPrefab;
    [SerializeField] private float defaultHealthDropChance = 0.15f;

    private charStats stats;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb2d;
    private playerController cachedPlayerCtrl;
    private charStats cachedPlayerStats;
    private float stateTimer = 0f;

    public ZombieState CurrentState => currentState;

    void Awake()
    {
        stats = GetComponent<charStats>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rb2d = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        FindPlayerTarget();
        stats.OnCharacterDied += HandleDeath;
    }

    void Update()
    {
        if (stats.IsDead) return;

        if (targetPlayer == null)
        {
            FindPlayerTarget();
            if (targetPlayer == null) return;
        }

        switch (currentState)
        {
            case ZombieState.Chase:
                HandleChaseState();
                break;

            case ZombieState.WindupAttack:
                // Actively winding up attack
                break;

            case ZombieState.EndlagRecovery:
                HandleEndlagState();
                break;

            case ZombieState.Stunned:
                // Awaiting stun expiry
                break;
        }
    }

    private void FindPlayerTarget()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            // Fallback: search for playerController if tag was not assigned
            playerController pc = FindAnyObjectByType<playerController>();
            if (pc != null) playerObj = pc.gameObject;
        }

        if (playerObj != null)
        {
            targetPlayer = playerObj.transform;
            cachedPlayerCtrl = playerObj.GetComponent<playerController>();
            cachedPlayerStats = playerObj.GetComponent<charStats>();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Chain knockback ("bowling"): while flying from a knockback, ram into
        // another enemy and transfer part of the momentum to them.
        if (collision.collider.CompareTag("Enemy"))
        {
            stats.TryChainKnockbackTo(collision.collider.GetComponentInParent<charStats>());
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Same chain transfer for trigger-based colliders (zombies may use triggers)
        if (other.CompareTag("Enemy"))
        {
            stats.TryChainKnockbackTo(other.GetComponentInParent<charStats>());
        }
    }

    private void HandleChaseState()
    {
        // While being knocked back, physics owns the velocity — do not override it,
        // otherwise the knockback impulse gets cancelled on the very next frame.
        if (stats.IsKnockedBack)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = ((Vector2)targetPlayer.position - (Vector2)transform.position).x < 0;
            }
            return;
        }

        float distance = Vector2.Distance(transform.position, targetPlayer.position);

        // If within melee reach, enter WindupAttack state
        if (distance <= stats.attackRange)
        {
            StartCoroutine(PerformMeleeAttackRoutine());
            return;
        }

        // Navigate directly towards player coordinates
        Vector2 direction = ((Vector2)targetPlayer.position - (Vector2)transform.position).normalized;

        if (rb2d != null)
        {
            rb2d.linearVelocity = direction * stats.speed;
        }
        else
        {
            transform.position += (Vector3)(direction * stats.speed * Time.deltaTime);
        }

        // Face player direction
        if (spriteRenderer != null && direction.x != 0)
        {
            spriteRenderer.flipX = direction.x < 0;
        }
    }

    private IEnumerator PerformMeleeAttackRoutine()
    {
        currentState = ZombieState.WindupAttack;

        // Halt movement during windup
        if (rb2d != null) rb2d.linearVelocity = Vector2.zero;

        // Visual telegraph: flash reddish during windup
        Color origColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 0.4f, 0.4f, 1f);
        }
        AudioManager.Instance.PlaySFX(attackSounds[0]);
        yield return new WaitForSeconds(attackWindupDuration);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = origColor;
        }

        if (stats.IsDead) yield break;

        // Hit Check: evaluate if player is still within attack radius
        if (targetPlayer != null)
        {
            float hitDistance = Vector2.Distance(transform.position, targetPlayer.position);
            if (hitDistance <= attackHitRadius)
            {
                // Attack Landed: player takes damage
                Vector2 knockDir = ((Vector2)targetPlayer.position - (Vector2)transform.position).normalized;
                if (cachedPlayerStats != null)
                {
                    cachedPlayerStats.TakeDamage(stats.attackPower, knockDir, 4f);
                    AudioManager.Instance.PlaySFX(attackSounds[1]);
                }
                Debug.Log($"{gameObject.name} landed melee strike on Player!");
            }
            else
            {
                // Attack Missed: player evaded during windup
                Debug.Log($"{gameObject.name} attack missed (Player dodged).");
            }
        }

        // Transition to Endlag / Recovery state
        currentState = ZombieState.EndlagRecovery;
        stateTimer = attackEndlagDuration;
    }

    private void HandleEndlagState()
    {
        // Physics owns the velocity while knocked back — do not zero it out.
        if (!stats.IsKnockedBack && rb2d != null) rb2d.linearVelocity = Vector2.zero;

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            // End of recovery window; return to Range Check & Chase
            currentState = ZombieState.Chase;
        }
    }

    public void ApplyStun(float duration)
    {
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        currentState = ZombieState.Stunned;
        if (!stats.IsKnockedBack && rb2d != null) rb2d.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(duration);

        if (currentState == ZombieState.Stunned)
        {
            currentState = ZombieState.Chase;
        }
    }

    private void HandleDeath(GameObject victim)
    {
        stats.OnCharacterDied -= HandleDeath;
        // SpawnLootDrop();
    }

    // private void SpawnLootDrop()
    // {
    //     bool isScavengeActive = false;

    //     if (cachedPlayerCtrl != null)
    //     {
    //         isScavengeActive = cachedPlayerCtrl.IsInScavengeMode;
    //     }

    //     Vector3 dropPos = transform.position;

    //     // GDD Rule: When player ammo is depleted (Scavenge Mode), guarantee a random weapon drop
    //     if (isScavengeActive)
    //     {
    //         SpawnWeaponPickup(dropPos);
    //     }
    //     else
    //     {
    //         // Random chance to drop a health pack if the player is injured
    //         if (cachedPlayerStats != null && cachedPlayerStats.currentHealth < cachedPlayerStats.maxHealth)
    //         {
    //             if (Random.value < defaultHealthDropChance)
    //             {
    //                 SpawnHealthPickup(dropPos);
    //             }
    //         }
    //     }
    // }

    private void SpawnWeaponPickup(Vector3 position)
    {
        if (weaponPickupPrefab != null)
        {
            Instantiate(weaponPickupPrefab, position, Quaternion.identity);
        }
        else
        {
            // ItemPickup not implemented yet — skip the weapon drop without errors
            Debug.LogWarning($"[{gameObject.name}] weaponPickupPrefab is not assigned & ItemPickup does not exist — weapon drop skipped.");
        }
    }

    // private void SpawnHealthPickup(Vector3 position)
    // {
    //     if (healthPickupPrefab != null)
    //     {
    //         Instantiate(healthPickupPrefab, position, Quaternion.identity);
    //     }
    //     else
    //     {
    //         // ItemPickup not implemented yet — skip the health drop without errors
    //         Debug.LogWarning($"[{gameObject.name}] healthPickupPrefab is not assigned & ItemPickup does not exist — health drop skipped.");
    //     }
    // }

    // private WeaponType GetRandomScavengedWeaponType()
    // {
    //     // Pick random upgraded weapon from pool (excluding starter pistol)
    //     WeaponType[] pool = new WeaponType[] { WeaponType.Shotgun, WeaponType.RapidSMG, WeaponType.Rifle };
    //     int idx = Random.Range(0, pool.Length);
    //     return pool[idx];
    // }
}
