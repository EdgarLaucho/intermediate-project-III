using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    [SerializeField] private EnemyType enemyType;

    public EnemyType EnemyType => enemyType;

    public Vector3 Position => transform.position;
}
