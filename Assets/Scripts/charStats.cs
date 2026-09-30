using System;
using System.Collections;
using UnityEngine;

public class charStats : MonoBehaviour
{
    [Header("Character Info")]
    public string CharacterName;
    public string CharacterType;

    [Header("Health Stats")]
    public float currentHealth;
    public float maxHealth = 100f;

    [Header("Combat Stats")]
    public float attackPower = 10f;
    public float attackRange = 1.2f;
    public float attackCooldown = 0.8f;
    public float speed = 5f;
    public float lastAttackTime = 0f;

    [Header("Invulnerability & Feedback")]
    [SerializeField] private float invulnerabilityDuration = 0.4f;
    [SerializeField] private bool useInvulnerabilityOnDamage = false;

    private bool isInvulnerable = false;
    private bool isDead = false;
    private bool isKnockedBack = false;
    private float knockbackTimer = 0f;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb2d;

    // --- Chain knockback ("bowling") state ---
    // While flying from a knockback, this character carries momentum that can
    // be transferred to another character it collides with (domino effect).
    private Vector2 currentKnockVelocity = Vector2.zero;
    private charStats chainSource = null; // who knocked us (prevents A->B->A loops)
    private int chainDepth = 0;           // how many hops this impulse already made

    [Header("Knockback Settings")]
    [Tooltip("Duration in seconds that this character's AI movement is suspended after being knocked back")]
    [SerializeField] private float knockbackRecoveryTime = 0.25f;
    [Tooltip("Upper cap for knockback impulse velocity (units/sec). Prevents extreme launches.")]
    [SerializeField] private float maxKnockbackForce = 50f;

    [Header("Chain Knockback (Bowling Effect)")]
    [Tooltip("Fraction of a flying character's momentum transferred to the victim it rams (0-1).")]
    [Range(0f, 1f)]
    [SerializeField] private float chainKnockbackTransfer = 0.75f;
    [Tooltip("Flat force subtracted per hop. Eats weak leftover pushes so chains die out naturally.")]
    [SerializeField] private float chainKnockbackFlatReduction = 2f;
    [Tooltip("Maximum number of enemies a single knockback can plow through in a chain.")]
    [SerializeField] private int maxChainDepth = 3;

    // Events
    public event Action<float, float> OnHealthChanged;
    public event Action<GameObject> OnCharacterDied;

