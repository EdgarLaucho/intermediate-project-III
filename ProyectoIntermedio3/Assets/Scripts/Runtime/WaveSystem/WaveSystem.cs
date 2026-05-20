using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WaveSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("Wave Settings")]
    public List<Wave> waves;
    public float timeBetweenWaves = 5f;

    private int currentWaveIndex = 0;
    private int currentEnemiesAlive = 0;

    void Start()
    {
        if (enemySpawner == null)
        {
            enemySpawner = FindFirstObjectByType<EnemySpawner>();
        }

        StartCoroutine(SpawnWave());
    }

    IEnumerator SpawnWave()
    {
        while (currentWaveIndex < waves.Count)
        {
            Wave currentWave = waves[currentWaveIndex];
            Debug.Log("WAVE OF: " + currentWave.waveName);

            currentEnemiesAlive = 0;

            for (int i = 0; i < currentWave.enemyCount; i++)
            {
                SpawnEnemyFromPool(currentWave.enemyType);
                yield return new WaitForSeconds(1f / currentWave.rate);
            }

            while (currentEnemiesAlive > 0)
            {
                yield return new WaitForSeconds(0.5f);
            }

            Debug.Log($"YOU SURVIVED TO {currentWave.waveName} !");

            currentWaveIndex++;
            yield return new WaitForSeconds(timeBetweenWaves);
        }

        Debug.Log("YOU WON THE GAME!");
    }

    private void SpawnEnemyFromPool(EnemyType type)
    {
        BaseEnemyAI enemy = enemySpawner.SpawnEnemyFromPoint(type);

        if (enemy != null)
        {
            currentEnemiesAlive++;

            enemy.OnDeath -= OnEnemyCharacterDied;
            enemy.OnDeath += OnEnemyCharacterDied;
        }
    }

    private void OnEnemyCharacterDied(IDamageable damageable)
    {
        if (damageable is BaseEnemyAI enemy)
        {
            enemy.OnDeath -= OnEnemyCharacterDied;
        }

        currentEnemiesAlive--;
    }
}