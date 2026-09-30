using UnityEngine;

public enum WeaponType
{
    Pistol,
    Shotgun,
    RapidSMG,
    Rifle
}

[CreateAssetMenu(fileName = "NewWeaponData", menuName = "ZenZational/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("General Info")]
    public string weaponName = "Pistol";
    public WeaponType weaponType = WeaponType.Pistol;
    public Sprite weaponSprite;

    [Header("Firing Parameters")]
    public float fireRate = 0.25f;
    public bool autoFire = false;
    public float bulletSpeed = 16f;
    public float bulletDamage = 15f;

    [Header("Spread & Multi-Pellet")]
    [Tooltip("Number of projectiles fired per shot (e.g. Shotgun 6-8)")]
    public int pelletsCount = 1;
    [Tooltip("Bullet spread angle in degrees")]
    public float spreadAngle = 0f;

    [Header("Tactical & On-Hit Effects")]
    [Tooltip("Kinematic impulse applied when striking targets (Shotgun heavy bump)")]
    public float knockbackForce = 2f;
    [Tooltip("Number of targets a single bullet can penetrate (Rifle penetration)")]
    public int pierceCount = 0;

    // [Header("Ammunition")] public int maxAmmo = 30;

    /// <summary>
    /// Factory helper to generate default weapon preset data at runtime if no ScriptableObject asset is assigned.
    /// </summary>
    public static WeaponData CreateDefaultPreset(WeaponType type)
    {
        WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
        data.weaponType = type;

        switch (type)
        {
            case WeaponType.Pistol:
                data.weaponName = "Pistol (Starter)";
                data.fireRate = 0.25f;
                data.autoFire = false;
                data.bulletSpeed = 16f;
                data.bulletDamage = 15f;
                data.pelletsCount = 1;
                data.spreadAngle = 2f;
                data.knockbackForce = 1.5f;
                data.pierceCount = 0;
                // data.maxAmmo = 30;
                break;

            case WeaponType.Shotgun:
                data.weaponName = "Shotgun";
                data.fireRate = 0.65f;
                data.autoFire = false;
                data.bulletSpeed = 14f;
                data.bulletDamage = 12f; // Per pellet (6 pellets = 72 max total damage)
                data.pelletsCount = 6;
                data.spreadAngle = 25f;
                data.knockbackForce = 10f; // Heavy bump / knockback
                data.pierceCount = 0;
                // data.maxAmmo = 16;
                break;

            case WeaponType.RapidSMG:
                data.weaponName = "Rapid SMG";
                data.fireRate = 0.1f;
                data.autoFire = true;
                data.bulletSpeed = 18f;
                data.bulletDamage = 7f;
                data.pelletsCount = 1;
                data.spreadAngle = 8f;
                data.knockbackForce = 0.8f;
                data.pierceCount = 0;
                // data.maxAmmo = 60;
                break;

            case WeaponType.Rifle:
                data.weaponName = "Rifle Piercer";
                data.fireRate = 0.4f;
                data.autoFire = true;
                data.bulletSpeed = 22f;
                data.bulletDamage = 35f;
                data.pelletsCount = 1;
                data.spreadAngle = 1f;
                data.knockbackForce = 3.5f;
                data.pierceCount = 3; // Penetrates through up to 3 enemies
                // data.maxAmmo = 24;
                break;
        }

        return data;
    }
}
