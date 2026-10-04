using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simplified WaveManager: only spawns zombies around the player.
/// No buffs, glitches, bosses, or progression states yet — those will be added
/// once BuffSystem / GameHUD / ItemPickup have been designed.
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Spawn Configuration")]
    [SerializeField] private GameObject zombiePrefab;
    [SerializeField] int StartingWaveSize;
    [Tooltip("Amount to increase wave size by")]
    [SerializeField] int WaveTide;
    [Tooltip("Time interval between zombie spawns (seconds)")]
    [SerializeField] private float spawnInterval = 2f;
    [Tooltip("Maximum number of zombies alive at the same time")]
    [SerializeField] private int maxConcurrentZombies = 8;
    [Tooltip("Minimum spawn radius from the player position (units)")]
    [SerializeField] private float spawnRadiusMin = 10f;
    [Tooltip("Maximum spawn radius from the player position (units)")]
    [SerializeField] private float spawnRadiusMax = 15f;
    [Tooltip("Initial delay before the first spawn (seconds)")]
    [SerializeField] private float startDelay = 1f;

    [Header("Stat Override (optional)")]
    [Tooltip("If true, the zombie prefab stats will be overridden with the values below")]
    [SerializeField] private bool overrideZombieStats = false;
    [SerializeField] private float zombieHealth = 40f;
    [SerializeField] private float zombieSpeed = 3f;
    public int wC = 0;

    [Header("State")]
    [Tooltip("Uncheck to stop spawning via the Inspector during play")]
    [SerializeField] private bool spawnEnabled = true;

    private Transform playerTransform;
    private readonly List<GameObject> activeZombies = new List<GameObject>();
    private Coroutine spawnLoop;
    private int lastReportedCount = -1;

    /// <summary> Event for future HUD integration (alive zombie count) </summary>
    public event System.Action<int> OnZombieCountChanged;

    /// <summary> Number of zombies currently alive </summary>
    public int ActiveZombieCount => activeZombies.Count;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        FindPlayer();
        AudioManager.Instance.PlayMusic("WaveMusic");
        spawnLoop = StartCoroutine(SpawnLoopRoutine());
    }

    void Update()
    {
        // Purge destroyed/dead zombie references from the list
        activeZombies.RemoveAll(item => item == null);

        if (activeZombies.Count != lastReportedCount)
        {
            lastReportedCount = activeZombies.Count;
            OnZombieCountChanged?.Invoke(activeZombies.Count);
        }
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            playerController pc = FindAnyObjectByType<playerController>();
            if (pc != null) playerObj = pc.gameObject;
        }

        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    private IEnumerator SpawnLoopRoutine()
    {
        while (true) {
            int Zmb = 0;
            int wS = StartingWaveSize + (WaveTide * wC);
            yield return new WaitForSeconds(startDelay);

            while (Zmb < wS)
            {
                if (spawnEnabled && activeZombies.Count < maxConcurrentZombies)
                {
                    SpawnZombie();
                    Zmb++;
                }

                yield return new WaitForSeconds(spawnInterval);
            }
            yield return new WaitForSeconds(0.1f);
            while (lastReportedCount > 0)
            {
                yield return new WaitForSeconds(0.1f);
            }
            yield return new WaitForSeconds(startDelay);
            BuffSystem.Instance.TriggerBuffSelection();
            Zmb = 0;
            wC++;
        }
    }

    private void SpawnZombie()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return; // Player is not in the scene yet
        }

        Vector3 spawnPos = GetRandomSpawnPositionAroundPlayer();

        GameObject zombie;
        if (zombiePrefab != null)
        {
            zombie = Instantiate(zombiePrefab, spawnPos, Quaternion.identity);
            ApplyStatOverrides(zombie);
        }
        else
        {
            // Fallback: create a runtime zombie if no prefab is assigned in the Inspector
            zombie = CreateRuntimeZombie(spawnPos);
        }

        if (zombie != null)
        {
            activeZombies.Add(zombie);
            lastReportedCount = -1; // force refresh of the count event
        }
    }

    /// <summary> Random position on a ring (donut) around the player </summary>
    private Vector3 GetRandomSpawnPositionAroundPlayer()
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float dist = UnityEngine.Random.Range(spawnRadiusMin, spawnRadiusMax);
        return playerTransform.position + new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);
    }

    private void ApplyStatOverrides(GameObject zombie)
    {
        if (!overrideZombieStats) return;

        if (zombie.TryGetComponent<charStats>(out var stats))
        {
            stats.maxHealth = zombieHealth;
            stats.currentHealth = zombieHealth;
            stats.speed = zombieSpeed;
        }
    }

    private GameObject CreateRuntimeZombie(Vector3 position)
    {
        GameObject zombie = new GameObject("Zombie (Runtime)");
        zombie.transform.position = position;
        zombie.tag = "Enemy";

        SpriteRenderer sr = zombie.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.8f, 0.2f, 0.2f, 1f); // deep red
        sr.sortingOrder = 2;
        sr.sprite = CreateDebugSprite();

        CircleCollider2D col = zombie.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;

        Rigidbody2D rb = zombie.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.linearDamping = 3f; // knockback momentum decays naturally

        charStats stats = zombie.AddComponent<charStats>();
        stats.maxHealth = zombieHealth;
        stats.currentHealth = zombieHealth;
        stats.attackPower = 12f;
        stats.attackRange = 1.3f;
        stats.speed = zombieSpeed;

        zombie.AddComponent<ZombieAI>();

        return zombie;
    }

    private static Sprite CreateDebugSprite()
    {
        // 32x32 white square sprite used as the zombie's visual placeholder
        Texture2D tex = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
    }

    /// <summary> Stop the spawner (call this on game over, etc.) </summary>
    public void StopSpawning()
    {
        spawnEnabled = false;
        if (spawnLoop != null) StopCoroutine(spawnLoop);
        spawnLoop = null;
    }

    /// <summary> Resume the spawner </summary>
    public void ResumeSpawning()
    {
        if (spawnLoop == null)
        {
            spawnEnabled = true;
            spawnLoop = StartCoroutine(SpawnLoopRoutine());
        }
    }

    /// <summary> Destroy every zombie still alive </summary>
    public void ClearAllZombies()
    {
        foreach (GameObject zombie in activeZombies)
        {
            if (zombie != null) Destroy(zombie);
        }
        activeZombies.Clear();
        lastReportedCount = -1;
    }
}
