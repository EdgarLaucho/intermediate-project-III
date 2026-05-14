using UnityEngine;

[CreateAssetMenu(menuName = "TD/Weapon")]
public class WeaponData : ScriptableObject
{
    [Header("Info")]
    public string weaponName;

    [Header("Stats")]
    public int damage = 1;
    public float attackRate = 1f;
    public float attackRange = 6f;

    [Header("Projectile")]
    public Projectile projectilePrefab;

    //[Header("Visual")]
    //public Color projectileColor = Color.white;
}