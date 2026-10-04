using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum BuffType
{
    // --- Player stats ---
    MovementSpeed,
    MaxHealth,
    Healing,
    Revive,
    Unflinching,
    Vampirism,

    // --- Weapon upgrades ---
    AttackDamage,
    FireRate,
    KnockbackForce,
    BulletSpeed,
    Accuracy,
    PierceCount,
    ExtraPellet,
    AmmoCapacity
}

// Buff card category — determines the label & color in the menu. 
public enum BuffCategory
{
    Character,
    Gun
}

[System.Serializable]
public class BuffOption
{
    public BuffType type;
    public BuffCategory category;
    public string title;
    public string description;
    public float value;

    [Tooltip("Existing card prefab used to display this buff")]
    public GameObject cardPrefab;

    public BuffOption(BuffType type, BuffCategory category, string title, string description, float value)
    {
        this.type = type;
        this.category = category;
        this.title = title;
        this.description = description;
        this.value = value;
    }
}

public class BuffSystem : MonoBehaviour
{
    public static BuffSystem Instance { get; private set; }

    public event Action<List<BuffOption>> OnBuffSelectionPrompt;
    public event Action<BuffOption> OnBuffSelected;
    // Fired after a buff is picked — for other integrations (HUD, etc.).
    public event Action OnBuffSelectionClosed;

    [Header("Prebuilt Buff Cards")]
    [Tooltip("Assign the existing card prefabs in the same order as the matching buff options below")]
    [SerializeField] private BuffCardDefinition[] cardDefinitions;

    [Header("Buff Selection")]
    [Tooltip("Ensure every menu appearance contains at least 1 Character buff & 1 Gun buff")]
    [SerializeField] private bool guaranteeCategoryMix = false;
    [SerializeField] GameObject buffmenu;
    private readonly List<BuffOption> availableBuffPool = new List<BuffOption>()
    {
        // --- Player stats ---
        new BuffOption(BuffType.MovementSpeed, BuffCategory.Character, "Haste", "Multiply movement speed by 15%", 0.15f),
        new BuffOption(BuffType.MaxHealth, BuffCategory.Character, "Extra Health", "Multiply maximum health by 10%", 0.10f),
        new BuffOption(BuffType.Healing, BuffCategory.Character, "Healing", "Restore health to 100%", 1f),
        new BuffOption(BuffType.Revive, BuffCategory.Character, "GRAVEBUSTER", "Return from death [Max 3 revives]", 1f),
        new BuffOption(BuffType.Unflinching, BuffCategory.Character, "GOLDEFENSIVE", "Take 15% less damage [Max 50%]", 0.20f),
        new BuffOption(BuffType.Vampirism, BuffCategory.Character, "VAMPIRISM", "50% of damage dealt is regained as HEALTH", 0.50f),

        // --- Weapon upgrades ---
        new BuffOption(BuffType.AttackDamage, BuffCategory.Gun, "Upper Caliber", "Increase bullet damage by 50%", 0.50f),
        new BuffOption(BuffType.FireRate, BuffCategory.Gun, "Barrelburner", "Increase firing rate by 25%", 0.25f),
        new BuffOption(BuffType.ExtraPellet, BuffCategory.Gun, "Multishot", "+1 Bullet per shot", 1f),
    };

    [System.Serializable]
    public class BuffCardDefinition
    {
        public BuffType buffType;
        public GameObject cardPrefab;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        ApplyCardDefinitions();

        // Canvas-based buff UI: the BuffSelectionUI component must be placed manually
        // in the scene (inside the Canvas hierarchy). Search for it and warn if forgotten.
        if (FindBuffUI() == null)
        {
            Debug.LogWarning("UI isn't found");
        }
        buffmenu.SetActive(true);
    }

    private void ApplyCardDefinitions()
    {
        if (cardDefinitions == null) return;

        foreach (BuffCardDefinition definition in cardDefinitions)
        {
            if (definition == null || definition.cardPrefab == null) continue;

            BuffOption option = availableBuffPool.Find(buff => buff.type == definition.buffType);
            if (option != null) option.cardPrefab = definition.cardPrefab;
        }
    }

  

