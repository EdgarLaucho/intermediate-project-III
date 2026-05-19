using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WaveSystem : MonoBehaviour
{
    public List<Wave> waves;
    public Transform spawnPoint;
    public float timeBetweenWaves = 5f;

    private int currentWaveIndex = 0;

    void Start()
    {
        StartCoroutine(SpawnWave());
    }

    IEnumerator SpawnWave()
    {
        while (currentWaveIndex < waves.Count)
        {
            Wave currentWave = waves[currentWaveIndex];
            Debug.Log("Wave number: " + currentWave.waveName);

            for (int i = 0; i < currentWave.enemyCount; i++)
            {
                SpawnEnemy(currentWave.enemyPrefab);
                yield return new WaitForSeconds(1f / currentWave.rate);
            }

            while (GameObject.FindGameObjectsWithTag("Enemy").Length > 0)
            {
                yield return new WaitForSeconds(1f);
            }

            currentWaveIndex++;
            yield return new WaitForSeconds(timeBetweenWaves);
        }

        Debug.Log("YOU WON!");
    }

    void SpawnEnemy(GameObject enemyPrefab)
    {
        Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
    }
}