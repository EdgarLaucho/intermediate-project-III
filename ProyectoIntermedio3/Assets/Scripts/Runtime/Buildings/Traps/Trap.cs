using UnityEngine;
using System.Collections.Generic;

// Trap is a placed structure that damages (and optionally slows) enemies that walk
// over it. TrapTrigger handles the physics polling; Trap owns the state and rules.
public class Trap : BuildingBase
{
    #region Trap Stats

    public TrapRole Role { get; private set; }
    public int TriggerDamage { get; private set; }
    public float Cooldown { get; private set; }
    public int MaxUses { get; private set; }
    public int RemainingUses { get; private set; }
    public float TriggerRadius { get; private set; }
    public float EffectDuration { get; private set; }
    public float SlowPercent { get; private set; }

    #endregion

    #region Lifecycle

    public override void Initialize(BuildingData data)
    {
        base.Initialize(data);

        if (data is TrapData td && td.trapStats.HasValidValues)
        {
            Role = td.role;
            TriggerDamage = td.trapStats.triggerDamage;
            Cooldown = td.trapStats.cooldown;
            MaxUses = td.trapStats.maxUses;
            RemainingUses = MaxUses;
            TriggerRadius = td.trapStats.triggerRadius;
            EffectDuration = td.trapStats.effectDuration;
            SlowPercent = td.trapStats.slowPercent;
        }
    }

    #endregion

    #region Actions

    // Decrements uses and self-destructs when the last use is consumed.
    // Returns false if the trap is already spent or dead so callers can bail early.
    public bool TryConsumeUse()
    {
        if (!IsAlive || RemainingUses <= 0) return false;

        RemainingUses--;
        if (RemainingUses <= 0)
            TakeDamage(MaxHealth);  // Force death; lets BuildingBase handle cleanup.

        return true;
    }

    // Iterates the provided target list and applies damage + slow to each live target,
    // then consumes one use if at least one target was hit.
    public void ApplyToTargets(IEnumerable<ITargetable> targets)
    {
        if (targets == null || !IsAlive || RemainingUses <= 0) return;

        bool hitAnyTarget = false;
        foreach (ITargetable target in targets)
        {
            if (target == null || !target.IsAlive) continue;
            hitAnyTarget = true;
            if (TriggerDamage > 0) target.TakeDamage(TriggerDamage);
            if (target.IsAlive) ApplySlow(target);
        }

        if (hitAnyTarget)
            TryConsumeUse();
    }

    #endregion

    #region Upgrade Handling

    protected override void ApplyUpgradeStats(UpgradeLevelData upgradeData)
    {
        float statMultiplier = upgradeData.statMultiplier > 0f ? upgradeData.statMultiplier : 1f;
        TriggerDamage = Mathf.RoundToInt(TriggerDamage * statMultiplier);
        TriggerRadius = TriggerRadius * statMultiplier + upgradeData.bonusRange;
        MaxUses += upgradeData.bonusTrapUses;
        RemainingUses += upgradeData.bonusTrapUses;
    }

    #endregion

    #region Private Helpers

    private void ApplySlow(ITargetable target)
    {
        if (SlowPercent <= 0f || EffectDuration <= 0f) return;
        if (target is ISlowable slowable)
            slowable.ApplySlow(SlowPercent, EffectDuration);
    }

    #endregion
}
