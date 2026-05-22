using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemyPoolDataSO[] enemiesPoolData;
    [SerializeField] private EnemySpawnPoint[] spawnPoints;

    
    private Dictionary<EnemyType, ObjectPool<BaseEnemyAI>> pools = 
        new Dictionary<EnemyType, ObjectPool<BaseEnemyAI>>();

    private void Awake()
    {
        foreach (EnemyPoolDataSO data in enemiesPoolData)
        {
            ObjectPool<BaseEnemyAI> pool = new ObjectPool<BaseEnemyAI>(
                () => CreateEnemy(data),
                OnTakeEnemy,
                OnReturnEnemy,
                OnDestroyEnemy,
                true,
                data.defaultCapacity,
                data.maxSize);
            
            pools.Add(data.EnemyType,  pool);
        }
    }

    private BaseEnemyAI CreateEnemy(EnemyPoolDataSO data)
    {
        BaseEnemyAI enemy = Instantiate(data.prefab);
        enemy.gameObject.SetActive(false);
        return enemy;
    }

    private void OnTakeEnemy(BaseEnemyAI enemy)
    {
        enemy.OnDeath -= HandleEnemyDeath;
        enemy.OnDeath += HandleEnemyDeath;
        enemy.gameObject.SetActive(true);
    }

    private void OnReturnEnemy(BaseEnemyAI enemy)
    {
        enemy.OnDeath -= HandleEnemyDeath;
        enemy.gameObject.SetActive(false);
    }

    private void OnDestroyEnemy(BaseEnemyAI enemy)
    {
        Destroy(enemy.gameObject);
    }

    public BaseEnemyAI SpawnEnemy(EnemyType enemyType, Vector3 position)
    {
        if (!pools.ContainsKey(enemyType))
        {
            Debug.LogError("No existe pool para: " + enemyType);
            return null;
        }

        BaseEnemyAI enemy = pools[enemyType].Get();
        enemy.transform.position = position;
        enemy.transform.rotation = Quaternion.identity;
        enemy.Initialize();
        return enemy;
    }

    public void ReturnEnemy( BaseEnemyAI enemy)
    {
        EnemyType enemyType = enemy.EnemyType;
        if (!pools.ContainsKey(enemyType))
        {
            Destroy(enemy.gameObject);
            return;
        }

        pools[enemyType].Release(enemy);
    }
    public BaseEnemyAI SpawnEnemyFromPoint(EnemyType enemyType)
    {
        List<EnemySpawnPoint> validPoints = new List<EnemySpawnPoint>();

        foreach (EnemySpawnPoint point in spawnPoints)
        {
            if (point.EnemyType == enemyType)
            {
                validPoints.Add(point);
            }
        }

        if (validPoints.Count == 0)
        {
            Debug.LogError("No hay SpawnPoints para: " + enemyType);
            return null;
        }

        EnemySpawnPoint selectedPoint =
            validPoints[Random.Range(0, validPoints.Count)];

        return SpawnEnemy(enemyType, selectedPoint.Position);
    }

    private void HandleEnemyDeath(IDamageable damageable)
    {
        BaseEnemyAI enemy = damageable as BaseEnemyAI;

        if (enemy == null)
            return;

        ReturnEnemy(enemy);
    }
}
