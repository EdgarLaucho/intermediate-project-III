using UnityEngine;

public class Tower : BuildingBase
{
    #region Combat Stats

    public TowerRole Role { get; private set; }
    public DamageKind DamageKind { get; private set; }
    public int AttackRange { get; private set; }
    public int AttackDamage { get; private set; }
    public float FireRate { get; private set; }
    public float ProjectileSpeed { get; private set; }
    public int ProjectilesPerAttack { get; private set; } = 1;
    public int SplashRadius { get; private set; }
    public float SlowPercent { get; private set; }
    public float SlowDuration { get; private set; }

    #endregion

    #region Upgrade Preview

    public bool TryPreviewNextUpgrade(out int nextDamage, out int nextRange, out float nextFireRate)
    {
        nextDamage = AttackDamage;
        nextRange = AttackRange;
        nextFireRate = FireRate;

        if (IsMaxLevel || Data == null || Data.upgradeLevels == null || CurrentLevel >= Data.upgradeLevels.Length)
            return false;

        UpgradeLevelData upgradeData = Data.upgradeLevels[CurrentLevel];
        float statMultiplier = NormalizedMultiplier(upgradeData.statMultiplier);
        float fireRateMultiplier = NormalizedMultiplier(upgradeData.fireRateMultiplier);
        nextDamage = Mathf.RoundToInt(AttackDamage * statMultiplier);
        nextRange = Mathf.Max(1, AttackRange + upgradeData.bonusRange);
        nextFireRate = FireRate * fireRateMultiplier;
        return true;
    }

    #endregion

    #region Lifecycle

    public override void Initialize(BuildingData data)
    {
        base.Initialize(data);

        if (data is TowerData td && td.towerStats.HasValidValues)
        {
            Role = td.role;
            DamageKind = td.damageKind;
            AttackRange = td.towerStats.attackRange;
            AttackDamage = td.towerStats.attackDamage;
            FireRate = td.towerStats.fireRate;
            ProjectileSpeed = td.towerStats.projectileSpeed;
            ProjectilesPerAttack = Mathf.Max(1, td.towerStats.projectilesPerAttack);
            SplashRadius = td.towerStats.splashRadius;
            SlowPercent = td.towerStats.slowPercent;
            SlowDuration = td.towerStats.slowDuration;
        }
    }

    #endregion

    #region Upgrade Handling

    protected override void ApplyUpgradeStats(UpgradeLevelData upgradeData)
    {
        float statMultiplier = NormalizedMultiplier(upgradeData.statMultiplier);
        AttackDamage = Mathf.RoundToInt(AttackDamage * statMultiplier);
        AttackRange = Mathf.Max(1, AttackRange + upgradeData.bonusRange);
        FireRate = FireRate * NormalizedMultiplier(upgradeData.fireRateMultiplier);
        ProjectilesPerAttack = Mathf.Max(1, ProjectilesPerAttack + upgradeData.bonusProjectilesPerAttack);
    }

    #endregion

    #region Private Helpers

    private static float NormalizedMultiplier(float multiplier) => multiplier > 0f ? multiplier : 1f;

    #endregion
}