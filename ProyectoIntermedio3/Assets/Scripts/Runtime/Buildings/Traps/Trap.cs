using UnityEngine;
using System.Collections.Generic;

// Trap is a placed structure that damages (and optionally slows) enemies that walk
// over it. TrapTrigger handles the physics polling; Trap owns the state and rules.
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

    private static Material _fallbackParticleMaterial;
    private static AudioClip _fallbackActivationSound;

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
    }

    internal void SetRuntimeTargetMask(LayerMask targetMask)
    {
        RuntimeTargetMask = targetMask;
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
            TakeDamage(MaxHealth); // Force death; lets BuildingBase handle cleanup.

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

    // Cell radius used by TrapTrigger to collect targets when the trap fires.
    // Base traps affect only their own cell. Mine overrides this with ExplosionRadius.
    public virtual int GetCollectionRadius() => 0;

    // Entry point called by TrapTrigger once an enemy is detected on the cell.
    // Default: apply damage + slow to all targets in the collection radius.
    // Override in subclasses to add custom effects (e.g. explosion log, VFX).
    public virtual void OnTriggered(IEnumerable<ITargetable> targets)
    {
        PlayActivationFeedback();
        ApplyToTargets(targets);
    }

    #endregion

    #region Upgrade Handling

    protected override void ApplyUpgradeStats(UpgradeLevelData upgradeData)
    {
        float statMultiplier = upgradeData.statMultiplier > 0f ? upgradeData.statMultiplier : 1f;
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

    protected void PlayActivationFeedback(bool useFallbackVisual = false, bool useFallbackSound = false)
    {
        if (activationVfxPrefab != null)
        {
            ParticleSystem vfx = Instantiate(activationVfxPrefab, transform.position, activationVfxPrefab.transform.rotation);
            vfx.Play(true);
            Destroy(vfx.gameObject, Mathf.Max(0.1f, activationVfxLifetime));
        }
        else if (useFallbackVisual)
        {
            PlayFallbackActivationVfx(transform.position);
        }

        if (activationSound != null)
        {
            AudioSource.PlayClipAtPoint(activationSound, transform.position, activationSoundVolume);
        }
        else if (useFallbackSound)
        {
            AudioSource.PlayClipAtPoint(GetFallbackActivationSound(), transform.position, activationSoundVolume);
        }
    }

    private static void PlayFallbackActivationVfx(Vector3 position)
    {
        GameObject vfxObject = new GameObject("__TrapActivationVfx");
        vfxObject.transform.position = position + Vector3.up * 0.08f;

        ParticleSystem particles = vfxObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.35f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.76f, 0.16f, 0.95f),
            new Color(1f, 0.18f, 0.05f, 0.70f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 48) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetFallbackParticleMaterial();

        particles.Play(true);
        Destroy(vfxObject, 1.25f);
    }

    private static Material GetFallbackParticleMaterial()
    {
        if (_fallbackParticleMaterial != null) return _fallbackParticleMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");

        _fallbackParticleMaterial = new Material(shader) { name = "TrapFallbackExplosion" };
        return _fallbackParticleMaterial;
    }

    private static AudioClip GetFallbackActivationSound()
    {
        if (_fallbackActivationSound != null) return _fallbackActivationSound;

        const int sampleRate = 22050;
        const float duration = 0.42f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        uint noiseState = 0x6D2B79F5u;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            noiseState = noiseState * 1664525u + 1013904223u;
            float noise = ((noiseState >> 8) / 16777215f) * 2f - 1f;
            float lowBoom = Mathf.Sin(2f * Mathf.PI * (86f - 38f * t) * (i / (float)sampleRate));
            float envelope = Mathf.Pow(1f - t, 2.35f);
            samples[i] = (noise * 0.58f + lowBoom * 0.42f) * envelope * 0.55f;
        }

        _fallbackActivationSound = AudioClip.Create("TrapFallbackExplosion", sampleCount, 1, sampleRate, false);
        _fallbackActivationSound.SetData(samples, 0);
        return _fallbackActivationSound;
    }

    #endregion
}