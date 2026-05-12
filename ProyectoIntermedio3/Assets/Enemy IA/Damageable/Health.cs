using UnityEngine;

public class Health : MonoBehaviour, IDamageable2
{
    [SerializeField] 
    private int maxHealth = 100;
    
    private int currentHealth;
    public bool isDead => currentHealth <= 0;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($"{gameObject.name} received {damage} damage. Health: {currentHealth}.");

        if (currentHealth<=0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} died/destroyed");
        gameObject.SetActive(false);
    }

}
