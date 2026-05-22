using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class WaveSystem : MonoBehaviour
{
    public static event Action OnGameWon;

    [Header("References")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private GameObject startWaveButton;

    [Header("Wave Settings")]
    public List<Wave> waves;
    public float timeBetweenWaves = 5f;

    [Header("Game Mode Settings")]
    public bool isNormalMode = false;

    private int currentWaveIndex = 0;
    private int currentEnemiesAlive = 0;
    private bool startNextWavePressed = false;

    void Start()
    {
        isNormalMode = (PlayerPrefs.GetInt("NormalModeActive", 0) == 1);

        if (startWaveButton != null)
        {
            startWaveButton.SetActive(isNormalMode);
        }

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
            if (isNormalMode)
            {
                startNextWavePressed = false;

                while (!startNextWavePressed)
                {
                    yield return null;
                }
            }

            Wave currentWave = waves[currentWaveIndex];
            Debug.Log("LOADING WAVE: " + currentWave.waveName);

            currentEnemiesAlive = 0;

            int groupsFinished = 0;
            foreach (EnemyMix mix in currentWave.mixedEnemies)
            {
                StartCoroutine(SpawnEnemyGroup(mix, () => {
                    groupsFinished++;
                }));
            }

            while (groupsFinished < currentWave.mixedEnemies.Count)
            {
                yield return new WaitForSeconds(0.2f);
            }

            while (currentEnemiesAlive > 0)
            {
                yield return new WaitForSeconds(0.5f);
            }

            Debug.Log($"YOU SURVIVED TO {currentWave.waveName} !");

            currentWaveIndex++;
            if (!isNormalMode)
            {
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        Debug.Log("YOU WON THE GAME!");
        OnGameWon?.Invoke();
    }

    public void StartNextWave()
    {
        if (isNormalMode && currentEnemiesAlive == 0)
        {
            startNextWavePressed = true;
        }
    }

    IEnumerator SpawnEnemyGroup(EnemyMix mix, Action onGroupComplete)
    {
        if (mix.delayBeforeStart > 0)
        {
            yield return new WaitForSeconds(mix.delayBeforeStart);
        }

        for (int i = 0; i < mix.enemyCount; i++)
        {
            SpawnEnemyFromPool(mix.enemyType);
            yield return new WaitForSeconds(1f / mix.rate);
        }

        onGroupComplete?.Invoke();
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