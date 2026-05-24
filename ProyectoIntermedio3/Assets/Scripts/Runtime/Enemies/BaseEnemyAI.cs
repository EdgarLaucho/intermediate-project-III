using System;
using UnityEngine;
using UnityEngine.AI;



public class BaseEnemyAI : MonoBehaviour, IDamageable, ITargetable
{
    [Header("References")] 
    [SerializeField] protected EnemySO enemySO;
    
    
    protected EnemyState currentState;
    protected NavMeshAgent agent;
    protected Transform currentTarget;
    public EnemyType EnemyType => enemySO.type;
    protected IDamageable currentDamageable;
    protected bool hasCompletedObjective;
    protected int currentTargetLayer;
    protected bool isGamePaused;
    protected bool wasAttackedByPlayer;
    protected Transform playerAggroTarget;
    protected AudioSource audioSource;
    

    protected float attackTimer;
    
    
    
    public int CurrentHealth { get; protected set; }
    public int MaxHealth { get; protected set; }
    public bool IsAlive => CurrentHealth > 0;
    public event Action<IDamageable> OnDeath;
    public event Action OnHealthChanged;
    protected virtual void Awake()
    {
        
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
        agent.updateRotation = false;
        if (enemySO!=null)
        {
            MaxHealth = enemySO.health;
            CurrentHealth = MaxHealth;
            agent.speed = enemySO.speed;
        }
    }

    protected virtual void Start()
    {
        currentState = EnemyState.Idle;
        FindFoodTable();
    }

    protected virtual void OnEnable()
    {
        GamePauseEvents.OnGamePaused += HandleGamePaused;
        GamePauseEvents.OnGameResumed += HandleGameResumed;

        isGamePaused = GamePauseEvents.IsPaused;
        if (isGamePaused)
            StopAgent();
    }

    protected virtual void OnDisable()
    {
        GamePauseEvents.OnGamePaused -= HandleGamePaused;
        GamePauseEvents.OnGameResumed -= HandleGameResumed;
    }

    protected virtual void Update()
    {
        if (isGamePaused)
        {
            StopAgent();
            return;
        }

        if (hasCompletedObjective)
        {
            agent.isStopped = true;
            return;
        }
        if (!HasValidTargets())
        {
            currentState = EnemyState.Idle;
            currentTarget = null;
            currentDamageable = null;
            agent.isStopped = true;
            return;
        }
        
        if (currentState != EnemyState.Attacking)
        {
            EvaluateTargets();
        }
        
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            currentState = EnemyState.Idle;
            agent.isStopped = true;
            return;
        }
        
        LookAtTarget();

