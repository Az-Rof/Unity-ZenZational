using KinoGlitch;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Runs the finite story sequence: Wave 1, Wave 2, Boss 1, Wave 3, final boss.</summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Spawn Configuration")]
    [SerializeField] private GameObject zombiePrefab;
    [SerializeField] private GameObject boss1Prefab;
    [SerializeField] private GameObject finalBossPrefab;
    [SerializeField] private GameObject finalBossDecoyPrefab;
    [SerializeField] private GameObject finalBossExplosionPrefab;
    [SerializeField] private Sprite[] finalBossSprites;
    
    [SerializeField] private waveEvents[] events;
    [SerializeField, Min(0)] private int startingWaveSize = 10;
    [SerializeField, Min(0)] private int waveTide = 5;
    [SerializeField, Min(0.01f)] private float spawnInterval = 0.5f;
    [SerializeField, Min(1)] private int maxConcurrentZombies = 50;
    [SerializeField, Min(0f)] private float spawnRadiusMin = 10f;
    [SerializeField, Min(0f)] private float spawnRadiusMax = 15f;
    [Tooltip("Keep greater than Boss1AI's off-screen jump radius.")]
    [SerializeField, Min(1f)] private float bossSpawnRadius = 35f;
    [SerializeField, Min(0f)] private float startDelay = 1f;

    [Header("Zombie Stat Override")]
    [SerializeField] private bool overrideZombieStats;
    [SerializeField, Min(1f)] private float zombieHealth = 40f;
    [SerializeField, Min(0f)] private float zombieSpeed = 3f;
    [SerializeField, Min(1f)] private float finalEncounterZombieSpeedMultiplier = 1.5f;

    [Header("Story Progression")]
    [SerializeField, Min(1)] private int finalZombieWave = 3;
    [SerializeField] private bool waitForBoss1Defeat = true;
    [SerializeField] private GlitchingManager glitchingManager;
    [SerializeField, Range(0, 100)] private int wave2GlitchPercent = 12;
    [SerializeField, Min(0f)] private float wave2GlitchDuration = 2f;
    [SerializeField, Min(0f)] private float wave2FakePowerOffDuration = 1.25f;
    [SerializeField, Range(0, 100)] private int wave3GlitchPercent = 35;
    [SerializeField, Min(0f)] private float wave3GlitchDuration = 3f;

    [SerializeField, Min(0f)] private float finalRevealBlackDuration = 2f;
    [SerializeField, Min(0.01f)] private float finalFaceLungeDuration = 0.12f;
    [SerializeField, Min(0f)] private float finalFaceStareDuration = 1f;
    [SerializeField, Min(0.01f)] private float finalFaceScale = 1.5f;
    [SerializeField, Min(0f)] private float finalDecoyRevealDelay = 3f;
    [SerializeField, Min(1f)] private float finalBossPlaceholderHealth = 2500f;
    [SerializeField, Min(0f)] private float finalBossPlaceholderSpeed = 6f;
    [SerializeField, Min(0f)] private float finalBossPlaceholderDamage = 35f;
    [SerializeField, Min(1f)] private float decoyBossHealth = 300f;
    [SerializeField] private bool allowFinalBossPlaceholder = true;




    [Header("Runtime State (read only)")]
    [SerializeField] public int wC;
    [SerializeField] private bool spawnEnabled = true;
    [SerializeField] private int currentWave = 1;

    private Transform playerTransform;
    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private Coroutine progressionRoutine;
    private int lastReportedCount = -1;
    private bool boss1WasSpawned;

    public event System.Action<int> OnZombieCountChanged;
    public event System.Action<int> OnWaveStarted;
    public int ActiveZombieCount => activeEnemies.Count;
    public int CurrentWave => currentWave;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        FindPlayer();
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMusic("WaveMusic");

        if (glitchingManager == null)
            glitchingManager = FindAnyObjectByType<GlitchingManager>();
        progressionRoutine = StartCoroutine(StoryProgressionRoutine());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        activeEnemies.RemoveAll(item => item == null);
        if (activeEnemies.Count != lastReportedCount)
        {
            lastReportedCount = activeEnemies.Count;
            OnZombieCountChanged?.Invoke(activeEnemies.Count);
        }
    }

    private IEnumerator StoryProgressionRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        for (currentWave = 1; currentWave <= finalZombieWave; currentWave++)
        {
            wC = currentWave - 1;
            if (currentWave == 4)
            {
                if (glitchingManager != null)
                {
                    glitchingManager.PlayWaveGlitch(wave2GlitchPercent, wave2GlitchDuration);
                    glitchingManager.SetWaveGlitch(wave2GlitchPercent);
                }
            }
            else if (currentWave >= 6)
            {
                if (glitchingManager != null)
                {
                    glitchingManager.PlayWaveGlitch(wave3GlitchPercent, wave3GlitchDuration);
                    glitchingManager.SetWaveGlitch(wave3GlitchPercent);
                }
            }

            yield return RunZombieWave(currentWave);
            yield return WaitUntilEnemiesDefeated();

            // Reward after each zombie wave; BuffSelectionUI pauses gameplay until a choice is made.
            BuffSystem.Instance?.TriggerBuffSelection();
            yield return WaitForBuffChoice();

            if (currentWave == 5 && HasWaveEvent(5, "SpawnBoss1"))
            {
                if (glitchingManager != null)
                    yield return glitchingManager.PlayFakePowerOff(wave2FakePowerOffDuration);

                // BuffSystem.Instance?.TriggerBuffSelection();
                // yield return WaitForBuffChoice();

                yield return SpawnBoss1AndWait();
                if (glitchingManager != null) glitchingManager.ClearWaveGlitch();
            }
        }

        spawnEnabled = false;
        currentWave = finalZombieWave + 1;
        yield return StartFinalBoss();
        progressionRoutine = null;
    }

    private IEnumerator RunZombieWave(int waveNumber)
    {
        OnWaveStarted?.Invoke(waveNumber);
        int waveSize = Mathf.Max(0, startingWaveSize + waveTide * (waveNumber - 1));
        int spawned = 0;

        while (spawned < waveSize)
        {
            if (!IsPlayerAlive()) yield break;
            activeEnemies.RemoveAll(item => item == null);
            if (spawnEnabled && activeEnemies.Count < maxConcurrentZombies)
            {
                SpawnZombie();
                spawned++;
            }
            yield return new WaitForSeconds(spawnInterval);
        }
        spawnInterval = Mathf.Lerp(spawnInterval, 0, 0.125f);
    }

    private IEnumerator WaitUntilEnemiesDefeated()
    {
        while (true)
        {
            if (!IsPlayerAlive()) yield break;
            PruneDefeatedEnemies();
            if (activeEnemies.Count == 0) yield break;
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator WaitForBuffChoice()
    {
        // Avoid advancing before a present BuffSelectionUI has completed its selection.
        while (BuffSelectionUI.IsOpen)
            yield return null;
    }

    private IEnumerator SpawnBoss1AndWait()
    {
        if (boss1WasSpawned) yield break;
        boss1WasSpawned = true;
        SpawnBoss1();

        if (!waitForBoss1Defeat) yield break;
        while (true)
        {
            if (!IsPlayerAlive()) yield break;
            PruneDefeatedEnemies();
            if (activeEnemies.Count == 0)
            {
                AudioManager.Instance.PlayMusic("WaveMusic");
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

 

    private IEnumerator StartFinalBoss()
    {
        if (!HasWaveEvent(finalZombieWave, "SpawnFinalBoss")) yield break;
        AudioManager.Instance?.PauseMusic();


        GameObject resolvedFinalBossPrefab = finalBossPrefab != null ? finalBossPrefab : boss1Prefab;
        Sprite faceSprite = finalBossSprites[0];
        if (faceSprite == null && resolvedFinalBossPrefab != null)
        {
            SpriteRenderer faceRenderer = resolvedFinalBossPrefab.GetComponentInChildren<SpriteRenderer>();
            if (faceRenderer != null && faceRenderer.sprite != null) faceSprite = faceRenderer.sprite;
        }

        GameObject finalBoss = null;
        if (glitchingManager != null)
        {
            glitchingManager.PlayRevealGlitch(wave3GlitchPercent, 0.35f);
            yield return new WaitForSecondsRealtime(0.35f);
            yield return glitchingManager.PlayFaceReveal(
                faceSprite,
                finalRevealBlackDuration,
                finalFaceLungeDuration,
                finalFaceStareDuration,
                finalFaceScale,
                playScream: false
                );
            // Start the transition loop after the first face reveal and keep it through the final boss spawn.
            glitchingManager.StartGlitchAudioLoop();
        }
        else
        {
            Debug.LogWarning("[WaveManager] GlitchingManager is not assigned; skipping the cinematic blackout and face reveal.", this);
        }

        // Stop music after the first reveal; keep it paused through the decoy and second reveal.

        List<GameObject> decoys = new List<GameObject>();
        for (int i = 0; i < startingWaveSize + (wC * waveTide); i++)
        {
            GameObject decoyPrefab = finalBossDecoyPrefab != null ? finalBossDecoyPrefab : zombiePrefab;
            GameObject decoy = null;
            if (decoyPrefab != null)
            {
                decoy = Instantiate(decoyPrefab, RandomSpawnPosition(15f, 20f), Quaternion.identity);
                decoy.name = "Final Reveal Decoy (Zombie)";
                if (decoy.TryGetComponent(out charStats decoyStats))
                {
                    decoyStats.speed = Mathf.Max(decoyStats.speed, 3f);
                    decoyStats.maxHealth = decoyBossHealth;
                    decoyStats.currentHealth = decoyBossHealth;
                }
                else
                {
                    Debug.LogWarning("[WaveManager] The final reveal decoy prefab needs charStats to be tracked and defeated.", decoy);
                }
                RegisterEnemy(decoy);
                decoys.Insert(0, decoy);
            }
        }
        // Give the player a short moment to notice/defeat the ordinary zombie decoy.
        yield return new WaitForSeconds(finalDecoyRevealDelay);

        if (glitchingManager != null)
        {
            glitchingManager.PlayRevealGlitch(wave3GlitchPercent, 0.35f);
            yield return new WaitForSecondsRealtime(0.35f);
            yield return glitchingManager.PlayFaceReveal2(
               finalBossSprites[1], finalBossSprites[2],
                finalRevealBlackDuration,
                finalFaceLungeDuration,
                finalFaceStareDuration,
                finalFaceScale
                );
        }
        else
        {
            Debug.LogWarning("[WaveManager] GlitchingManager is not assigned; skipping the cinematic blackout and face reveal.", this);
        }

        // Music returns only after the second reveal is complete.

        yield return new WaitForSeconds(1);
        Vector2 spawnpositioning = Vector2.zero;
        if (decoys.Count > 0)
        {
            GameObject realOne = decoys[Random.Range(0, decoys.Count)];
            if (finalBossExplosionPrefab != null)
            {
                Instantiate(finalBossExplosionPrefab, realOne.transform.position, Quaternion.identity);
                spawnpositioning = realOne.transform.position;  
                realOne.GetComponent<charStats>().currentHealth = 1;
                realOne.GetComponent<charStats>().TakeDamage(20);
                activeEnemies.Remove(realOne);
            }
            if (finalBoss == null && resolvedFinalBossPrefab != null)
            {
                finalBoss = SpawnFinalBoss(resolvedFinalBossPrefab, true, realOne.transform.position);
            }
            else if (finalBoss == null && allowFinalBossPlaceholder)
            {
                finalBoss = CreateFinalBossPlaceholder(RandomSpawnPosition(4f, 6f));
                Debug.LogWarning("[WaveManager] Neither finalBossPrefab nor boss1Prefab is assigned. A temporary generic placeholder was spawned.", this);
            }
            else if (finalBoss == null)
            {
                Debug.LogWarning("[WaveManager] Assign finalBossPrefab or boss1Prefab to start the final boss encounter.", this);
            }
        }

        if (glitchingManager != null)
            glitchingManager.StopGlitchAudioLoop();

        foreach (GameObject decoy in decoys)
        {
            if (decoy != null)
            {
                decoy.GetComponent<charStats>().currentHealth = 1;
                decoy.GetComponent<charStats>().TakeDamage(20);
                finalBoss.transform.position = spawnpositioning;
            }
        }


        RegisterEnemy(finalBoss);
        AudioManager.Instance?.ResumeMusic();
        yield return new WaitForSeconds(0.1f);
        finalBoss.transform.position = spawnpositioning;
    }

    private GameObject SpawnFinalBoss(GameObject bossPrefab, bool triggerOpeningAttack, Vector3 pos)
    {
        if (bossPrefab == null) return null;

 

        GameObject boss = Instantiate(bossPrefab, pos, Quaternion.identity);
        boss.name = "Final Boss (Boss 1 Prefab)";
        UNSEENCUTSCENEDEATH ucd = GetComponent<UNSEENCUTSCENEDEATH>();
        ucd.sts = boss.GetComponent<charStats>();
        ucd.enabled = true;
        if (boss.TryGetComponent(out charStats bossStats))
        {
            bossStats.currentHealth = bossStats.maxHealth;
            bossStats.speed = Mathf.Max(bossStats.speed, finalBossPlaceholderSpeed);
        }

        Boss1AI bossAI = boss.GetComponent<Boss1AI>();
        if (bossAI != null)
        {
            bossAI.EnableLandingShockwaves(true);
            if (triggerOpeningAttack)
                bossAI.TriggerOpeningAttack();
        }
        else
        {
            Debug.LogWarning("[WaveManager] Boss 1 prefab has no Boss1AI; cannot trigger its opening jump attack.", boss);
        }

        return boss;
    }

    private GameObject CreateFinalBossPlaceholder(Vector3 position)
    {
        GameObject boss = new GameObject("FINAL BOSS PLACEHOLDER - UNUSED CHARACTER NOT ASSIGNED") { tag = "Enemy" };
        boss.transform.position = position;

        SpriteRenderer renderer = boss.AddComponent<SpriteRenderer>();
        SpriteRenderer sourceRenderer = zombiePrefab != null
            ? zombiePrefab.GetComponentInChildren<SpriteRenderer>()
            : boss1Prefab != null ? boss1Prefab.GetComponentInChildren<SpriteRenderer>() : null;
        if (sourceRenderer != null) renderer.sprite = sourceRenderer.sprite;
        renderer.color = new Color(0.25f, 0.02f, 0.08f, 1f);
        renderer.sortingOrder = 10;
        boss.transform.localScale = Vector3.one * 2f;

        CircleCollider2D collider = boss.AddComponent<CircleCollider2D>();
        collider.radius = 0.8f;
        Rigidbody2D body = boss.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;

        charStats stats = boss.AddComponent<charStats>();
        stats.CharacterName = "Final Boss Placeholder";
        stats.maxHealth = finalBossPlaceholderHealth;
        stats.currentHealth = finalBossPlaceholderHealth;
        stats.speed = finalBossPlaceholderSpeed;
        stats.attackPower = finalBossPlaceholderDamage;
        stats.attackRange = 1.5f;
        boss.AddComponent<ZombieAI>();
        return boss;
    }

    private bool HasWaveEvent(int waveNumber, string eventName)
    {
        if (events == null) return false;
        foreach (waveEvents waveEvent in events)
        {
            if (waveEvent != null && waveEvent.waveNumber == waveNumber && waveEvent.eventName == eventName)
                return true;
        }
        return false;
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            playerController player = FindAnyObjectByType<playerController>();
            if (player != null) playerObject = player.gameObject;
        }
        if (playerObject != null) playerTransform = playerObject.transform;
    }

    private Vector3 RandomSpawnPosition(float minimum, float maximum)
    {
        if (playerTransform == null) FindPlayer();
        if (playerTransform == null && minimum > 0f)
        {
            Debug.LogWarning("[WaveManager] Cannot spawn enemies because no Player was found.", this);
            return transform.position;
        }
        if (playerTransform == null) return transform.position;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(minimum, Mathf.Max(minimum, maximum));
        return playerTransform.position + new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0f);
    }

    private void SpawnZombie()
    {
        GameObject zombie;
        if (zombiePrefab != null)
        {
            zombie = Instantiate(zombiePrefab, RandomSpawnPosition(spawnRadiusMin, spawnRadiusMax), Quaternion.identity);
            ApplyStatOverrides(zombie);
            if (currentWave > finalZombieWave && zombie.TryGetComponent(out charStats stats))
                stats.speed *= finalEncounterZombieSpeedMultiplier;
        }
        else
        {
            zombie = CreateRuntimeZombie(RandomSpawnPosition(spawnRadiusMin, spawnRadiusMax));
        }
        RegisterEnemy(zombie);
    }

    private void SpawnBoss1()
    {
        if (boss1Prefab == null)
        {
            Debug.LogError("[WaveManager] SpawnBoss1 event is configured, but boss1Prefab is not assigned.", this);
            return;
        }
        GameObject boss = Instantiate(boss1Prefab, RandomSpawnPosition(bossSpawnRadius, bossSpawnRadius), Quaternion.identity);
        RegisterEnemy(boss);
    }

    private void RegisterEnemy(GameObject enemy)
    {
        if (enemy == null) return;
        activeEnemies.Add(enemy);
        lastReportedCount = -1;
    }

    private void PruneDefeatedEnemies()
    {
        activeEnemies.RemoveAll(enemy => enemy == null ||
            !enemy.TryGetComponent<charStats>(out _) ||
            enemy.TryGetComponent(out charStats stats) && stats.IsDead);
    }

    private void ApplyStatOverrides(GameObject zombie)
    {
        if (!overrideZombieStats || zombie == null) return;
        if (!zombie.TryGetComponent<charStats>(out charStats stats)) return;
        stats.maxHealth = zombieHealth;
        stats.currentHealth = zombieHealth;
        stats.speed = zombieSpeed;
    }

    private GameObject CreateRuntimeZombie(Vector3 position)
    {
        GameObject zombie = new GameObject("Zombie (Runtime)") { tag = "Enemy" };
        zombie.transform.position = position;
        SpriteRenderer renderer = zombie.AddComponent<SpriteRenderer>();
        renderer.color = new Color(0.8f, 0.2f, 0.2f, 1f);
        renderer.sortingOrder = 2;

        Texture2D texture = new Texture2D(16, 16);
        Color[] pixels = new Color[texture.width * texture.height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply();
        renderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 16f);

        CircleCollider2D collider = zombie.AddComponent<CircleCollider2D>();
        collider.radius = 0.5f;
        Rigidbody2D body = zombie.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;

        charStats stats = zombie.AddComponent<charStats>();
        stats.maxHealth = zombieHealth;
        stats.currentHealth = zombieHealth;
        stats.attackPower = 12f;
        stats.attackRange = 1.3f;
        stats.speed = zombieSpeed;
        zombie.AddComponent<ZombieAI>();
        return zombie;
    }

    public void StopSpawning()
    {
        spawnEnabled = false;
        if (progressionRoutine != null)
        {
            StopCoroutine(progressionRoutine);
            progressionRoutine = null;
        }
    }

    private bool IsPlayerAlive()
    {
        return FindAnyObjectByType<playerController>() != null &&
               FindAnyObjectByType<Titlescreen>() != null &&
               SceneManager.GetActiveScene().buildIndex != 0;
    }

    public void ResumeSpawning()
    {
        if (progressionRoutine == null)
        {
            spawnEnabled = true;
            progressionRoutine = StartCoroutine(StoryProgressionRoutine());
        }
    }

    public void ClearAllZombies()
    {
        foreach (GameObject enemy in activeEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        activeEnemies.Clear();
        lastReportedCount = -1;
    }
}
