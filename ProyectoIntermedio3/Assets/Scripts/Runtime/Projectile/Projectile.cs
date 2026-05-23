using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField]
    protected float speed = 12f;

    [SerializeField]
    protected float arcHeight = 0.3f;

    protected Transform target;
    protected int damage;

    protected Vector3 startPosition;
    protected float journeyLength;
    protected float currentTravelDistance;

    protected bool isGamePaused;

    protected virtual void OnEnable()
    {
        GamePauseEvents.OnGamePaused += HandleGamePaused;
        GamePauseEvents.OnGameResumed += HandleGameResumed;
        isGamePaused = GamePauseEvents.IsPaused;
    }

    protected virtual void OnDisable()
    {
        GamePauseEvents.OnGamePaused -= HandleGamePaused;
        GamePauseEvents.OnGameResumed -= HandleGameResumed;
    }

    private void HandleGamePaused()
    {
        isGamePaused = true;
    }

    private void HandleGameResumed()
    {
        isGamePaused = false;
    }

    public virtual void Initialize(Transform newTarget, int newDamage)
    {
        target = newTarget;
        damage = newDamage;

        startPosition = transform.position;

        if (target != null)
        {
            journeyLength =
                Vector3.Distance(
                    startPosition,
                    target.position);
        }
    }

    protected virtual void Update()
    {
        if (isGamePaused)
            return;

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Move();
    }

    protected virtual void Move()
    {
        currentTravelDistance += speed * Time.deltaTime;

        float progress = currentTravelDistance / journeyLength;

        progress = Mathf.Clamp01(progress);

        Vector3 targetPosition = target.position;

        Vector3 nextPosition =
            Vector3.Lerp(
                startPosition,
                targetPosition,
                progress);

        nextPosition.y += Mathf.Sin(progress * Mathf.PI) * arcHeight;

        Vector3 movementDirection = nextPosition - transform.position;

        transform.position = nextPosition;

        if (movementDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(movementDirection)
                * Quaternion.Euler(90f, 0f, 0f);
        }

        if (progress >= 1f)
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