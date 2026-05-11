using UnityEngine;
using System;

// Minimal enemy implementation used during development while the full enemy system
// is not yet in place. Satisfies ITargetable (so towers can attack it) and ISlowable
// (so traps can debuff it). All stats come from an EnemyData ScriptableObject so
// this stub never needs to be edited when tuning values.
public class EnemyStub : MonoBehaviour, ITargetable, ISlowable
{
    #region Inspector Fields

    [Header("Configuration")]
    [SerializeField] private EnemyData data;

    #endregion

    #region IDamageable / ITargetable Properties

    [field: SerializeField, ReadOnly]
    public int  CurrentHealth { get; private set; }
    public int  MaxHealth     { get; private set; }
    public bool IsAlive       => CurrentHealth > 0;

    // Damage this enemy deals on contact with a building (read by collision handlers).
    public int ContactDamage => data != null ? data.baseDamage : 0;

    #endregion

    #region ISlowable Properties

    // Returns a [0, 1] multiplier: 1 = full speed, 0 = fully stopped.
    // Once the slow expires the multiplier snaps back to 1 automatically in Update.
    public float MoveSpeedMultiplier => Time.time < _slowEndTime ? 1f - _strongestSlow : 1f;

    // Convenience: actual units/second after the slow is factored in.
    public float EffectiveMoveSpeed  => data != null ? data.moveSpeed * MoveSpeedMultiplier : 0f;

    #endregion

    #region IDamageable Events

    public event Action<IDamageable> OnDeath;
    public event Action OnHealthChanged;

    #endregion

    #region Private State

    // Only the strongest active slow is kept. Overlapping slows extend the
    // duration but never stack multiplicatively — this keeps the math simple
    // and prevents enemies from being permanently frozen by rapid trap hits.
    private float _strongestSlow;
    private float _slowEndTime;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        if (data == null) { Debug.LogError("[EnemyStub] EnemyData not assigned."); return; }
        MaxHealth = CurrentHealth = data.baseHealth;
    }

    private void Update()
    {
        // Clear the cached slow once its timer expires so MoveSpeedMultiplier
        // returns 1 without needing a time comparison in the property every frame.
        if (_strongestSlow > 0f && Time.time >= _slowEndTime)
            _strongestSlow = 0f;
    }

    #endregion

    #region IDamageable Methods

    public void TakeDamage(int amount)
    {
        if (!IsAlive) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke();
        if (!IsAlive) Die();
    }

    // Healing is intentionally a no-op for enemies in this stub.
    public void Heal(int amount) { }

    #endregion

    #region ISlowable Methods

    public void ApplySlow(float slowPercent, float duration)
    {
        if (!IsAlive || slowPercent <= 0f || duration <= 0f) return;

        // Take whichever is stronger / lasts longer — never weaken an existing slow.
        _strongestSlow = Mathf.Max(_strongestSlow, Mathf.Clamp01(slowPercent));
        _slowEndTime   = Mathf.Max(_slowEndTime,   Time.time + duration);
    }

    #endregion

    #region Private Helpers

    private void Die()
    {
        // Broadcast the gold reward before destroying so listeners that need
        // the reward amount don't have to race against the GameObject being gone.
        EnemyEvents.EnemyDied(data.goldReward);
        OnDeath?.Invoke(this);
        Destroy(gameObject);
    }

    #endregion
}
