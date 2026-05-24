using UnityEngine;

public class Wall : BuildingBase
{
    #region Wall Stats

    public WallRole Role { get; private set; }
    public int Armor { get; private set; }
    public int ThornsDamage { get; private set; }

    #endregion

    #region Lifecycle

    public override void Initialize(BuildingData data)
    {
        base.Initialize(data);

        if (data is WallData wallData)
        {
            Role = wallData.role;
            Armor = wallData.armor;
            ThornsDamage = wallData.thornsDamage;
        }
    }

    #endregion

    #region Actions

    public override void TakeDamage(int amount)
    {
        base.TakeDamage(Mathf.Max(1, amount - Armor));
    }

    public void TakeContactHit(IDamageable attacker, int amount)
    {
        TakeDamage(amount);

        if (attacker == null || !attacker.IsAlive) return;
        if (ThornsDamage <= 0 || Role != WallRole.Thorned) return;
        if (ReferenceEquals(attacker, this)) return;

        attacker.TakeDamage(ThornsDamage);
    }

    #endregion

    #region Upgrade Handling

    protected override void ApplyUpgradeStats(UpgradeLevelData upgradeData)
    {
        Armor += upgradeData.bonusArmor;
        ThornsDamage += upgradeData.bonusThornsDamage;
    }

    #endregion
}