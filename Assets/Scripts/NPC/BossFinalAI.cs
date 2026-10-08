using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(charStats))]
public class BossFinalAI : MonoBehaviour
{
    public enum FinalBossState
    {
        Chase,
        Windup,
        DashAway,
        JumpAttack,
        LungeAttack,
        StompShockwave,
        BarrelBarrage,
        QuickDodge,
        GlitchSwap,
        EndlagRecovery,
        Stunned,
        Dead
    }

    [Header("State & Target")]
    [SerializeField] private FinalBossState currentState = FinalBossState.Chase;
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private ZombieAnimator legs;

    [Header("Ranges & Timing")]
    [Tooltip("Within this distance, the boss chooses between a jump attack and a stomp shockwave.")]
    [SerializeField, Min(0.1f)] private float nearbyAttackDistance = 4f;
    [Tooltip("The boss chases instead of lunging when the player is farther away than this.")]
    [SerializeField, Min(0.1f)] private float maximumLungeDistance = 10f;
    [SerializeField, Min(0.1f)] private float attackInterval = 1.25f;
    [SerializeField, Min(0f)] private float attackWindup = 0.35f;
    [SerializeField, Min(0f)] private float attackEndlag = 0.7f;
    [SerializeField, Min(0f)] private float missEndlag = 0.55f;

    [Header("Lunge Melee")]
    [SerializeField, Min(0.1f)] private float lungeSpeed = 18f;
    [SerializeField, Min(0.1f)] private float lungeHitRadius = 1.2f;
    [SerializeField, Min(0f)] private float lungeDamage = 35f;
    [SerializeField, Min(0f)] private float lungeKnockback = 5f;

    [Header("Jump Attack")]
    [SerializeField, Min(0f)] private float dashAwayDistance = 3f;
    [SerializeField, Min(0.1f)] private float dashAwaySpeed = 14f;
    [SerializeField, Min(0.1f)] private float jumpSpeed = 16f;
    [SerializeField, Min(0f)] private float jumpLeadTime = 0.2f;
    [SerializeField, Min(0.1f)] private float jumpHitRadius = 1.8f;
    [SerializeField, Min(0f)] private float jumpDamage = 45f;
    [SerializeField, Min(0f)] private float jumpKnockback = 7f;
    [SerializeField, Min(1f)] private float jumpScaleMultiplier = 1.7f;
    [SerializeField] private GameObject landingTelegraphPrefab;
    [SerializeField, Min(0.1f)] private float landingTelegraphScale = 3f;

    [Header("Shockwave")]
    [SerializeField] private GameObject shockwavePrefab;
    [SerializeField] private ParticleSystem stompEffect;
    [SerializeField, Min(1)] private int shockwaveProjectileCount = 10;
    [SerializeField, Min(0f)] private float stompDamage = 10f;
    [SerializeField, Min(0f)] private float stompWindup = 0.45f;

    [Header("Arena Barrel Barrage")]
    [SerializeField] private GameObject barrelPrefab;
    [SerializeField, Range(0f, 1f)] private float barrelAttackChance = 0.12f;
    [SerializeField, Min(1)] private int barrelsPerBarrage = 3;
    [SerializeField, Min(0f)] private float barrelDamage = 20f;
    [SerializeField, Min(0f)] private float barrelLandingSpread = 3f;
    [SerializeField, Min(0f)] private float barrelSpawnInterval = 0.45f;
    [SerializeField, Min(0.1f)] private float offscreenSpawnDistance = 12f;

    [Header("Cursor Dodge")]
    [Tooltip("If the mouse cursor is this close to the boss, it dodges before choosing a normal attack.")]
    [SerializeField, Min(0f)] private float cursorReactionRadius = 1.5f;
    [SerializeField, Min(0f)] private float cursorDodgeCooldown = 2f;
    [SerializeField, Min(0.1f)] private float quickDodgeDistance = 2.5f;
    [SerializeField, Min(0.1f)] private float quickDodgeSpeed = 16f;

    [Header("Optional References")]
    [SerializeField] private GlitchingManager glitchingManager;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private ZombieAnimator zombieAnimator;
    [SerializeField] private string walkAnimation = "Walking";
    [SerializeField] private string attackAnimation = "Attack";
    [SerializeField] private string attackAnimation2 = "Attack2";

    [Tooltip("Rotate the boss toward the player. Use a 90 degree offset for sprites whose default facing direction is up.")]
    [SerializeField] private bool rotateTowardsPlayer = true;
    [SerializeField] private float facingRotationOffset = 90f;
    [Tooltip("Used only when Rotate Towards Player is disabled; suitable for sprites that only need left/right mirroring.")]
    [SerializeField] private bool flipSpriteTowardPlayer = true;