    // Build the buff pool from the configured character and weapon upgrades.
    // Draw 3 random buffs. If guaranteeCategoryMix is enabled, at least 1
    // Character card and 1 Gun card are guaranteed to appear.
    public List<BuffOption> GetThreeRandomBuffs()
    {
        ApplyCardDefinitions();
        List<BuffOption> pool = new List<BuffOption>(availableBuffPool);
        List<BuffOption> result = new List<BuffOption>();

        if (guaranteeCategoryMix)
        {
            BuffOption character = TakeRandom(pool, b => b.category == BuffCategory.Character);
            if (character != null) result.Add(character);

            BuffOption gunBuff = TakeRandom(pool, b => b.category == BuffCategory.Gun);
            if (gunBuff != null) result.Add(gunBuff);
        }

        // Fill the remaining slots randomly from the available buff pool.
        while (result.Count < 3 && pool.Count > 0)
        {
            BuffOption pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            result.Add(pick);
            pool.Remove(pick);
        }

        return result;
    }

    // Draw one random option that passes the filter from the pool (and remove it).
    private BuffOption TakeRandom(List<BuffOption> pool, Func<BuffOption, bool> filter)
    {
        List<BuffOption> matches = pool.FindAll(b => filter(b));
        if (matches.Count == 0) return null;

        BuffOption pick = matches[UnityEngine.Random.Range(0, matches.Count)];
        pool.Remove(pick);
        return pick;
    }

    public void TriggerBuffSelection()
    {
        if (BuffSelectionUI.IsOpen) return; // currently choosing
        
        List<BuffOption> choices = GetThreeRandomBuffs();

        // Call the UI DIRECTLY (not via event) so it still works even when the
        // menu object is inactive (its Start() never runs).
        BuffSelectionUI ui = FindBuffUI();
        if (ui == null)
        {
            Debug.LogWarning("[BuffSystem] BuffSelectionUI not present in the scene — buff selection cancelled.");
            return;
        }

        OnBuffSelectionPrompt?.Invoke(choices);
        ui.ShowChoices(choices);
    }

    // Search the scene for a BuffSelectionUI component (including inactive ones). 
    private BuffSelectionUI FindBuffUI()
    {
        if (BuffSelectionUI.Instance != null) return BuffSelectionUI.Instance;

        // IncludeInactive = true: the menu is usually inactive at game start.
        // FindAnyObjectByType without that flag does not find inactive objects.
        return FindAnyObjectByType<BuffSelectionUI>(FindObjectsInactive.Include);
    }

    // Entry point for the buff menu: applies the chosen buff to the player
    // and then closes the selection screen. Called by BuffSelectionUI when a
    // card is clicked (or hotkeys 1/2/3 are pressed).

    public void SelectBuff(BuffOption option)
    {
        if (option == null) return;

        playerController player = FindPlayer();

        if (player == null)
        {
            Debug.LogWarning("[BuffSystem] No playerController in the scene — buff discarded.");
            FindBuffUI()?.HideMenu();
            OnBuffSelectionClosed?.Invoke();
            return;
        }

        ApplyBuff(option, player);
        FindBuffUI()?.HideMenu();
        OnBuffSelectionClosed?.Invoke();
    }

