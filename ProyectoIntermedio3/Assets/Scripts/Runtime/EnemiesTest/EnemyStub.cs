using UnityEngine;
using System;

public class EnemyStub : MonoBehaviour, ITargetable, ISlowable
{
    #region Inspector Fields

    [Header("Configuration")]
    [SerializeField] private EnemyData data;

    #endregion

    #region IDamageable / ITargetable Properties

    [field: SerializeField, ReadOnly]
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0;

    public int ContactDamage => data != null ? data.baseDamage : 0;

    #endregion

    #region ISlowable Properties

    public float MoveSpeedMultiplier => Time.time < _slowEndTime ? 1f - _strongestSlow : 1f;

    public float EffectiveMoveSpeed => data != null ? data.moveSpeed * MoveSpeedMultiplier : 0f;

    #endregion

    #region IDamageable Events

    public event Action<IDamageable> OnDeath;
    public event Action OnHealthChanged;

    #endregion

    #region Private State

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

    public void Heal(int amount) { }

    #endregion

    #region ISlowable Methods

    public void ApplySlow(float slowPercent, float duration)
    {
        if (!IsAlive || slowPercent <= 0f || duration <= 0f) return;

        _strongestSlow = Mathf.Max(_strongestSlow, Mathf.Clamp01(slowPercent));
        _slowEndTime = Mathf.Max(_slowEndTime, Time.time + duration);
    }

    #endregion

    #region Private Helpers

    private void Die()
    {
        EnemyEvents.EnemyDied(data.goldReward);
        OnDeath?.Invoke(this);
        Destroy(gameObject);
    }

    #endregion
}