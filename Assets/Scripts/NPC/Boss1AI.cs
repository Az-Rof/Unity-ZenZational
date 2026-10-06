using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.UI.Image;

[RequireComponent(typeof(charStats))]
public class Boss1AI : MonoBehaviour
{
    public enum Boss1State
    {
        Chase,
        WindupAttack,
        OffScreen,
        ThrowingBarrel,
        EndlagRecovery,
        Stunned
    }


    [Header("State & Targeting")]
    [SerializeField] private Boss1State currentState = Boss1State.Chase;
    [SerializeField] private Transform targetPlayer;

    [Header("Boss Stats")]
    [SerializeField] private float directLandingDamage = 75f;
    [SerializeField] private float barrelDamage = 20f;
    [SerializeField] private float shockwaveDamage = 10f;
    [SerializeField] private float screenRadius = 10f;
    [SerializeField] private float jumpEndlagDuration = 1.5f;
    [SerializeField] private ParticleSystem shockwave;
    [SerializeField, Min(1)] private int landingShockwaveProjectileCount = 8;

   [Header("Attack Settings")]
    [SerializeField] private float attackWindupDuration = 0.3f;
    [SerializeField] private float attackEndlagDuration = 0.6f;
    [SerializeField] private float attackHitRadius = 1.3f;
    [SerializeField] string[] attackSounds;
    [SerializeField] GameObject attackCirc;
    [SerializeField] GameObject barrel;
    [SerializeField] GameObject shockwavePrefab;



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
    ZombieAnimator ZA;

    bool walking = false;
    private bool landingShockwavesEnabled = true;
    float horizontal = 0f;
    float vertical = 0f;

    public Boss1State CurrentState => currentState;

    public void EnableLandingShockwaves(bool enabled)
    {
        landingShockwavesEnabled = enabled;
    }