    private playerController FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            return FindAnyObjectByType<playerController>();
        }
        return playerObj.GetComponent<playerController>();
    }

    /// "current -> new" preview text displayed on the menu card before picking.
    public string GetStatPreviewText(BuffOption buff)
    {
        if (buff == null) return string.Empty;

        playerController player = FindPlayer();
        charStats stats = player != null ? player.Stats : null;
        gun currentGun = player != null ? player.CurrentGun : null;
        WeaponStats weapon = currentGun != null ? currentGun.Stats : null;

        switch (buff.type)
        {
            case BuffType.MovementSpeed:
                return stats != null ? $"Speed {stats.speed:0.0} -> {stats.speed * (1f + buff.value):0.0}" : string.Empty;

            case BuffType.MaxHealth:
                return stats != null ? $"Max HP {stats.maxHealth:0} -> {stats.maxHealth * (1f + buff.value):0}" : string.Empty;

            case BuffType.Healing:
                return stats != null ? $"HP {stats.currentHealth:0} -> {stats.maxHealth:0}" : string.Empty;

            case BuffType.Revive:
                return stats != null ? $"Revive charges +{(int)buff.value} (max 3)" : string.Empty;

            case BuffType.Unflinching:
                return stats != null ? $"Damage taken -{buff.value * 100f:0}%" : string.Empty;

            case BuffType.Vampirism:
                return stats != null ? $"Lifesteal {stats.VampirismPercent * 100f:0}% -> {(stats.VampirismPercent + buff.value) * 100f:0}%" : string.Empty;

            case BuffType.AttackDamage:
                return weapon != null ? $"DMG {weapon.bulletDamage:0.0} -> {weapon.bulletDamage * (1f + buff.value):0.0}" : string.Empty;

            case BuffType.FireRate:
                return weapon != null ? $"Rate {weapon.fireRate:0.00}s -> {Mathf.Max(0.05f, weapon.fireRate * (1f - buff.value)):0.00}s" : string.Empty;

            case BuffType.KnockbackForce:
                return weapon != null ? $"Knockback {weapon.knockbackForce:0.0} -> {weapon.knockbackForce * (1f + buff.value):0.0}" : string.Empty;

            case BuffType.BulletSpeed:
                return weapon != null ? $"Bullet Spd {weapon.bulletSpeed:0.0} -> {weapon.bulletSpeed * (1f + buff.value):0.0}" : string.Empty;

            case BuffType.Accuracy:
                return weapon != null ? $"Spread {weapon.spreadAngle:0.0}° -> {weapon.spreadAngle * (1f - buff.value):0.0}°" : string.Empty;

            case BuffType.PierceCount:
                return weapon != null ? $"Pierce {weapon.pierceCount:0} -> {weapon.pierceCount + (int)buff.value:0}" : string.Empty;

            case BuffType.ExtraPellet:
                return weapon != null ? $"Pellets {weapon.pelletsCount:0} -> {weapon.pelletsCount + (int)buff.value:0}" : string.Empty;

            case BuffType.AmmoCapacity:
                return weapon != null ? $"Mag {weapon.maxAmmo:0} -> {Mathf.RoundToInt(weapon.maxAmmo * (1f + buff.value)):0} + refill" : string.Empty;

        }

        return string.Empty;
    }

    public void ApplyBuff(BuffOption buff, playerController player)
    {
        if (player == null || buff == null) return;

        charStats stats = player.Stats;
        gun currentGun = player.CurrentGun;

        switch (buff.type)
        {
            case BuffType.MovementSpeed:
                if (stats != null) stats.speed *= (1f + buff.value);
                break;

            case BuffType.AttackDamage:
                if (currentGun != null && currentGun.Stats != null)
                {
                    currentGun.Stats.bulletDamage *= (1f + buff.value);
                }
                break;

            case BuffType.MaxHealth:
                if (stats != null)
                {
                    float ratio = stats.currentHealth / stats.maxHealth; // preserve current health percentage
                    stats.maxHealth *= 1f + buff.value;
                    stats.currentHealth = stats.maxHealth * ratio; // adjust current health to maintain the same percentage
                }
                break;

            case BuffType.Healing:
                if (stats != null)
                {
                    stats.Heal(stats.maxHealth);
                }
                break;

            case BuffType.Revive:
                if (stats != null)
                {
                    stats.AddReviveCharges(Mathf.RoundToInt(buff.value));
                }
                break;

            case BuffType.Unflinching:
                if (stats != null)
                {
                    stats.AddDamageReduction(buff.value);
                }
                break;

            case BuffType.Vampirism:
                if (stats != null)
                {
                    stats.AddVampirism(buff.value);
                }
                break;

            case BuffType.FireRate:
                if (currentGun != null && currentGun.Stats != null)
                {
                    currentGun.Stats.fireRate = Mathf.Max(0.05f, currentGun.Stats.fireRate * (1f - buff.value));
                }
                break;

            case BuffType.KnockbackForce:
                if (currentGun != null && currentGun.Stats != null)
                {
                    currentGun.Stats.knockbackForce *= (1f + buff.value);
                }
                break;

            case BuffType.BulletSpeed:
                if (currentGun != null && currentGun.Stats != null)
                {
                    currentGun.Stats.bulletSpeed *= (1f + buff.value);
                }
                break;

            case BuffType.Accuracy:
                if (currentGun != null && currentGun.Stats != null)
                {
                    // Smaller spread = more accurate (minimum 0°)
                    currentGun.Stats.spreadAngle = Mathf.Max(0f, currentGun.Stats.spreadAngle * (1f - buff.value));
                }
                break;

            case BuffType.PierceCount:
                if (currentGun != null && currentGun.Stats != null)
                {
                    currentGun.Stats.pierceCount += (int)buff.value;
                }
                break;

            case BuffType.ExtraPellet:
                if (currentGun != null && currentGun.Stats != null)
                {
                    currentGun.Stats.pelletsCount += (int)buff.value;
                    currentGun.Stats.spreadAngle += 15;
                }
                break;

            case BuffType.AmmoCapacity:
                if (currentGun != null && currentGun.Stats != null)
                {
                    currentGun.Stats.maxAmmo = Mathf.Max(1, Mathf.RoundToInt(currentGun.Stats.maxAmmo * (1f + buff.value)));
                    currentGun.RefillAmmo(); // immediately refilled to the new capacity
                }
                break;

        }

        OnBuffSelected?.Invoke(buff);
        Debug.Log($"Applied Buff: {buff.title} ({buff.description})");
    }
}
