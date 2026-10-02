using UnityEngine;

public class WeaponStats : MonoBehaviour
{
    [Header("General Info")]
    [Tooltip("Weapon name displayed on the HUD / logs")]
    public string weaponName = "Pistol";

    [Header("Firing Parameters")]
    public float fireRate = 0.25f;
    public bool autoFire = false;
    public float bulletSpeed = 16f;
    public float bulletDamage = 15f;

    [Header("Spread & Multi-Pellet")]
    [Tooltip("Number of projectiles fired per shot (e.g. Shotgun 6-8)")]
    public int pelletsCount;
    [Tooltip("Bullet spread angle in degrees")]
    public float spreadAngle ;

    [Header("Tactical & On-Hit Effects")]
    [Tooltip("Kinematic impulse applied when striking targets (Shotgun heavy bump)")]
    public float knockbackForce ;
    [Tooltip("Number of targets a single bullet can penetrate (Rifle penetration)")]
    public int pierceCount;

    [Header("Ammunition")]
    [Tooltip("Number of the ammunition")]
    public int maxAmmo;
}
