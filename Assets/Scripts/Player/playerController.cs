using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum PlayerAimMode
{
    [Tooltip("The gun orbits around the player 360 degrees following the mouse cursor (Enter the Gungeon / Nuclear Throne style)")]
    OrbitGunAroundPlayer,
    [Tooltip("The entire character body rotates to face the mouse cursor (Hotline Miami style)")]
    RotateEntirePlayer
}

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(charStats))]
public class playerController : MonoBehaviour
{
    [Header("Gun Setup")]
    [Tooltip("Weapon PREFAB (must contain the gun + WeaponStats components)")]
    [SerializeField] private GameObject playerGunPrefab;
    [SerializeField] private GameObject playerProjectilePrefab;
    [SerializeField] private Transform gunHoldPoint;
    [SerializeField] private gun currentGun;
    [SerializeField] private Transform cam;


    [Header("Aiming & Gun Hold Behavior")]
    [SerializeField] private PlayerAimMode aimMode = PlayerAimMode.OrbitGunAroundPlayer;
    [Tooltip("Orbit distance of the gun from the player's body center (e.g. 0.6 unit)")]
    [SerializeField] private float gunHoldDistance = 0.6f;
    [Tooltip("Offset of the player's hand pivot center (e.g. Y = 0.1 at chest height)")]
    [SerializeField] private Vector2 gunPivotOffset = Vector2.zero;
    [Tooltip("Automatically flips the player's body sprite left/right following the mouse position")]
    [SerializeField] private bool flipPlayerSpriteWithMouse = true;

    private charStats stats;
    private Rigidbody2D rb2d;
    private SpriteRenderer spriteRenderer;
    private Vector2 aimDirection;

    [SerializeField] Image HP;

    // Get & Set
    public charStats Stats { get => stats; set => stats = value; }
    public gun CurrentGun => currentGun;
    public Transform GunHoldPoint => gunHoldPoint;
    public bool IsInScavengeMode => currentGun != null && currentGun.IsAmmoDepleted;

    /// The Slider used to visualize the player's health (assigned via Inspector).
    bool moving;

    void Awake()
    {
        if (!CompareTag("Player"))
        {
            gameObject.tag = "Player";
        }

        rb2d = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Start()
    {
        stats = GetComponent<charStats>();
        InitializeGun();
        SetupHPSlider();
        StartCoroutine(WalkingSound());
    }

    IEnumerator WalkingSound()
    {
        while (true)
        {
            if (moving)
            {
                // Play walking sound here
                AudioManager.Instance.PlaySFX("playerwalk");
            }
            yield return new WaitForSeconds(0.35f / (stats.speed/10)); // Adjust the interval as needed
        }
    }

    void Update()
    {
        MovePlayer();
        UpdateAimingAndGunPosition();
    }

    /// Wires the health Slider to the player's charStats health events.
    /// The slider updates automatically whenever the player takes damage or heals.

    private void SetupHPSlider()
    {
        if (stats == null) return;

        // Subscribe to health changes (damage, healing, initialization)
        stats.OnHealthChanged -= HandleHealthChanged; // prevent duplicate subscriptions
        stats.OnHealthChanged += HandleHealthChanged;

        // Initialize slider range and show the starting value
        if (HP != null)
        {
            HP.fillAmount = Mathf.Clamp01(stats.currentHealth / stats.maxHealth );
        }
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        if (HP == null) return;

      
        HP.fillAmount = Mathf.Clamp01(stats.currentHealth / stats.maxHealth);
    }

    private void InitializeGun()
    {

        if (gunHoldPoint == null)
        {
            GameObject holdObj = new GameObject("GunHoldPoint");
            holdObj.transform.SetParent(transform);
            holdObj.transform.localPosition = new Vector3(gunHoldDistance, 0f, 0f);
            gunHoldPoint = holdObj.transform;
        }

        if (currentGun == null)
        {
            currentGun = GetComponentInChildren<gun>();
        }
        if (currentGun == null && playerGunPrefab != null)
        {
            Transform parent = gunHoldPoint != null ? gunHoldPoint : transform;
            GameObject gunObj = Instantiate(playerGunPrefab, parent.position, Quaternion.identity, parent);
            currentGun = gunObj.GetComponent<gun>();
            if (currentGun == null)
            {
                currentGun = gunObj.AddComponent<gun>();
            }
        }

        // Forward the bullet prefab reference to the gun if it is not assigned there yet
        if (currentGun != null && currentGun.BulletPrefab == null && playerProjectilePrefab != null)
        {
            currentGun.BulletPrefab = playerProjectilePrefab;
        }
    }
    /// Swap the player's weapon using a weapon PREFAB (scavenge / pickup system).
    /// The old weapon is destroyed and the new one is attached to the GunHoldPoint.
    public gun EquipWeaponPrefab(GameObject weaponPrefab)
    {
        currentGun = gun.EquipWeaponPrefab(weaponPrefab, gunHoldPoint, playerProjectilePrefab);
        return currentGun;
    }

    /// Swap weapons via a prefab name from the Inspector (e.g. a UI button).
    public void EquipWeaponPrefabByName(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return;

        GameObject prefab = Resources.Load<GameObject>($"Weapons/{prefabName}");
        if (prefab == null)
        {
            Debug.LogWarning($"[playerController] Weapon prefab '{prefabName}' not found in Resources/Weapons!");
            return;
        }

        EquipWeaponPrefab(prefab);
    }

    void MovePlayer()
    {
        if (stats != null && stats.IsDead)
        {
            if (rb2d != null) rb2d.linearVelocity = Vector2.zero;
            return;
        }

        // While being knocked back (e.g. by zombie melee), physics owns the
        // velocity — do not override it so the impulse is actually visible.
        if (stats != null && stats.IsKnockedBack)
        {
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical -= 1f;

            if (Keyboard.current.bKey.isPressed)
            {
                BuffSystem.Instance.TriggerBuffSelection();
            }
        }

        Vector2 direction = new Vector2(horizontal, vertical).normalized;
     
            moving = (direction.magnitude > 0.1f);
        
        if (rb2d != null)
        {
            rb2d.linearVelocity = direction * stats.speed;
        }
        else
        {
            transform.Translate(direction * stats.speed * Time.deltaTime, Space.World);
        }
    }

    private void UpdateAimingAndGunPosition()
    {
        if (Mouse.current == null || Camera.main == null || Time.timeScale == 0) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
        mouseWorldPos.z = 0f;

        Vector3 centerPos = transform.position + (Vector3)gunPivotOffset;
        aimDirection = ((Vector2)(mouseWorldPos - centerPos)).normalized;

        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

        if (aimMode == PlayerAimMode.OrbitGunAroundPlayer)
        {
            transform.rotation = Quaternion.identity;

            if (gunHoldPoint != null)
            {
                Vector3 targetPos = centerPos + (Vector3)(aimDirection * gunHoldDistance);
                gunHoldPoint.position = targetPos;
            }
            if (flipPlayerSpriteWithMouse && spriteRenderer != null)
            {
                spriteRenderer.flipX = mouseWorldPos.x < transform.position.x;
            }
        }
        else if (aimMode == PlayerAimMode.RotateEntirePlayer)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
            cam.rotation = Quaternion.Euler(0f, 0f, 0);

        }
    }
}
