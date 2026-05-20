using UnityEngine;

public class RangedEnemyAI : BaseEnemyAI
{
   [Header("Ranged Attack")] 
   [SerializeField]
   protected Transform shootPoint;

   protected override  void Attack()
   {
      agent.isStopped = true;
      
      attackTimer += Time.deltaTime;
      if (attackTimer >= enemySO.attackCooldown)
      {
         attackTimer = 0f;
         if (currentDamageable == null || !currentDamageable.IsAlive)
         {
            currentState = EnemyState.Idle;
            currentTarget = null;
            currentDamageable = null;
            agent.isStopped = false;
            return;
         }
         ShootProjectile();
         
         Debug.Log($"{gameObject.name} shot projectile at {currentTarget.name}");
      }
      
   }

   protected virtual void ShootProjectile()
   {
      if (enemySO.projectilePrefab == null || currentTarget == null || shootPoint == null)
         return;
      
      EnemyProjectileBase projectile = Instantiate(
         enemySO.projectilePrefab,
         shootPoint.position,
         Quaternion.identity
      );
      
      Vector3 direction = (currentTarget.position - shootPoint.position).normalized;

      projectile.Initialize(direction, enemySO.damage, enemySO.projectileSpeed, this);
   }
   
}
