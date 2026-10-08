using System.Collections;
using UnityEngine;

public class ExplosiveBarrel : MonoBehaviour
{
    [Tooltip("Optional fixed landing position. Used when no target player is assigned.")]
    public Vector2 landingPos;
    [HideInInspector] public float damage = 20f;

    [Header("Flight & Prediction")]
    [SerializeField, Min(0.1f)] private float flightSpeed = 12.5f;
    [SerializeField, Min(0f)] private float maxPredictionTime = 1.25f;
    [SerializeField, Min(0f)] private float maxPredictionDistance = 5f;
    [SerializeField, Min(0f)] private float landingScatterRadius;
    [SerializeField, Min(0.1f)] public float warningRadius = 5f;

    [SerializeField] private ParticleSystem efx;
    [SerializeField] private GameObject circ;

    public Transform targetPlayer;
    private charStats playerStats;
    private Rigidbody2D playerRigidbody;
    private Vector2 previousPlayerPosition;
    private Vector2 estimatedPlayerVelocity;
    private bool hasPreviousPlayerPosition;
    private bool hasFixedLandingPosition;

    /// <summary>
    /// Supplies the intended player target before the barrel's Start runs.
    /// The barrel estimates travel time and leads the target based on movement velocity.
    /// </summary>
    public void Initialize(Transform playerTarget, float barrelDamage, float scatterRadius = 0f)
    {
        targetPlayer = playerTarget;
        damage = barrelDamage;
        landingScatterRadius = Mathf.Max(0f, scatterRadius);
        hasFixedLandingPosition = false;
        CachePlayerReferences();
    }

    private void Start()
    {
        // Boss1AI supplies a fixed landingPos. Preserve that legacy behavior;
        // the final boss instead calls Initialize with a target transform.
        hasFixedLandingPosition = targetPlayer == null && landingPos != Vector2.zero;
        if (targetPlayer == null && !hasFixedLandingPosition) FindPlayerTarget();
        CachePlayerReferences();
        StartCoroutine(Fling());
    }

    private void Update()
    {
        if (targetPlayer == null) return;

        Vector2 currentPosition = targetPlayer.position;
        if (playerRigidbody != null && playerRigidbody.linearVelocity.sqrMagnitude > 0.01f)
        {
            estimatedPlayerVelocity = playerRigidbody.linearVelocity;
        }
        else if (hasPreviousPlayerPosition && Time.deltaTime > 0f)
        {
            // Fallback for players moved by Transform rather than a Rigidbody2D.
            estimatedPlayerVelocity = (currentPosition - previousPlayerPosition) / Time.deltaTime;
        }

        previousPlayerPosition = currentPosition;
        hasPreviousPlayerPosition = true;
    }

    private IEnumerator Fling()
    {
        // Legacy callers can still set landingPos directly. New boss barrels are
        // assigned a target and have their predicted landing point calculated here.
        if (targetPlayer != null)
        {
            // Give Update one frame to sample player movement when its velocity
            // is not available from Rigidbody2D.
            yield return null;
            landingPos = PredictLandingPosition();
        }

        while (targetPlayer == null && !hasFixedLandingPosition && landingPos == Vector2.zero)
            yield return null;

        Vector3 defaultSize = transform.localScale;
        Vector3 heightSize = defaultSize * 2f;
        Vector2 startPosition = transform.position;
        float duration = Mathf.Max(0.05f, Vector2.Distance(startPosition, landingPos) / Mathf.Max(0.1f, flightSpeed));
        float elapsedTime = 0f;
        AudioManager.Instance.PlaySFX("barrelthrow");
        GameObject warning = null;
        SpriteRenderer warningRenderer = null;
        if (circ != null)
        {
            warning = Instantiate(circ, landingPos, Quaternion.identity);
            warning.transform.localScale = Vector3.one * warningRadius * 2f;
            warningRenderer = warning.GetComponentInChildren<SpriteRenderer>();
        }

        while (elapsedTime < duration)
        {
            transform.Rotate(Vector3.forward, 360f * Time.deltaTime);
            float t = Mathf.Clamp01(elapsedTime / duration);
            float sizeT = t < 0.5f ? t * 2f : (1f - t) * 2f;
            transform.localScale = Vector3.Lerp(defaultSize, heightSize, sizeT);

            if (warningRenderer != null)
            {
                Color color = warningRenderer.color;
                color.a = Mathf.Lerp(0f, 0.5f, t);
                warningRenderer.color = color;
            }

            transform.position = Vector2.Lerp(startPosition, landingPos, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = landingPos;
        transform.localScale = defaultSize;
        if (warning != null) Destroy(warning);

        SpriteRenderer barrelRenderer = GetComponentInChildren<SpriteRenderer>();
        if (barrelRenderer != null) barrelRenderer.enabled = false;
        if (efx != null) efx.Play();
        AudioManager.Instance?.PlaySFX("explosion");

        float blastRadius = circ != null ? warningRadius : 0f;
        if (playerStats != null && !playerStats.IsDead &&
            Vector2.Distance(playerStats.transform.position, landingPos) <= blastRadius)
        {
            playerStats.TakeDamage(damage);
        }

        float effectDuration = efx != null ? efx.main.duration : 0f;
        Destroy(gameObject, effectDuration);
    }

    private Vector2 PredictLandingPosition()
    {
        Vector2 playerPosition = targetPlayer.position;
        Vector2 velocity = GetPlayerVelocity();
        float travelTime = Vector2.Distance(transform.position, playerPosition) / Mathf.Max(0.1f, flightSpeed);

        // Refine travel time a few times because leading the player also changes
        // the distance the barrel has to travel.
        for (int i = 0; i < 3; i++)
        {
            travelTime = Mathf.Min(maxPredictionTime,
                Vector2.Distance(transform.position, playerPosition + velocity * travelTime) / Mathf.Max(0.1f, flightSpeed));
        }

        Vector2 lead = Vector2.ClampMagnitude(velocity * travelTime, maxPredictionDistance);
        return playerPosition + lead + Random.insideUnitCircle * landingScatterRadius;
    }

    private Vector2 GetPlayerVelocity()
    {
        if (playerRigidbody != null)
            return playerRigidbody.linearVelocity;
        return estimatedPlayerVelocity;
    }

    private void FindPlayerTarget()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            playerController controller = FindAnyObjectByType<playerController>();
            if (controller != null) playerObject = controller.gameObject;
        }

        if (playerObject != null) targetPlayer = playerObject.transform;
    }

    private void CachePlayerReferences()
    {
        if (targetPlayer == null) return;
        playerStats = targetPlayer.GetComponent<charStats>();
        playerRigidbody = targetPlayer.GetComponent<Rigidbody2D>();
        previousPlayerPosition = targetPlayer.position;
        hasPreviousPlayerPosition = true;
    }
}
