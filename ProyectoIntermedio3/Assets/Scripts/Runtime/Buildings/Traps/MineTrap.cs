using UnityEngine;
using System.Collections.Generic;

// Mine variant of Trap. Stays dormant until an enemy steps on its cell, then
// detonates dealing damage in a configurable explosion radius (larger than the
// trap-cell footprint used to detect the enemy stepping on it).
//
// Uses the shared TrapTrigger component — same as Spikes and Tar.
// MineTrap only overrides two virtual hooks:
//   GetCollectionRadius() → returns ExplosionRadius for the AOE blast.
//   OnTriggered()         → calls base (damage + consume use) and logs the explosion.
//
// Upgrade path (configure in TrapData asset):
//   Level 1 (base): 1 use,  small explosion radius, base damage
//   Level 2       : statMultiplier scales damage + AOE radius, bonusRange extends it
//   Level 3 (max) : bonusTrapUses +1 gives a second charge; larger damage/radius
public sealed class MineTrap : Trap
{
    #region Properties

    // AOE explosion radius in grid cells (Chebyshev). Activation is still only the mine cell.
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

    // TrapTrigger uses this to widen the collection sweep from cell-size to AOE.
    public override int GetCollectionRadius() => ExplosionRadius;

    // Called by TrapTrigger after collecting all targets in ExplosionRadius.
    public override void OnTriggered(IEnumerable<ITargetable> targets)
    {
        PlayActivationFeedback(true, true);
        ApplyToTargets(targets);
        Debug.Log($"[MineTrap] Detonated at {transform.position}. Remaining uses: {RemainingUses}");
    }

    #endregion

    #region Upgrade Handling

    protected override void ApplyUpgradeStats(UpgradeLevelData upgradeData)
    {
        float mult = upgradeData.statMultiplier > 0f ? upgradeData.statMultiplier : 1f;

        // Scale damage, but grow cell radius only through explicit bonusRange.
        // Multiplying integer cell ranges makes upgrades balloon too quickly.
        TriggerDamage = Mathf.RoundToInt(TriggerDamage * mult);
        ExplosionRadius = Mathf.Max(1, ExplosionRadius + upgradeData.bonusRange);

        MaxUses += upgradeData.bonusTrapUses;
        RemainingUses += upgradeData.bonusTrapUses;
    }

    #endregion
}