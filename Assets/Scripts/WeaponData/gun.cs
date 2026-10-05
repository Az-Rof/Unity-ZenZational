using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum SpriteFacingDirection
{
    FacingRight = 0,    // 0°: Original sprite muzzle points Right (Unity standard)
    FacingDown = 90,    // 90°: Original sprite muzzle points Down (vertical art)
    FacingUp = -90,     // -90°: Original sprite muzzle points Up (vertical art)
    FacingLeft = 180,   // 180°: Original sprite muzzle points Left
    Custom = 999        // Use the manual value from customAngleOffset
}

/// <summary>
/// Weapon component attached to weapon PREFABS. All values (damage, fire rate, etc.)
/// are read from the WeaponStats located on the same GameObject.
/// </summary>
[RequireComponent(typeof(WeaponStats))]
public class gun : MonoBehaviour
{
    [Header("Weapon Configuration")]
    [SerializeField] private GameObject playerProjectilePrefab;
    [SerializeField] private Transform firePoint;

    [Header("Sprite Orientation & Offset")]
    [Tooltip("Select the original orientation of your weapon sprite art")]
    [SerializeField] private SpriteFacingDirection spriteDefaultFacing = SpriteFacingDirection.FacingRight;
    [Tooltip("Extra angle correction when using Custom mode, or for fine-tuning (degrees)")]
    [SerializeField] private float customAngleOffset = 0f;

    [Header("Layering / Depth Sorting")]
    [Tooltip("Automatically renders the gun behind the player when aiming up, and in front when aiming down")]
    [SerializeField] private bool autoDepthSorting = true;
    [SerializeField] private int baseSortingOrder = 3;

    [Header("Trajectory / Laser Sight")]
    [SerializeField] private bool showTrajectory = true;
    [SerializeField] private float trajectoryLength = 8f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private LineRenderer lineRenderer;

    // Runtime state
    private WeaponStats stats;
    private int currentAmmo;
    private float nextFireTime = 0f;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer parentSpriteRenderer;
    private charStats ownerStats;
    private Vector2 aimDirection;

    // Events for HUD integration & Scavenge logic
    public event Action<int, int> OnAmmoChanged;
    public event Action OnAmmoDepleted;
    public event Action<WeaponStats> OnWeaponEquipped;

    // Properties
    public GameObject BulletPrefab { get => playerProjectilePrefab; set => playerProjectilePrefab = value; }
    public WeaponStats Stats => stats;
    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => stats != null ? stats.maxAmmo : 0;
    public bool IsAmmoDepleted => currentAmmo <= 0;
    public Vector2 AimDirection => aimDirection;

    public SpriteFacingDirection SpriteFacing
    {
        get => spriteDefaultFacing;
        set => spriteDefaultFacing = value;
    }

    public float CustomAngleOffset
    {
        get => customAngleOffset;
        set => customAngleOffset = value;
    }

    void Awake()
    {
        stats = GetComponent<WeaponStats>();

        if (stats == null)
        {
            // The gun object/prefab in the scene does not have the WeaponStats component yet.
            // Without that component, ammo = 0 and the weapon cannot fire.
            Debug.LogWarning($"[{gameObject.name}] WeaponStats component not found! Add it to the weapon prefab.");
        }

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        ownerStats = GetComponentInParent<charStats>();
        if (transform.parent != null)
        {
            parentSpriteRenderer = transform.parent.GetComponentInParent<SpriteRenderer>();
        }
        SetupLineRenderer();
    }

    void Start()
    {
        // Ammo is initialized from the prefab stats when the weapon first becomes active.
        // (BuffSystem/weapon swap does not call this, so ammo is not reset.)
        currentAmmo = stats != null ? stats.maxAmmo : 0;
        OnAmmoChanged?.Invoke(currentAmmo, MaxAmmo);
        OnWeaponEquipped?.Invoke(stats);
    }

    void Update()
    {
        if (Time.timeScale != 0)
        {
            Aim();
            UpdateTrajectory();
            HandleShooting();
        }
    }