    public bool IsDead => isDead;
    public bool IsInvulnerable => isInvulnerable;
    /// <summary>
    /// True while this character is actively being knocked back. AI should NOT override velocity during this window.
    /// </summary>
    public bool IsKnockedBack => isKnockedBack;

    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rb2d = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        InitiateCharacterStats();
    }

    void Update()
    {
        // Tick knockback recovery timer so AI/movement scripts know when
        // they are allowed to take over velocity control again.
        if (isKnockedBack)
        {
            // Keep the stored momentum in sync with the real physics velocity
            // (it decays via rigidbody damping) so chain transfers reflect
            // actual momentum instead of the initial launch impulse.
            if (rb2d != null) currentKnockVelocity = rb2d.linearVelocity;

            knockbackTimer -= Time.deltaTime;
            if (knockbackTimer <= 0f)
            {
                isKnockedBack = false;
                currentKnockVelocity = Vector2.zero;
                chainSource = null;
            }
        }
    }

    void InitiateCharacterStats()
    {
        if (string.IsNullOrEmpty(CharacterName))
        {
            CharacterName = gameObject.name;
        }

        if (string.IsNullOrEmpty(CharacterType))
        {
            CharacterType = gameObject.tag;
        }

        // Default: Enable invulnerability frames on damage for the player
        if (CompareTag("Player") || gameObject.name.Contains("Player"))
        {
            useInvulnerabilityOnDamage = true;
        }

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage, Vector2 knockbackDir = default, float knockbackForce = 0f)
    {
        if (isDead) return;
        if (isInvulnerable) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        Debug.Log($"{gameObject.name} took {damage} damage! Remaining HP: {currentHealth}");

        // Physical knockback impulse if force and direction are provided
        if (knockbackForce > 0f && knockbackDir != Vector2.zero)
        {
            // A fresh impact starts a brand new knockback chain state
            chainSource = null;
            chainDepth = 0;
            ApplyKnockback(knockbackDir, knockbackForce);
        }

        // Visual flash & invulnerability frames
        if (useInvulnerabilityOnDamage && currentHealth > 0)
        {
            StartCoroutine(InvulnerabilityRoutine());
        }
        else if (currentHealth > 0 && spriteRenderer != null)
        {
            StartCoroutine(FlashDamageRoutine());
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (rb2d == null) return;

        // Refresh the knockback state window so AI/player movement scripts
        // suspend their per-frame velocity override until recovery finishes.
        isKnockedBack = true;
        knockbackTimer = knockbackRecoveryTime;

        // Directly set velocity instead of AddForce to guarantee the impulse is visible
        // even if AI was previously driving velocity. Clamp to prevent extreme launches.
        Vector2 knockVelocity = direction.normalized * Mathf.Min(force, maxKnockbackForce);
        rb2d.linearVelocity = knockVelocity;
        currentKnockVelocity = knockVelocity;
    }

    /// <summary>
    /// Chain knockback ("bowling" effect): a character flying from knockback
    /// transfers part of its momentum to the victim it collides with.
    /// The impulse decays each hop so chains naturally die out.
    /// </summary>
    public void TryChainKnockbackTo(charStats victim)
    {
        // Not flying, already recovered, or this hop carries no momentum
        if (!isKnockedBack || currentKnockVelocity.sqrMagnitude < 0.01f) return;

        // Don't knock back the character that started our own knockback (A <-> B ping-pong)
        if (chainSource == victim) return;

        // Chain length cap: stop the domino effect after a limited number of hops
        if (chainDepth >= maxChainDepth) return;

        // Sanity: victim must be alive and able to receive the impulse
        if (victim == null || victim.IsDead) return;

        // Momentum transfer: the victim receives a fraction of our flying velocity.
        // Decay prevents infinite chains; flat reduction eats weak leftover pushes.
        float transferredForce = currentKnockVelocity.magnitude * chainKnockbackTransfer
                               - chainKnockbackFlatReduction;
        if (transferredForce <= 0f) return;

        Vector2 dirToVictim = ((Vector2)victim.transform.position - (Vector2)transform.position).normalized;

        // Use our flight direction, but blended toward the actual direction to the
        // victim so a grazing hit still pushes them away from us, not through us.
        Vector2 pushDir = (currentKnockVelocity.normalized + dirToVictim).normalized;
        if (pushDir.sqrMagnitude < 0.01f) pushDir = dirToVictim;

        // Store who initiated this victim's knockback so they can't bounce it right back
        victim.chainSource = this;
        victim.chainDepth = chainDepth + 1;
        victim.ApplyKnockback(pushDir, transferredForce);

        // Once momentum is spent, this character stops being a projectile
        currentKnockVelocity = Vector2.zero;
        isKnockedBack = false;
        knockbackTimer = 0f;
    }

    /// <summary>
    /// True while this character is actively flying from a knockback and can
    /// still transfer momentum to characters it collides with (bowling effect).
    /// </summary>
    public bool IsFlyingFromKnockback => isKnockedBack && currentKnockVelocity.sqrMagnitude > 0.01f;

    private IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;
        float elapsed = 0f;
        float blinkRate = 0.08f;

        while (elapsed < invulnerabilityDuration)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = !spriteRenderer.enabled;
            }
            yield return new WaitForSeconds(blinkRate);
            elapsed += blinkRate;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }
        isInvulnerable = false;
    }

    private IEnumerator FlashDamageRoutine()
    {
        if (spriteRenderer == null) yield break;
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = new Color(1f, 0.3f, 0.3f, 1f);
        yield return new WaitForSeconds(0.08f);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"{gameObject.name} has died.");
        OnCharacterDied?.Invoke(gameObject);

        Destroy(gameObject);
    }
}
