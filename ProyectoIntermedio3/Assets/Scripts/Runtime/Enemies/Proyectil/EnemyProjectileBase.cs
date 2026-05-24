using UnityEngine;

public class EnemyProjectileBase : MonoBehaviour
{
    [Header("Settings")] [SerializeField]
    protected float speed = 10f;
    [SerializeField]
    protected int damage = 10;
    [SerializeField]
    protected float lifeTime = 10f;

    protected Vector3 moveDirection;
    protected IDamageable owner;
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

    protected virtual void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    protected virtual void Update()
    {
        if (isGamePaused)
            return;

        transform.position += moveDirection * speed * Time.deltaTime;
    }

    public virtual void Initialize(Vector3 direction, int projectileDamage, float projectileSpeed, IDamageable projectileOwner)
    {
        moveDirection = direction.normalized;
        damage = projectileDamage;
        speed = projectileSpeed;
        owner = projectileOwner;
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        Debug.Log("Knife hit: " + other.name);
        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        Debug.Log("Damageable found: " + (damageable != null));

        if (damageable == null)
            return;
        
        if (damageable == owner)
            return;
        
        if (damageable is BaseEnemyAI)
            return;

        damageable.TakeDamage(damage);

        Destroy(gameObject);
    }
}
