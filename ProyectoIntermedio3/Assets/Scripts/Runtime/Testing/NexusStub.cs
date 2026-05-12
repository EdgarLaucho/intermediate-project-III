using UnityEngine;
using System;

public class NexusStub : MonoBehaviour, IDamageable
{
    [Header("Configuration")]
    [SerializeField] private int maxHealth = 500;

    [field: SerializeField, ReadOnly]
    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsAlive => CurrentHealth > 0;

    public event Action<IDamageable> OnDeath;
    public event Action OnHealthChanged;

    private void Awake() => CurrentHealth = maxHealth;

    public void TakeDamage(int amount)
    {
        if (!IsAlive) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke();
        Debug.Log($"[NexusStub] Took {amount} damage. HP: {CurrentHealth}/{maxHealth}");
        if (!IsAlive)
        {
            Debug.Log("[NexusStub] Nexus destroyed — Game Over.");
            OnDeath?.Invoke(this);
        }
    }

    public void Heal(int amount)
    {
        if (!IsAlive) return;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke();
    }
}