    private charStats stats;
    private charStats cachedPlayerStats;
    private Rigidbody2D rb2d;
    private Rigidbody2D playerRigidbody;
    private Vector3 baseScale;
    private bool actionRunning;
    private bool lastLungeHit;
    private float nextAttackTime;
    private float nextCursorDodgeTime;

    public FinalBossState CurrentState => currentState;

    private void Awake()
    {
        stats = GetComponent<charStats>();
        rb2d = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (zombieAnimator == null) zombieAnimator = GetComponent<ZombieAnimator>();
        if (glitchingManager == null) glitchingManager = FindAnyObjectByType<GlitchingManager>();
        baseScale = transform.localScale;
    }

    private void Start()
    {
        FindPlayerTarget();
        stats.OnCharacterDied += HandleDeath;
        if (stats.IsDead) HandleDeath(gameObject);
        legs.PlayAnimation("LegMove");
    }

    private void Update()
    {
        if (currentState == FinalBossState.Dead || stats.IsDead) return;

        if (targetPlayer == null)
        {
            FindPlayerTarget();
            if (targetPlayer == null) return;
        }

        FaceTarget();

        if (currentState != FinalBossState.Chase || actionRunning || stats.IsKnockedBack)
            return;

        float distance = Vector2.Distance(transform.position, targetPlayer.position);
        if (distance > maximumLungeDistance)
        {
            ChasePlayer();
            return;
        }

        StopMovement();
        if (Time.time >= nextAttackTime)
        {
            actionRunning = true;
            StartCoroutine(AttackCycle(distance));
        }
    }

