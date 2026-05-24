using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemyDrop : MonoBehaviour
{
    [SerializeField]
    private BaseEnemyAI enemy;

    [SerializeField]
    private DropData[] possibleDrops;


    private void Awake()
    {
        if (enemy == null)
        {
            enemy = GetComponent<BaseEnemyAI>();
        }
    }

    private void OnEnable()
    {
        if (enemy==null)
            return;
        
        enemy.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (enemy==null)
            return;
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

            break;
        }
    }
}