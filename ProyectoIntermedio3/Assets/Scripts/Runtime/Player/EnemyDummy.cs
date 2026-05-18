using System;
using UnityEngine;

public class EnemyDummy : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 5;

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

    public void TakeDamage(int damage)
    {
        if (!IsAlive)
            return;

        currentHealth -= damage;

        OnHealthChanged?.Invoke();

        Debug.Log($"{gameObject.name} recibió {damage} daño");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
    }

    private void Die()
    {
        OnDeath?.Invoke(this);

        Destroy(gameObject);
    }
}