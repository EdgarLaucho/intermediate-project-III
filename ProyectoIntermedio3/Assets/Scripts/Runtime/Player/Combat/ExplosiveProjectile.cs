using UnityEngine;

public class ExplosiveProjectile : Projectile
{
    [SerializeField]
    private float explosionRadius = 3f;

    [SerializeField]
    private LayerMask damageLayers;

    [SerializeField]
    private ExplosionWave explosionEffect;

    protected override void HitTarget()
    {
        if (explosionEffect != null)
        {
            Instantiate(
                explosionEffect,
                transform.position,
                Quaternion.identity);
        }

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            explosionRadius,
            damageLayers);

        foreach (Collider hit in hits)
        {
            IDamageable damageable =
                hit.GetComponent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            explosionRadius);
    }
}