using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "TD/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName;

    [Header("Economy")]
    public int goldReward; // gold awarded to the player on kill

    [Header("Stats")]
    public int baseHealth;
    public int baseDamage;
    public float moveSpeed;
}