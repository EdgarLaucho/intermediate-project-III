using UnityEngine;

public enum TowerRole
{
    Missil,
    Cannon,
    Ice,
}

public enum DamageKind
{
    Physical,
    Siege,
    Frost,
    Pierce,
}

[System.Serializable]
public struct TowerStatsData
{
    public float attackRange;
    public int attackDamage;
    public float fireRate;
    public float projectileSpeed;
    public int projectilesPerAttack;
    public float splashRadius;
    [Range(0f, 1f)] public float slowPercent;
    public float slowDuration;

    public bool HasValidValues => attackRange > 0f && attackDamage > 0 && fireRate > 0f;
}

[CreateAssetMenu(fileName = "NewTowerData", menuName = "TD/Building Data/Tower")]
public class TowerData : BuildingData
{
    public override BuildingCategory Category => BuildingCategory.Tower;

    [Header("Tower Stats")]
    public TowerRole role = TowerRole.Missil;
    public DamageKind damageKind = DamageKind.Physical;
    public TowerStatsData towerStats = new TowerStatsData
    {
        attackRange = 5f,
        attackDamage = 10,
        fireRate = 1f,
        projectileSpeed = 12f,
        projectilesPerAttack = 1,
        splashRadius = 0f,
        slowPercent = 0f,
        slowDuration = 0f,
    };
}
