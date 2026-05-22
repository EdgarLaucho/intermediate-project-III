using UnityEngine;

public class ExplosiveProjectile : Projectile
{
    [SerializeField] private float explosionRadius = 3f;

    [SerializeField] private LayerMask damageLayers;

    private Vector3 startPositionExplosive;

    private float currentTravelTime;

    [SerializeField]
    private float travelDuration = 0.7f;

    [SerializeField]
    private float arcHeightExplosive = 1.5f;

    [SerializeField]
    private ExplosionWave explosionEffect;

    public override void Initialize(Transform newTarget, int newDamage)
    {
        base.Initialize(newTarget, newDamage);

        startPositionExplosive = transform.position;

        currentTravelTime = 0f;
    }

    protected override void Move()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        currentTravelTime += Time.deltaTime;

        float progress = currentTravelTime / travelDuration;

        progress = Mathf.Clamp01(progress);

        Vector3 targetPosition = target.position;

        Vector3 nextPosition = Vector3.Lerp(
                startPositionExplosive,
                targetPosition,
                progress);

        nextPosition.y += Mathf.Sin(progress * Mathf.PI) * arcHeightExplosive;

        transform.position = nextPosition;

        transform.Rotate(
            220f * Time.deltaTime,
            220f * Time.deltaTime,
            220f * Time.deltaTime);

        if (progress >= 1f)
        {
            HitTarget();
        }
    }

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
            IDamageable damageable = hit.GetComponent<IDamageable>();

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