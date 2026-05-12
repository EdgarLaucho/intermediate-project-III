using UnityEngine;

[CreateAssetMenu(fileName = "EnemySO", menuName = "Scriptable Objects/EnemySO")]
public class EnemySO : ScriptableObject
{
    [Header("Stats")] 
    public int health;
    public float speed;
    
    [Header("Layers")]
    public LayerMask targetLayer;
    
    [Header("Detection")]
    public float turretDetectionRange = 5f;
    public float playerDetectionRange = 4f;
    [Range(0f, 1f)] public float playerAttackChance = 0.15f;

    [Header("Attack")] 
    public int damage;
    public float attackRange = 1f;
    public float attackCooldown = 1f;
    
    [Header("Sounds")] 
    public AudioClip attackSound;
}
