using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] protected float speed = 12f;

    protected Transform target;
    protected int damage;

    public virtual void Initialize(
        Transform newTarget,
        int newDamage)
    {
        target = newTarget;
        damage = newDamage;
    }

    protected virtual void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Move();
    }

    protected virtual void Move()
    {
        Vector3 direction = (target.position - transform.position).normalized;

        transform.position += direction * speed * Time.deltaTime;

        transform.LookAt(target);

        float distance = Vector3.Distance(
            transform.position,
            target.position);

        if (distance <= 0.2f)
        {
            HitTarget();
        }
    }

    protected virtual void HitTarget()
    {
        IDamageable damageable = target.GetComponent<IDamageable>();

        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}