    private void Aim()
    {
        if (Mouse.current == null || Camera.main == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
        mouseWorldPos.z = 0f;

        // Compute the aim direction from the gun origin to the mouse cursor
        Vector3 origin = transform.position;
        aimDirection = ((Vector2)(mouseWorldPos - origin)).normalized;

        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

        // Determine the sprite orientation correction
        float baseOffset = spriteDefaultFacing == SpriteFacingDirection.Custom
            ? customAngleOffset
            : (float)spriteDefaultFacing;

        // Detect whether we are aiming toward the left (left quadrant)
        bool isAimingLeft = Mathf.Abs(angle) > 90f;

        // Rotate the gun transform so the muzzle and trajectory stay perfectly aligned
        float finalAngle = angle + (isAimingLeft ? -baseOffset : baseOffset);
        transform.rotation = Quaternion.Euler(0f, 0f, finalAngle);

        // Flip the gun sprite on Y so it does not look upside-down while aiming left
        if (spriteRenderer != null)
        {
            spriteRenderer.flipY = isAimingLeft;
        }

        // Auto depth sorting: when aiming up (behind the player), render behind the player
        if (autoDepthSorting && spriteRenderer != null)
        {
            int refOrder = parentSpriteRenderer != null ? parentSpriteRenderer.sortingOrder : baseSortingOrder;
            spriteRenderer.sortingOrder = aimDirection.y > 0.05f ? refOrder - 1 : refOrder + 1;
        }
    }

    private void HandleShooting()
    {
        if (Mouse.current == null) return;

        // Do not shoot while the buff selection menu is open — clicking a card
        // would otherwise also fire the gun behind the overlay.
        if (BuffSelectionUI.IsOpen) return;

        bool auto = stats != null ? stats.autoFire : true;
        bool isTryingToShoot = auto
            ? Mouse.current.leftButton.isPressed
            : Mouse.current.leftButton.wasPressedThisFrame;

        if (isTryingToShoot && Time.time >= nextFireTime)
        {
            if (currentAmmo > 0)
            {
                Shoot();
                float rate = stats != null ? stats.fireRate : 0.2f;
                nextFireTime = Time.time + rate;
            }
            else
            {
                OnAmmoDepleted?.Invoke();
                nextFireTime = Time.time + 0.3f;
                Debug.LogWarning("Out of Ammo! Scavenge zombie drops for new weapons.");
            }
        }
    }

    public void Shoot()
    {
        if (playerProjectilePrefab == null)
        {
            Debug.LogWarning("playerProjectilePrefab is not assigned on the Gun!");
            return;
        }

        // currentAmmo--;
        OnAmmoChanged?.Invoke(currentAmmo, MaxAmmo);

        if (currentAmmo <= 0)
        {
            OnAmmoDepleted?.Invoke();
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        int pellets = stats != null ? Mathf.Max(1, stats.pelletsCount) : 1;
        float spreadAngle = stats != null ? stats.spreadAngle : 0f;
        float damage = stats != null ? stats.bulletDamage : 10f;
        float speed = stats != null ? stats.bulletSpeed : 15f;
        float knockback = stats != null ? stats.knockbackForce : 1f;
        int pierce = stats != null ? stats.pierceCount : 0;

        for (int i = 0; i < pellets; i++)
        {
            Vector2 shotDir = aimDirection;

            if (spreadAngle > 0f)
            {
                float randomOffset = UnityEngine.Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
                shotDir = RotateVector(aimDirection, randomOffset);
            }
            AudioManager.Instance.PlaySFX("shoot");
            GameObject bullet = Instantiate(playerProjectilePrefab, spawnPos, Quaternion.identity);
            if (!bullet.TryGetComponent<projectile>(out var proj))
            {
                proj = bullet.AddComponent<projectile>();
            }

            proj.Setup(shotDir, speed, damage, knockback, pierce, ownerStats);
        }
        // AudioManager.Instance.PlaySFX("762x39 Single WAV");
    }

    public void RefillAmmo()
    {
        currentAmmo = stats != null ? stats.maxAmmo : 0;
        OnAmmoChanged?.Invoke(currentAmmo, MaxAmmo);
    }

    public static gun EquipWeaponPrefab(GameObject weaponPrefab, Transform holdPoint, GameObject bulletPrefab)
    {
        if (weaponPrefab == null || holdPoint == null) return null;

        // Destroy the old weapon currently attached to the hold point (if any)
        for (int i = holdPoint.childCount - 1; i >= 0; i--)
        {
            Destroy(holdPoint.GetChild(i).gameObject);
        }

        GameObject gunObj = Instantiate(weaponPrefab, holdPoint.position, Quaternion.identity, holdPoint);
        if (!gunObj.TryGetComponent<gun>(out gun newGun))
        {
            newGun = gunObj.AddComponent<gun>();
        }

        if (bulletPrefab != null && newGun.BulletPrefab == null)
        {
            newGun.BulletPrefab = bulletPrefab;
        }

        return newGun;
    }

    private Vector2 RotateVector(Vector2 v, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(cos * v.x - sin * v.y, sin * v.x + cos * v.y).normalized;
    }

    private void UpdateTrajectory()
    {
        if (!showTrajectory || lineRenderer == null) return;

        Vector3 startPos = firePoint != null ? firePoint.position : transform.position;
        startPos.z = 0f;

        Vector3 endPos = startPos + (Vector3)(aimDirection * trajectoryLength);
        RaycastHit2D[] hits = Physics2D.RaycastAll(startPos, aimDirection, trajectoryLength, hitLayers.value != 0 ? hitLayers : ~0);
        foreach (var hit in hits)
        {
            if (hit.collider != null && !hit.collider.isTrigger && !hit.transform.IsChildOf(transform.root))
            {
                endPos = hit.point;
                break;
            }
        }
        endPos.z = 0f;

        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, endPos);
    }

    private void SetupLineRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }

        lineRenderer.startWidth = 0.04f;
        lineRenderer.endWidth = 0.01f;
        lineRenderer.useWorldSpace = true;
        lineRenderer.sortingOrder = 10;

        if (lineRenderer.material == null || lineRenderer.sharedMaterial == null)
        {
            Shader defaultShader = Shader.Find("Sprites/Default");
            if (defaultShader != null)
            {
                lineRenderer.material = new Material(defaultShader);
            }
        }

        Color startColor = new Color(1f, 0.95f, 0.2f, 0.7f);
        Color endColor = new Color(1f, 0.4f, 0f, 0.1f);
        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
    }
}
