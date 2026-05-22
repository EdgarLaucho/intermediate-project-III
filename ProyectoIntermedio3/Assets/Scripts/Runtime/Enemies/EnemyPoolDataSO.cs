using UnityEngine;


[CreateAssetMenu(menuName = "Enemies/Enemy Pool Data")]
public class EnemyPoolDataSO : ScriptableObject
{
    public EnemyType EnemyType;
    public BaseEnemyAI prefab;
    public int defaultCapacity = 10;
    public int maxSize = 30;
}
