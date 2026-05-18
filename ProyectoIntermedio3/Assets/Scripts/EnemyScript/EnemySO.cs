using UnityEngine;

[CreateAssetMenu(fileName = "EnemySO", menuName = "Scriptable Objects/EnemySO")]
public class EnemySO : ScriptableObject
{
    [Header("Economy")]
    public int goldReward = 5;
    
    [Header("Stats")] 
    public int health =100;
    public float speed =3f;
    
    [Header("Target Priority")]
    public LayerMask foodTableLayer;
    public LayerMask turretLayer;
    public LayerMask playerLayer;
    
    [Header("Allowed Targets")]
    public bool canAttackFoodTable = true;
    public bool canAttackTurrets = true;
    public bool canAttackPlayer = true;
    
    [Header("Detection")]
    public float turretDetectionRange = 5f;
    public float playerDetectionRange = 4f;

    [Header("Attack")] 
    public int damage=1;
    public float attackRange = 1f;
    public float attackCooldown = 1f;
    
    [Header("Sounds")] 
    public AudioClip attackSound;
}
