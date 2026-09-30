using System;
using System.Collections.Generic;
using UnityEngine;

public enum BuffType
{
    MovementSpeed,
    AttackDamage,
    MaxHealth,
    FireRate,
    KnockbackForce
}

[System.Serializable]
public class BuffOption
{
    public BuffType type;
    public string title;
    public string description;
    public float value;

    public BuffOption(BuffType type, string title, string description, float value)
    {
        this.type = type;
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

    private readonly List<BuffOption> availableBuffPool = new List<BuffOption>()
    {
        new BuffOption(BuffType.MovementSpeed, "Adrenaline Rush", "+20% Movement Speed", 0.20f),
        new BuffOption(BuffType.AttackDamage, "Hollow Points", "+25% Bullet Damage", 0.25f),
        new BuffOption(BuffType.MaxHealth, "Reinforced Armor", "+30 Max Health & Full Heal", 30f),
        new BuffOption(BuffType.FireRate, "Hair Trigger", "+20% Faster Fire Rate", 0.20f),
        new BuffOption(BuffType.KnockbackForce, "Heavy Kinetic Slug", "+50% Knockback / Crowd Control", 0.50f)
    };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Retrieves 3 random, unique buffs from the available pool.
    /// </summary>
    public List<BuffOption> GetThreeRandomBuffs()
    {
        List<BuffOption> copy = new List<BuffOption>(availableBuffPool);
        List<BuffOption> result = new List<BuffOption>();

        int count = Mathf.Min(3, copy.Count);
        for (int i = 0; i < count; i++)
        {
            int randIndex = UnityEngine.Random.Range(0, copy.Count);
            result.Add(copy[randIndex]);
            copy.RemoveAt(randIndex);
        }

        return result;
    }

    public void TriggerBuffSelection()
    {
        List<BuffOption> choices = GetThreeRandomBuffs();
        OnBuffSelectionPrompt?.Invoke(choices);
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
                if (currentGun != null && currentGun.CurrentWeaponData != null)
                {
                    currentGun.CurrentWeaponData.bulletDamage *= (1f + buff.value);
                }
                break;

            case BuffType.MaxHealth:
                if (stats != null)
                {
                    stats.maxHealth += buff.value;
                    stats.Heal(stats.maxHealth);
                }
                break;

            case BuffType.FireRate:
                if (currentGun != null && currentGun.CurrentWeaponData != null)
                {
                    currentGun.CurrentWeaponData.fireRate = Mathf.Max(0.05f, currentGun.CurrentWeaponData.fireRate * (1f - buff.value));
                }
                break;

            case BuffType.KnockbackForce:
                if (currentGun != null && currentGun.CurrentWeaponData != null)
                {
                    currentGun.CurrentWeaponData.knockbackForce *= (1f + buff.value);
                }
                break;
        }

        OnBuffSelected?.Invoke(buff);
        Debug.Log($"Applied Buff: {buff.title} ({buff.description})");
    }
}