    /// <summary>Starts the landing jump immediately for the final-boss reveal opening.</summary>
    public void TriggerOpeningAttack()
    {
        if (stats == null || stats.IsDead || targetPlayer == null)
            FindPlayerTarget();
        if (stats == null || stats.IsDead || targetPlayer == null)
        {
            Debug.LogWarning($"[{gameObject.name}] Cannot trigger opening attack because Boss1AI or player target is unavailable.", this);
            return;
        }

        StopAllCoroutines();
        currentState = Boss1State.OffScreen;
        StartCoroutine(PerformOffscreenJump());
    }

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
        ZA = GetComponent<ZombieAnimator>();
    }

    void Update()   
    {
        if (stats.IsDead) return;

        if (targetPlayer == null)
        {
            FindPlayerTarget();
            if (targetPlayer == null) return;
        }

        if (ZA != null)
        {
            if (!walking)
            {
                walking = true;
                ZA.PlayAnimation("Walking");
            }
        }
        horizontal = 0f;
        vertical = 0f;  
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal = -1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal = 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical = 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical = -1f;
        }

        // Get the direction vector from current position to target
        Vector2 directionl = (Vector2)targetPlayer.position - (Vector2)transform.position;

        // Calculate the angle in degrees
        float angle = Mathf.Atan2(directionl.y, directionl.x) * Mathf.Rad2Deg;

        // Apply rotation around the Z-axis (adjust angle offset if your sprite points UP instead of RIGHT)
        transform.rotation = Quaternion.AngleAxis(angle + 90, Vector3.forward);

        switch (currentState)
        {
            case Boss1State.Chase:
                HandleChaseState();
                break;
            case Boss1State.OffScreen:
                // Jump Attack
                StartCoroutine(PerformOffscreenJump());
                break;
            case Boss1State.ThrowingBarrel:
                break;
            case Boss1State.WindupAttack:
                // Actively winding up attack
                break;

            case Boss1State.EndlagRecovery:
                HandleEndlagState();
                break;

            case Boss1State.Stunned:
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
        if (distance >= screenRadius)
        {
            currentState = Boss1State.OffScreen;
            return;
        }

        // If within melee reach, enter WindupAttack state
        if (distance <= stats.attackRange)
        {
            StartCoroutine(PerformMeleeAttackRoutine());
            return;
        } else
        {
            if (distance >= screenRadius)
            {
                currentState = Boss1State.OffScreen;
                return;
            } else if (distance <= screenRadius && distance > stats.speed)
            {
                StartCoroutine(PerformBarrelThrow());
                return;
            }
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
        currentState = Boss1State.WindupAttack;
        ZA.StopAnimation();
        ZA.PlayAnimation("Attack");
        // Halt movement during windup
        if (rb2d != null) rb2d.linearVelocity = Vector2.zero;

        // Visual telegraph: flash reddish during windup
        UnityEngine.Color origColor = spriteRenderer != null ? spriteRenderer.color : UnityEngine.Color.white;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new UnityEngine.Color(1f, 0.4f, 0.4f, 1f);
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
        currentState = Boss1State.EndlagRecovery;
        walking = false;
        stateTimer = 0.5f;
    }

    private IEnumerator PerformBarrelThrow()
    {
        float projectedHP = cachedPlayerStats.currentHealth;
        currentState = Boss1State.WindupAttack;
        for (int i = 0; i < 10; i++)
        {
            ZA.StopAnimation();
            ZA.PlayAnimation("Attack");
            yield return new WaitForSeconds(attackWindupDuration);
            GameObject barrelInstance = Instantiate(barrel, transform.position, Quaternion.identity);
            ExplosiveBarrel br = barrelInstance.GetComponent<ExplosiveBarrel>();
        

           
            float ran = Random.Range(25f, 100f)/100f;
            br.landingPos = targetPlayer.position + new Vector3(horizontal*cachedPlayerStats.speed *ran, vertical*cachedPlayerStats.speed*ran, 0);
            Vector2 direction = ((Vector2)targetPlayer.position - (Vector2)transform.position).normalized;
            rb2d.linearVelocity = direction * stats.speed/2;
            yield return new WaitForSeconds(0.125f * Random.Range(2f,8f));
            ZA.PlayAnimation("Walking");
        }
        if (cachedPlayerStats.currentHealth < projectedHP)
        {
            Debug.Log($"{gameObject.name} landed barrel throw on Player!");
            currentState = Boss1State.Chase;
        }
        else
        {
            Debug.Log($"{gameObject.name} barrel throw missed (Player dodged).");
            currentState = Boss1State.OffScreen;
        }

    }

    private IEnumerator PerformOffscreenJump()
    {
        if (rb2d != null) rb2d.linearVelocity = Vector2.zero;
        currentState = Boss1State.WindupAttack;
        Vector2 landingPosition = targetPlayer.position;
        if (cachedPlayerStats != null)
        {
            landingPosition += new Vector2(horizontal * cachedPlayerStats.speed, vertical * cachedPlayerStats.speed);
        }

        GameObject landingTelegraph = attackCirc != null
            ? Instantiate(attackCirc, landingPosition, Quaternion.identity)
            : null;
        if (landingTelegraph != null) landingTelegraph.transform.localScale = new Vector2(15f, 15f);
        SpriteRenderer telegraphRenderer = landingTelegraph != null
            ? landingTelegraph.GetComponent<SpriteRenderer>()
            : null;

        float duration = Vector2.Distance(transform.position, landingPosition) / 15f;
        float elapsedTime = 0f;
        Vector2 DefaultSize = transform.localScale;
        Vector2 HeightSize = transform.localScale * 2;
        Vector2 startPos = transform.position;
        while (elapsedTime < duration)
        {
            if (elapsedTime < duration / 2)
            {
                transform.localScale = Vector2.Lerp(
                    DefaultSize,
                    HeightSize,
                    elapsedTime / (duration / 2)
                );
            }
            else
            {
                transform.localScale = Vector2.Lerp(
                    HeightSize,
                    DefaultSize,
                    (elapsedTime - duration / 2) / (duration / 2)
                );
            }
            if (telegraphRenderer != null)
            {
                UnityEngine.Color color = telegraphRenderer.color;
                color.a = Mathf.Lerp(0f, 0.5f, elapsedTime / duration);
                telegraphRenderer.color = color;
            }
            transform.position = Vector2.Lerp(startPos, landingPosition, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = landingPosition;
        if (landingTelegraph != null) Destroy(landingTelegraph);

        if (shockwave != null) shockwave.Play();
        if (attackSounds != null && attackSounds.Length > 2)
            AudioManager.Instance?.PlaySFX(attackSounds[2]);

        if (landingShockwavesEnabled)
            SpawnLandingShockwaves(landingPosition);

        float landingHitRadius = landingTelegraph != null ? landingTelegraph.transform.localScale.y / 2f : 7.5f;
        float distance = targetPlayer != null
            ? Vector2.Distance(landingPosition, targetPlayer.position)
            : float.MaxValue;
        if (distance < landingHitRadius)
        {
            Vector2 knockDir = ((Vector2)targetPlayer.position - landingPosition).normalized;
            if (cachedPlayerStats != null)
            {
                cachedPlayerStats.TakeDamage(directLandingDamage, knockDir, 4f);
            }
            Debug.Log($"{gameObject.name} landed jump strike on Player!");
            currentState = Boss1State.EndlagRecovery;
            stateTimer = jumpEndlagDuration;
        }
        else
        {
            Debug.Log($"{gameObject.name} jump attack missed (Player dodged).");
            currentState = Boss1State.EndlagRecovery;
            stateTimer = jumpEndlagDuration;
        }
    }

    private void SpawnLandingShockwaves(Vector2 landingPosition)
    {
        if (shockwavePrefab == null)
        {
            Debug.LogWarning($"[{gameObject.name}] shockwavePrefab is not assigned; landing projectiles were skipped.");
            return;
        }

        int projectileCount = Mathf.Max(1, landingShockwaveProjectileCount);
        float angleStep = 360f / projectileCount;
        HashSet<charStats> hitTargets = new HashSet<charStats>();

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = angleStep * i;
            Vector2 direction = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));

            GameObject projectile = Instantiate(
                shockwavePrefab,
                landingPosition,
                Quaternion.Euler(0f, 0f, angle));

            ShockProjectile shockProjectile = projectile.GetComponent<ShockProjectile>();
            if (shockProjectile != null)
            {
                shockProjectile.Initialize(direction, shockwaveDamage, hitTargets);
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] shockwavePrefab has no ShockProjectile component.");
            }
        }
    }

    private void HandleEndlagState()
    {

        // Physics owns the velocity while knocked back — do not zero it out.
        if (!stats.IsKnockedBack && rb2d != null) rb2d.linearVelocity = Vector2.zero;

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            // End of recovery window; return to Range Check & Chase
            currentState = Boss1State.Chase;
        }
    }

    public void ApplyStun(float duration)
    {
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        currentState = Boss1State.Stunned;
        if (!stats.IsKnockedBack && rb2d != null) rb2d.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(duration);

        if (currentState == Boss1State.Stunned)
        {
            currentState = Boss1State.Chase;
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
