using UnityEngine;

public class ExplodingEnemyAI : BaseEnemyAI
{
    [Header("Explosion")] 
    [SerializeField] 
    protected float explosionRadius = 3f;

    [SerializeField] 
    protected int explosionDamage = 40;
    
    [SerializeField]
    protected ExplosionWave explosionEffect;
    
    protected bool hasExploded = false;

    protected override void Attack()
    {
        if (hasExploded)
            return;

        Explode();
    }

    protected virtual void Explode()
    {
        Debug.Log("BOOM Kitty explotó");
        hasExploded = true;
        agent.isStopped = true;
        
        if (enemySO.attackSound != null)
        {
            AudioSource.PlayClipAtPoint(
                enemySO.attackSound,
                transform.position,
                1f
            );
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider hit in hits)
        {
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            
            if(damageable== null || !damageable.IsAlive)
                continue;
            
            if (damageable is BaseEnemyAI)
                continue;
            
            damageable.TakeDamage(explosionDamage);
        }
        if (explosionEffect != null)
        {
            Instantiate(
                explosionEffect,
                transform.position,
                Quaternion.identity);
        }
        Die();
        
        
    }

    public override void Initialize()
    {
        base.Initialize();
        hasExploded = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
