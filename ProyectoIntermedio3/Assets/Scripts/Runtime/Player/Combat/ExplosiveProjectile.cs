using UnityEngine;

public class ExplosiveProjectile : Projectile
{
    [SerializeField] private float explosionRadius = 3f;

    protected override void HitTarget()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            explosionRadius,
            LayerMask.GetMask("Enemy"));

        foreach (Collider hit in hits)
        {
            EnemyDummy enemy =
                hit.GetComponent<EnemyDummy>();

            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }
        }

        Destroy(gameObject);
    }
}