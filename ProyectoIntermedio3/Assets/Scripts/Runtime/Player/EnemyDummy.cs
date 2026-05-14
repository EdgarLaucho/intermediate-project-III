using UnityEngine;

public class EnemyDummy : MonoBehaviour
{
    [SerializeField] private int health = 5;

    public void TakeDamage(int damage)
    {
        health -= damage;

        Debug.Log($"{gameObject.name} recibió {damage} daño");

        if (health <= 0)
        {
            Destroy(gameObject);
        }
    }
}