        switch (currentState)
        {
            case EnemyState.Idle:
                HandleIdle();
                break;
            case EnemyState.MovingToTarget:
            case EnemyState.AttackingConstruction:
            case EnemyState.ChasingPlayer:
                HandleMove();
                break;
            case EnemyState.Attacking:
                HandleAttack();
                break;
        }
    }
    
    public virtual void Initialize()
    {
        MaxHealth = enemySO.health;
        CurrentHealth = MaxHealth;

        hasCompletedObjective = false;
        currentState = EnemyState.Idle;
        currentTarget = null;
        currentDamageable = null;
        attackTimer = 0f;

        if (agent != null)
        {
            agent.speed = enemySO.speed;
            agent.isStopped = GamePauseEvents.IsPaused;
        }

        isGamePaused = GamePauseEvents.IsPaused;

        FindFoodTable();
    }

    private void HandleGamePaused()
    {
        isGamePaused = true;
        StopAgent();
    }

    private void HandleGameResumed()
    {
        isGamePaused = false;
    }

    private void StopAgent()
    {
        if (agent != null && agent.enabled)
            agent.isStopped = true;
    }
    
   
    protected virtual void HandleIdle()
    {
        currentState = EnemyState.MovingToTarget;
    }

    protected virtual void HandleMove()
    {
        float distance = Vector3.Distance(transform.position, currentTarget.position);

        if (distance > enemySO.attackRange)
        {
            MoveToTarget();
        }
        else
        {
            currentState = EnemyState.Attacking;
        }
    }

    protected virtual void HandleAttack()
    {
        float distance = Vector3.Distance(transform.position, currentTarget.position);
        if (distance > enemySO.attackRange)
        {
            if (currentTargetLayer == LayerMask.NameToLayer("Player"))
            {
                currentState = EnemyState.ChasingPlayer;
            }
            else if (currentTargetLayer == LayerMask.NameToLayer("Construction"))
            {
                currentState = EnemyState.AttackingConstruction;
            }
            else
            {
                currentState = EnemyState.MovingToTarget;
            }

            return;
        }
        Attack();
    }
   
    
    
    protected virtual void FindFoodTable()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            100f,
            enemySO.foodTableLayer
        );

        if (hits.Length <= 0)
        {
            Debug.LogWarning("No target found.");
            currentTarget = null;
            currentDamageable = null;
            return;
        }

        Collider closestTable = hits[0];
        float closestDistance = Vector3.Distance(transform.position, closestTable.transform.position);

        for (int i = 1; i < hits.Length; i++)
        {
            float distance = Vector3.Distance(transform.position, hits[i].transform.position);

            if (distance < closestDistance)
            {
                closestTable = hits[i];
                closestDistance = distance;
            }
        }

        currentTarget = closestTable.transform;
        currentDamageable = closestTable.GetComponentInParent<IDamageable>();
        currentTargetLayer = LayerMask.NameToLayer("FoodTable");
    }
    protected virtual void MoveToTarget()
    {
        agent.isStopped = false;
        agent.SetDestination(currentTarget.position);
    }
    
    protected virtual void Attack()
    {
        BaseMeleeAttack();
    }

    protected void BaseMeleeAttack()
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
                agent.isStopped = true;
                return;
            }

            currentDamageable.TakeDamage(enemySO.damage);
            
            if (enemySO.attackSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(enemySO.attackSound);
            }

            Debug.Log($"{gameObject.name} attacked {currentTarget.name}");

            if (!currentDamageable.IsAlive)
            {
                int deadTargetLayer = currentTargetLayer;

                currentState = EnemyState.Idle;
                currentTarget = null;
                currentDamageable = null;
                agent.isStopped = true;

                if (deadTargetLayer == LayerMask.NameToLayer("FoodTable") ||
                    deadTargetLayer == LayerMask.NameToLayer("Player"))
                {
                    hasCompletedObjective = true;
                }
            }
        }
    }
    
    protected virtual void LookAtTarget()
    {
        Vector3 direction = currentTarget.position - transform.position;
        direction.y = 0f;

        if (direction == Vector3.zero) return;

        transform.rotation = Quaternion.LookRotation(-direction);
    }

    protected virtual void EvaluateTargets()
    {
        if (enemySO.canAttackConstruction &&
            currentTarget != null &&
            currentTargetLayer == LayerMask.NameToLayer("Construction") &&
            currentDamageable != null &&
            currentDamageable.IsAlive)
        {
            currentState = EnemyState.AttackingConstruction;
            return;
        }

        if (enemySO.canAttackPlayer &&
            currentTarget != null &&
            currentTargetLayer == LayerMask.NameToLayer("Player") &&
            currentDamageable != null &&
            currentDamageable.IsAlive)
        {
            currentState = EnemyState.ChasingPlayer;
            return;
        }

        if (enemySO.canAttackConstruction)
        {
            Collider[] Constructions = Physics.OverlapSphere(
                transform.position,
                enemySO.ConstructionDetectionRange,
                enemySO.constructionLayer
            );

            foreach (Collider construction in Constructions)
            {
                IDamageable damageable = construction.GetComponentInParent<IDamageable>();
                
                Debug.Log(
                    $"[CONSTRUCTION CHECK] Collider: {construction.name} | " +
                    $"Parent: {construction.transform.root.name} | " +
                    $"Damageable: {damageable != null} | " +
                    $"IsAlive: {(damageable != null ? damageable.IsAlive : false)}"
                );
                if (damageable == null || !damageable.IsAlive)
                    continue;

                currentTarget = construction.transform;
                currentDamageable = damageable;
                currentTargetLayer = LayerMask.NameToLayer("Construction");
                currentState = EnemyState.AttackingConstruction;

                Debug.Log("Changing target to Construction");
                return;
            }
        }

        if (enemySO.canAttackPlayer &&
            wasAttackedByPlayer &&
            playerAggroTarget != null)
        {
            float distance = Vector3.Distance(
                transform.position,
                playerAggroTarget.position);

            if (distance <= enemySO.playerDetectionRange)
            {
                IDamageable damageable =
                    playerAggroTarget.GetComponentInParent<IDamageable>();

                if (damageable != null && damageable.IsAlive)
                {
                    currentTarget = playerAggroTarget;
                    currentDamageable = damageable;
                    currentTargetLayer = LayerMask.NameToLayer("Player");
                    currentState = EnemyState.ChasingPlayer;

                    Debug.Log("Player attacked me, chasing PLAYER");
                    return;
                }
            }

            wasAttackedByPlayer = false;
            playerAggroTarget = null;
        }

        // Finalmente va a la mesa si puede atacarla
        if (enemySO.canAttackFoodTable)
        {
            if (currentTarget != null &&
                currentTargetLayer == LayerMask.NameToLayer("FoodTable") &&
                currentDamageable != null &&
                currentDamageable.IsAlive)
            {
                return;
            }

            currentState = EnemyState.MovingToTarget;
            FindFoodTable();
            return;
        }

        currentTarget = null;
        currentDamageable = null;
        currentState = EnemyState.Idle;
        agent.isStopped = true;
    }
    
    protected virtual bool HasValidTargets()
    {
        if (enemySO.canAttackFoodTable)
        {
            Collider[] foodTables = Physics.OverlapSphere(
                transform.position,
                100f,
                enemySO.foodTableLayer
            );

            foreach (Collider table in foodTables)
            {
                IDamageable damageable = table.GetComponentInParent<IDamageable>();

                if (damageable != null && damageable.IsAlive)
                    return true;
            }
        }

        if (enemySO.canAttackConstruction)
        {
            Collider[] constructions = Physics.OverlapSphere(
                transform.position,
                enemySO.ConstructionDetectionRange,
                enemySO.constructionLayer
            );

            foreach (Collider construction in constructions)
            {
                IDamageable damageable = construction.GetComponentInParent<IDamageable>();
                Debug.Log(
                    $"[HAS VALID CONSTRUCTION] Collider: {construction.name} | " +
                    $"Damageable: {damageable != null} | " +
                    $"IsAlive: {(damageable != null ? damageable.IsAlive : false)}"
                );
                if (damageable != null && damageable.IsAlive)
                    return true;
            }
        }

        if (enemySO.canAttackPlayer)
        {
            Collider[] players = Physics.OverlapSphere(
                transform.position,
                enemySO.playerDetectionRange,
                enemySO.playerLayer
            );

            foreach (Collider player in players)
            {
                IDamageable damageable = player.GetComponentInParent<IDamageable>();

                
                if (damageable != null && damageable.IsAlive)
                    return true;
            }
        }

        return false;
    }

    
    public void TakeDamage(int amount)
    {
        if (!IsAlive)
            return;
        
        CurrentHealth= Mathf.Max(0,CurrentHealth-amount);
        OnHealthChanged?.Invoke();
        Debug.Log($"{gameObject.name} received{amount} damage. Health: {CurrentHealth}");

        if (!IsAlive)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (!IsAlive)
            return;
        
        CurrentHealth = Mathf.Min(MaxHealth,CurrentHealth+amount);
        OnHealthChanged?.Invoke();
    }

    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} died");
        
        EnemyEvents.EnemyDied(enemySO.goldReward);
        OnDeath?.Invoke(this);
    }
    
    public void SetPlayerAsTarget(Transform player)
    {
        if (!enemySO.canAttackPlayer)
            return;

        wasAttackedByPlayer = true;
        playerAggroTarget = player;
    }
}
