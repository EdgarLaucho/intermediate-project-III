using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Trap : BuildingBase
{
    #region Inspector Fields

    [Header("Activation Feedback")]
    [SerializeField] private ParticleSystem activationVfxPrefab;
    [SerializeField] private AudioClip activationSound;
    [SerializeField, Range(0f, 1f)] private float activationSoundVolume = 1f;
    [SerializeField] private float activationVfxLifetime = 4f;

    #endregion

    #region Trap Stats

    public TrapRole Role { get; private set; }
    public int TriggerDamage { get; protected set; }
    public float Cooldown { get; private set; }
    public int MaxUses { get; protected set; }
    public int RemainingUses { get; protected set; }
    public float EffectDuration { get; protected set; }
    public float SlowPercent { get; protected set; }

    protected LayerMask RuntimeTargetMask { get; private set; } = ~0;

    private SpikeTrapAnimator _spikeAnimator;
    private bool _spentWaitingForAnimation;
    private Coroutine _spikeDamageRoutine;
    private readonly List<ITargetable> _pendingSpikeTargets = new();

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
            EffectDuration = td.trapStats.effectDuration;
            SlowPercent = td.trapStats.slowPercent;
        }

        EnsureSpikeAnimator();
    }

    internal void SetRuntimeTargetMask(LayerMask targetMask)
    {
        RuntimeTargetMask = targetMask;
    }

    #endregion

    #region Actions

    public bool TryConsumeUse()
    {
        if (!IsAlive || RemainingUses <= 0) return false;

        RemainingUses--;
        if (RemainingUses <= 0)
        {
            if (Role == TrapRole.Spikes && _spikeAnimator != null)
            {
                _spentWaitingForAnimation = true;
                Invoke(nameof(KillSpentTrap), Mathf.Max(0.05f, _spikeAnimator.RemainingDurationAfterHit));
            }
            else
            {
                TakeDamage(MaxHealth);
            }
        }

        return true;
    }

    public void ApplyToTargets(IEnumerable<ITargetable> targets)
    {
        if (targets == null || !IsAlive || RemainingUses <= 0) return;

        var hitAnyTarget = false;
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

    public virtual int GetCollectionRadius() => 0;

    public virtual void OnTriggered(IEnumerable<ITargetable> targets)
    {
        if (_spentWaitingForAnimation || RemainingUses <= 0) return;

        if (Role == TrapRole.Spikes && _spikeAnimator != null)
        {
            TriggerSpikeAttack(targets);
            return;
        }

        PlayActivationFeedback();
        ApplyToTargets(targets);
    }

    #endregion

    #region Upgrade Handling

    protected override void ApplyUpgradeStats(UpgradeLevelData upgradeData)
    {
        var statMultiplier = upgradeData.statMultiplier > 0f ? upgradeData.statMultiplier : 1f;
        TriggerDamage = Mathf.RoundToInt(TriggerDamage * statMultiplier);
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

    private void EnsureSpikeAnimator()
    {
        if (Role != TrapRole.Spikes)
            return;

        if (!TryGetComponent(out _spikeAnimator))
            _spikeAnimator = gameObject.AddComponent<SpikeTrapAnimator>();

        _spikeAnimator.SyncToAttackRate(Cooldown);
        _spikeAnimator.Initialize();
    }

    private void TriggerSpikeAttack(IEnumerable<ITargetable> targets)
    {
        if (_spikeDamageRoutine != null) return;

        _pendingSpikeTargets.Clear();
        if (targets != null)
        {
            foreach (ITargetable target in targets)
            {
                if (target != null && target.IsAlive)
                    _pendingSpikeTargets.Add(target);
            }
        }

        if (_pendingSpikeTargets.Count == 0) return;

        _spikeAnimator.Play();
        _spikeDamageRoutine = StartCoroutine(ApplySpikeDamageAtHitFrame());
    }

    private IEnumerator ApplySpikeDamageAtHitFrame()
    {
        var elapsed = 0f;
        var hitDelay = _spikeAnimator != null ? _spikeAnimator.HitDelay : 0f;

        while (elapsed < hitDelay)
        {
            if (!GamePauseEvents.IsPaused)
                elapsed += Time.deltaTime;

            yield return null;
        }

        if (IsAlive && !_spentWaitingForAnimation)
        {
            PlayActivationFeedback();
            ApplyToTargets(_pendingSpikeTargets);
        }

        _pendingSpikeTargets.Clear();
        _spikeDamageRoutine = null;
    }

    private void KillSpentTrap()
    {
        if (IsAlive)
            TakeDamage(MaxHealth);
    }

    protected void PlayActivationFeedback()
    {
        if (activationVfxPrefab != null)
        {
            var vfx = Instantiate(activationVfxPrefab, transform.position, activationVfxPrefab.transform.rotation);
            vfx.Play(true);
            Destroy(vfx.gameObject, Mathf.Max(0.1f, activationVfxLifetime));
        }

        if (activationSound != null)
        {
            AudioSource.PlayClipAtPoint(activationSound, transform.position, activationSoundVolume);
        }
    }

    #endregion
}