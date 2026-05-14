using UnityEngine;
using UnityEngine.AI;
public class BaseEnemyAI : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] protected EnemySO enemySO;

    protected EnemyState currentState;
    protected NavMeshAgent agent;
    protected Transform currentTarget;
    protected IDamageable2 currentDamageable;
    protected bool hasCompletedObjective;

    protected float attackTimer;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        if (enemySO!=null)
        {
            agent.speed = enemySO.speed;
        }
    }

    protected virtual void Start()
    {
        currentState = EnemyState.Idle;
        FindFoodTable();
    }

    protected virtual void Update()
    {
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
        EvaluateTargets();
        
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
            case EnemyState.AttackingTurret:
            case EnemyState.ChasingPlayer:
                HandleMove();
                break;
            case EnemyState.Attacking:
                HandleAttack();
                break;
        }
    }
    //State Enemy
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
            if (currentTarget.gameObject.layer == LayerMask.NameToLayer("Player"))
            {
                currentState = EnemyState.ChasingPlayer;
            }
            else if (currentTarget.gameObject.layer == LayerMask.NameToLayer("Turret"))
            {
                currentState = EnemyState.AttackingTurret;
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
        currentDamageable = closestTable.GetComponentInParent<IDamageable2>();
    }
    protected virtual void MoveToTarget()
    {
        agent.isStopped = false;
        agent.SetDestination(currentTarget.position);
    }
    
    protected virtual void Attack()
    {
        agent.isStopped = true;

        attackTimer += Time.deltaTime;

        if (attackTimer >= enemySO.attackCooldown)
        {
            attackTimer = 0f;
            
            
            if (currentDamageable == null || currentDamageable.isDead)
            {
                currentState = EnemyState.Idle;
                currentTarget = null;
                currentDamageable = null;
                agent.isStopped = true;
                return;
            }
            currentDamageable.TakeDamage(enemySO.damage);

            Debug.Log($"{gameObject.name} attacked {currentTarget.name}");
            if (currentDamageable.isDead)
            {
                if (currentTarget.gameObject.layer == LayerMask.NameToLayer("Player") ||
                    currentTarget.gameObject.layer == LayerMask.NameToLayer("FoodTable"))
                {
                    hasCompletedObjective = true;
                    currentState = EnemyState.Idle;
                    currentTarget = null;
                    currentDamageable = null;
                    agent.isStopped = true;
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
        // If already chasing player, ignore everything else
        if (currentTarget != null && 
            currentTarget.gameObject.layer == LayerMask.NameToLayer("Player") &&
            currentDamageable != null &&
            !currentDamageable.isDead)
        {
            return;
        }

        // Search for nearby Player first
        Collider[] players = Physics.OverlapSphere(
            transform.position,
            enemySO.playerDetectionRange,
            enemySO.playerLayer
        );

        foreach (Collider playerCollider in players)
        {
            IDamageable2 damageable = playerCollider.GetComponentInParent<IDamageable2>();

            if (damageable == null || damageable.isDead)
            {
                continue;
            }

            bool shouldAttackPlayer = Random.value <= enemySO.playerAttackChance;

            if (shouldAttackPlayer)
            {
                currentTarget = playerCollider.transform;
                currentDamageable = damageable;
                currentState = EnemyState.ChasingPlayer;

                Debug.Log("Changing target to PLAYER");
                return;
            }
        }

        // Search for nearby turret after player chance
        Collider[] turrets = Physics.OverlapSphere(
            transform.position,
            enemySO.turretDetectionRange,
            enemySO.turretLayer
        );

        foreach (Collider turret in turrets)
        {
            IDamageable2 damageable = turret.GetComponentInParent<IDamageable2>();

            if (damageable == null || damageable.isDead)
            {
                continue;
            }

            if (currentTarget != turret.transform)
            {
                currentTarget = turret.transform;
                currentDamageable = damageable;
                currentState = EnemyState.AttackingTurret;
            }

            return;
        }

        if (currentTarget != null && currentTarget.gameObject.layer == LayerMask.NameToLayer("FoodTable"))
        {
            return;
        }

        currentState = EnemyState.MovingToTarget;
        FindFoodTable();
    }
    
    protected virtual bool HasValidTargets()
    {
        Collider[] foodTables = Physics.OverlapSphere(
            transform.position,
            100f,
            enemySO.foodTableLayer
        );

        foreach (Collider table in foodTables)
        {
            IDamageable2 damageable = table.GetComponentInParent<IDamageable2>();

            if (damageable != null && !damageable.isDead)
            {
                return true;
            }
        }

        return false;
    }
}
