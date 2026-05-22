using UnityEngine;

public class EnemyDrop : MonoBehaviour
{
    [SerializeField]
    private EnemyDummy enemy;

    [SerializeField]
    private DropData[] possibleDrops;

    private void OnEnable()
    {
        enemy.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        enemy.OnDeath -= HandleDeath;
    }

    private void HandleDeath(IDamageable deadEnemy)
    {
        foreach (DropData drop in possibleDrops)
        {
            float roll = Random.Range(0f, 100f);

            if (roll > drop.dropChance)
                continue;

            Instantiate(
                drop.pickupPrefab,
                transform.position,
                Quaternion.identity);
        }
    }
}