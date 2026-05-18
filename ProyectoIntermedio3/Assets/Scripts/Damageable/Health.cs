using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 100;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsAlive => CurrentHealth > 0;

    public event Action<IDamageable> OnDeath;
    public event Action OnHealthChanged;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (!IsAlive) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke();

        Debug.Log($"{gameObject.name} received {amount} damage. Health: {CurrentHealth}");

        if (!IsAlive)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (!IsAlive) return;

        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke();
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} died/destroyed");

        OnDeath?.Invoke(this);
        gameObject.SetActive(false);
    }
}