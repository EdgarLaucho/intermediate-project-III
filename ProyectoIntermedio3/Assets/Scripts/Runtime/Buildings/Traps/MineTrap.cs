using UnityEngine;
using System.Collections.Generic;

public class MineTrap : Trap
{
    #region Properties

    public int ExplosionRadius { get; private set; }

    #endregion

    #region Lifecycle

    public override void Initialize(BuildingData data)
    {
        base.Initialize(data);

        if (data is TrapData td)
        {
            ExplosionRadius = Mathf.Max(1, td.trapStats.effectRadius);
        }
    }

    #endregion

    #region Trap Virtual Overrides

    public override int GetCollectionRadius() => ExplosionRadius;

    public override void OnTriggered(IEnumerable<ITargetable> targets)
    {
        PlayActivationFeedback();
        ApplyToTargets(targets);
        Debug.Log($"[MineTrap] Detonated at {transform.position}. Remaining uses: {RemainingUses}");
    }

    #endregion

    #region Upgrade Handling

    protected override void ApplyUpgradeStats(UpgradeLevelData upgradeData)
    {
        var mult = upgradeData.statMultiplier > 0f ? upgradeData.statMultiplier : 1f;

        TriggerDamage = Mathf.RoundToInt(TriggerDamage * mult);
        ExplosionRadius = Mathf.Max(1, ExplosionRadius + upgradeData.bonusRange);

        MaxUses += upgradeData.bonusTrapUses;
        RemainingUses += upgradeData.bonusTrapUses;
    }

    #endregion
}