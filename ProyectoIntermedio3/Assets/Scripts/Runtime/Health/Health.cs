using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 100;

    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsAlive => currentHealth > 0;

    public event Action<IDamageable> OnDeath;
    public event Action OnHealthChanged;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (!IsAlive)
            return;

        currentHealth -= amount;

        currentHealth = Mathf.Max(currentHealth, 0);

        OnHealthChanged?.Invoke();

        Debug.Log($"{gameObject.name} took {amount} damage.");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (!IsAlive)
            return;

        currentHealth += amount;

        currentHealth = Mathf.Min(currentHealth, maxHealth);

        OnHealthChanged?.Invoke();

        Debug.Log($"{gameObject.name} healed {amount} HP.");
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} died.");

        OnDeath?.Invoke(this);

        gameObject.SetActive(false);
    }
}