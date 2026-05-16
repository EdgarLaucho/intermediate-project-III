using UnityEngine;
using UnityEngine.InputSystem;

public class EnemySpawnTester : MonoBehaviour
{
    [SerializeField] private EnemySpawner spawner;

    private void Update()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            spawner.SpawnEnemyFromPoint(EnemyType.Chicken);
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            spawner.SpawnEnemyFromPoint(EnemyType.Penguin);
        }
    }
}