    private void FindPlayerTarget()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            playerController player = FindAnyObjectByType<playerController>();
            if (player != null) playerObject = player.gameObject;
        }

        if (playerObject == null) return;

        targetPlayer = playerObject.transform;
        cachedPlayerStats = playerObject.GetComponent<charStats>();
        playerRigidbody = playerObject.GetComponent<Rigidbody2D>();
    }

    private void ChasePlayer()
    {
        if (stats.IsKnockedBack) return;

        Vector2 direction = ((Vector2)targetPlayer.position - (Vector2)transform.position).normalized;
        if (rb2d != null)
            rb2d.linearVelocity = direction * stats.speed;
        else
            transform.position += (Vector3)(direction * stats.speed * Time.deltaTime);

        PlayAnimation(walkAnimation);
    }

    private IEnumerator AttackCycle(float distanceAtDecision)
    {
        if (IsCursorNearBoss() && Time.time >= nextCursorDodgeTime)
        {
            nextCursorDodgeTime = Time.time + cursorDodgeCooldown;
            yield return PerformQuickDodge();
        }
        else if (barrelPrefab != null && Random.value < barrelAttackChance)
        {
            yield return PerformBarrelBarrage();
        }
        else if (distanceAtDecision <= nearbyAttackDistance)
        {
            // Close range: either disengage and jump onto the player, or stomp in place.
            if (Random.Range(0, 2) == 0)
                yield return PerformDashAwayJump();
            else
                yield return PerformStompShockwave();
        }
        else
        {
            yield return PerformLunge();
            if (lastLungeHit)
            {
                yield return Endlag(attackEndlag);
            }
            else
            {
                // After a missed lunge and its recovery, choose a positional trick or a wave.
                yield return Endlag(missEndlag);
                if (Random.Range(0, 2) == 0)
                    yield return PerformGlitchSwapAndRetreat();
                else
                    yield return PerformStompShockwave();
            }
        }

        if (currentState != FinalBossState.Dead && currentState != FinalBossState.Stunned)
        {
            currentState = FinalBossState.Chase;
            nextAttackTime = Time.time + Mathf.Max(attackInterval, stats.attackCooldown);
        }
        actionRunning = false;
    }

    private IEnumerator PerformLunge()
    {
        currentState = FinalBossState.Windup;
        StopMovement();
        PlayAnimation(attackAnimation2);
        yield return new WaitForSeconds(attackWindup);
        if (stats.IsDead || targetPlayer == null) yield break;

        currentState = FinalBossState.LungeAttack;
        Vector2 start = transform.position;
        Vector2 towardPlayer = ((Vector2)targetPlayer.position - start).normalized;
        if (towardPlayer == Vector2.zero) towardPlayer = Vector2.right;
        Vector2 end = (Vector2)targetPlayer.position;
        float duration = Mathf.Max(0.08f, Vector2.Distance(start, end) / Mathf.Max(0.1f, lungeSpeed));
        float elapsed = 0f;
        lastLungeHit = false;

        while (elapsed < duration && !stats.IsDead)
        {
            Vector2 previous = transform.position;
            elapsed += Time.deltaTime;
            Vector2 next = Vector2.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
            SetBossPosition(next);

            if (!lastLungeHit && targetPlayer != null && cachedPlayerStats != null)
            {
                Vector2 playerPosition = targetPlayer.position;
                if (DistanceToSegment(playerPosition, previous, next) <= lungeHitRadius)
                {
                    ApplyPlayerDamage(lungeDamage, lungeKnockback, towardPlayer);
                    lastLungeHit = true;
                }
            }
            yield return null;
        }

        SetBossPosition(end);
        StopMovement();
    }

    private IEnumerator PerformDashAwayJump()
    {
        currentState = FinalBossState.DashAway;
        StopMovement();
        Vector2 away = (Vector2)transform.position - (Vector2)targetPlayer.position;
        if (away.sqrMagnitude < 0.001f) away = Random.insideUnitCircle;
        away.Normalize();
        Vector2 dashEnd = (Vector2)transform.position + away * dashAwayDistance;
        yield return MoveBossTo(dashEnd, dashAwaySpeed);
        if (stats.IsDead || targetPlayer == null) yield break;

        currentState = FinalBossState.Windup;
        PlayAnimation(attackAnimation);
        yield return new WaitForSeconds(attackWindup * 0.5f);
        if (stats.IsDead || targetPlayer == null) yield break;

        Vector2 landingPosition = GetPredictedPlayerPosition();
        GameObject telegraph = CreateLandingTelegraph(landingPosition);
        currentState = FinalBossState.JumpAttack;
        Vector2 jumpStart = transform.position;
        float duration = Mathf.Max(0.12f, Vector2.Distance(jumpStart, landingPosition) / Mathf.Max(0.1f, jumpSpeed));
        float elapsed = 0f;

        while (elapsed < duration && !stats.IsDead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetBossPosition(Vector2.Lerp(jumpStart, landingPosition, t));
            float jumpHeight = Mathf.Sin(t * Mathf.PI);
            transform.localScale = baseScale * Mathf.Lerp(1f, jumpScaleMultiplier, jumpHeight);
            yield return null;
        }

        SetBossPosition(landingPosition);
        transform.localScale = baseScale;
        if (telegraph != null) Destroy(telegraph);

        if (targetPlayer != null && cachedPlayerStats != null &&
            Vector2.Distance(landingPosition, targetPlayer.position) <= jumpHitRadius)
        {
            Vector2 knockDirection = ((Vector2)targetPlayer.position - landingPosition).normalized;
            ApplyPlayerDamage(jumpDamage, jumpKnockback, knockDirection);
        }

        yield return Endlag(attackEndlag);
    }

    private IEnumerator PerformStompShockwave()
    {
        currentState = FinalBossState.Windup;
        StopMovement();
        PlayAnimation(attackAnimation);
        yield return new WaitForSeconds(stompWindup);
        if (stats.IsDead) yield break;

        currentState = FinalBossState.StompShockwave;
        Vector3 originalScale = transform.localScale;
        transform.localScale = originalScale * 0.78f;
        yield return new WaitForSeconds(0.08f);
        transform.localScale = originalScale;
        if (stompEffect != null) stompEffect.Play();
        SpawnShockwaves(transform.position);

        // The projectile wave is the main hit. This small stomp radius rewards
        // players who remain directly under the boss at impact.
        if (targetPlayer != null && cachedPlayerStats != null &&
            Vector2.Distance(transform.position, targetPlayer.position) <= jumpHitRadius * 0.6f)
        {
            Vector2 knockDirection = ((Vector2)targetPlayer.position - (Vector2)transform.position).normalized;
            ApplyPlayerDamage(stompDamage, jumpKnockback * 0.5f, knockDirection);
        }

        yield return Endlag(attackEndlag);
    }

    private IEnumerator PerformGlitchSwapAndRetreat()
    {
        if (targetPlayer == null)
        {
            yield return PerformStompShockwave();
            yield break;
        }

        currentState = FinalBossState.GlitchSwap;
        StopMovement();
        glitchingManager?.PlayGlitchSilently(2, 90, 0.45f);
        AudioManager.Instance.PlaySFX("Glitch2");

        Vector2 bossPosition = transform.position;
        Vector2 playerPosition = targetPlayer.position;
        SetBossPosition(playerPosition);
        SetPlayerPosition(bossPosition);
        yield return new WaitForSeconds(0.12f);

        Vector2 retreatDirection = ((Vector2)transform.position - (Vector2)targetPlayer.position).normalized;
        if (retreatDirection.sqrMagnitude < 0.001f) retreatDirection = Random.insideUnitCircle.normalized;
        currentState = FinalBossState.DashAway;
        yield return MoveBossTo((Vector2)transform.position + retreatDirection * dashAwayDistance, dashAwaySpeed);
        yield return Endlag(attackEndlag);
    }

    private IEnumerator PerformQuickDodge()
    {
        currentState = FinalBossState.QuickDodge;
        StopMovement();

        Vector2 cursorWorld = GetCursorWorldPosition();
        Vector2 direction = (Vector2)transform.position - cursorWorld;
        if (direction.sqrMagnitude < 0.001f && targetPlayer != null)
            direction = (Vector2)transform.position - (Vector2)targetPlayer.position;
        if (direction.sqrMagnitude < 0.001f) direction = Random.insideUnitCircle;
        direction.Normalize();

        yield return MoveBossTo((Vector2)transform.position + direction * quickDodgeDistance, quickDodgeSpeed);
        yield return Endlag(Mathf.Min(0.25f, attackEndlag));
    }

    private IEnumerator PerformBarrelBarrage()
    {
        currentState = FinalBossState.BarrelBarrage;
        StopMovement();
        PlayAnimation(attackAnimation);

        int count = Mathf.Max(1, barrelsPerBarrage);
        for (int i = 0; i < count; i++)
        {
            if (stats.IsDead || targetPlayer == null) yield break;

            Vector2 playerPosition = targetPlayer.position;
            GameObject barrelObject = Instantiate(barrelPrefab, GetOffscreenSpawnPosition(playerPosition), Quaternion.identity);
            ExplosiveBarrel barrel = barrelObject.GetComponent<ExplosiveBarrel>();
            if (barrel != null)
            {
                barrel.Initialize(targetPlayer, barrelDamage, barrelLandingSpread);
            }
            else
            {
                Debug.LogWarning($"[{name}] Barrel prefab needs an ExplosiveBarrel component.", barrelObject);
            }

            if (i < count - 1) yield return new WaitForSeconds(barrelSpawnInterval);
        }

        yield return Endlag(attackEndlag);
    }

    private IEnumerator MoveBossTo(Vector2 destination, float speed)
    {
        Vector2 start = transform.position;
        float distance = Vector2.Distance(start, destination);
        float duration = distance / Mathf.Max(0.1f, speed);
        if (duration <= 0f)
        {
            SetBossPosition(destination);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration && !stats.IsDead)
        {
            elapsed += Time.deltaTime;
            SetBossPosition(Vector2.Lerp(start, destination, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        if (!stats.IsDead) SetBossPosition(destination);
        StopMovement();
    }

    private IEnumerator Endlag(float duration)
    {
        currentState = FinalBossState.EndlagRecovery;
        StopMovement();
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
    }

    private Vector2 GetPredictedPlayerPosition()
    {
        Vector2 predictedPosition = targetPlayer.position;
        if (playerRigidbody != null && jumpLeadTime > 0f)
        {
            Vector2 lead = playerRigidbody.linearVelocity * jumpLeadTime;
            lead = Vector2.ClampMagnitude(lead, 2f);
            predictedPosition += lead;
        }
        return predictedPosition;
    }

    private GameObject CreateLandingTelegraph(Vector2 position)
    {
        if (landingTelegraphPrefab == null) return null;
        GameObject telegraph = Instantiate(landingTelegraphPrefab, position, Quaternion.identity);
        telegraph.transform.localScale = Vector3.one * landingTelegraphScale;
        return telegraph;
    }

    private void SpawnShockwaves(Vector2 origin)
    {
        if (shockwavePrefab == null)
        {
            Debug.LogWarning($"[{name}] Assign a ShockProjectile prefab to enable the stomp shockwave.", this);
            return;
        }

        int projectileCount = Mathf.Max(1, shockwaveProjectileCount);
        float angleStep = 360f / projectileCount;
        HashSet<charStats> sharedHitTargets = new HashSet<charStats>();

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = angleStep * i;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            GameObject projectileObject = Instantiate(
                shockwavePrefab,
                origin,
                Quaternion.Euler(0f, 0f, angle));

            ShockProjectile projectile = projectileObject.GetComponent<ShockProjectile>();
            if (projectile != null)
                projectile.Initialize(direction, stompDamage, sharedHitTargets);
            else
                Debug.LogWarning($"[{name}] Shockwave prefab needs a ShockProjectile component.", projectileObject);
        }
    }

    private Vector2 GetOffscreenSpawnPosition(Vector2 landingPosition)
    {
        Camera sceneCamera = Camera.main;
        if (sceneCamera != null)
        {
            Vector3 viewport = sceneCamera.WorldToViewportPoint(landingPosition);
            float side = Random.Range(0, 4);
            if (side < 1f) viewport.x = -0.12f;
            else if (side < 2f) viewport.x = 1.12f;
            else if (side < 3f) viewport.y = -0.12f;
            else viewport.y = 1.12f;

            Vector3 spawn = sceneCamera.ViewportToWorldPoint(viewport);
            spawn.z = 0f;
            return spawn;
        }

        Vector2 direction = Random.insideUnitCircle.normalized;
        return landingPosition + direction * offscreenSpawnDistance;
    }

    private bool IsCursorNearBoss()
    {
        if (Mouse.current == null || Camera.main == null || cursorReactionRadius <= 0f)
            return false;
        return Vector2.Distance(transform.position, GetCursorWorldPosition()) <= cursorReactionRadius;
    }

    private Vector2 GetCursorWorldPosition()
    {
        if (Mouse.current == null || Camera.main == null) return transform.position;
        Vector2 cursor = Mouse.current.position.ReadValue();
        float cameraDepth = Mathf.Abs(transform.position.z - Camera.main.transform.position.z);
        Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(cursor.x, cursor.y, cameraDepth));
        return world;
    }

    private void ApplyPlayerDamage(float damage, float knockback, Vector2 direction)
    {
        if (cachedPlayerStats == null || cachedPlayerStats.IsDead) return;
        if (direction.sqrMagnitude < 0.001f) direction = Vector2.right;
        cachedPlayerStats.TakeDamage(damage, direction.normalized, knockback);
    }

    private void FaceTarget()
    {
        if (targetPlayer == null) return;

        Vector2 direction = (Vector2)targetPlayer.position - (Vector2)transform.position;
        if (direction.sqrMagnitude < 0.0001f) return;

        if (rotateTowardsPlayer)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle + facingRotationOffset, Vector3.forward);
        }
       
    }

    private void PlayAnimation(string animationName)
    {
        if (zombieAnimator == null || string.IsNullOrEmpty(animationName)) return;
        if (animationName == attackAnimation)
        {
            spriteRenderer.flipX = Random.Range(0, 2) == 1;
        }
        zombieAnimator.PlayAnimation(animationName);
    }

    private void SetBossPosition(Vector2 position)
    {
        if (rb2d != null) rb2d.position = position;
        else transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    private void SetPlayerPosition(Vector2 position)
    {
        if (targetPlayer == null) return;
        if (playerRigidbody != null)
        {
            playerRigidbody.position = position;
            playerRigidbody.linearVelocity = Vector2.zero;
        }
        else
        {
            targetPlayer.position = new Vector3(position.x, position.y, targetPlayer.position.z);
        }
    }

    private void StopMovement()
    {
        if (rb2d != null) rb2d.linearVelocity = Vector2.zero;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        Vector2 segment = segmentEnd - segmentStart;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= Mathf.Epsilon) return Vector2.Distance(point, segmentStart);
        float t = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) / lengthSquared);
        return Vector2.Distance(point, segmentStart + segment * t);
    }

    /// <summary>Can be called by another gameplay system to interrupt the boss with a stun.</summary>
    public void ApplyStun(float duration)
    {
        if (stats == null || stats.IsDead || currentState == FinalBossState.Dead) return;
        StopAllCoroutines();
        actionRunning = false;
        transform.localScale = baseScale;
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        currentState = FinalBossState.Stunned;
        StopMovement();
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        if (!stats.IsDead)
        {
            currentState = FinalBossState.Chase;
            nextAttackTime = Time.time + 0.25f;
        }
    }

    private void HandleDeath(GameObject victim)
    {
        if (stats != null) stats.OnCharacterDied -= HandleDeath;
        StopAllCoroutines();
        actionRunning = false;
        currentState = FinalBossState.Dead;
        transform.localScale = baseScale;
        StopMovement();
    }

    private void OnDestroy()
    {
        if (stats != null) stats.OnCharacterDied -= HandleDeath;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        actionRunning = false;
        transform.localScale = baseScale;
        if (currentState != FinalBossState.Dead && stats != null && !stats.IsDead)
            currentState = FinalBossState.Chase;
        StopMovement();
    }
}
