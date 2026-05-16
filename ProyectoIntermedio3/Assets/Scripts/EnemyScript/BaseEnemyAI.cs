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
    protected int currentTargetLayer;

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
            if (currentTargetLayer == LayerMask.NameToLayer("Player"))
            {
                currentState = EnemyState.ChasingPlayer;
            }
            else if (currentTargetLayer == LayerMask.NameToLayer("Turret"))
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
        currentTargetLayer = LayerMask.NameToLayer("FoodTable");
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
                int deadTargetLayer = currentTargetLayer;

                currentState = EnemyState.Idle;
                currentTarget = null;
                currentDamageable = null;
                agent.isStopped = true;

                if (deadTargetLayer == LayerMask.NameToLayer("FoodTable")||
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
        // Si ya está atacando una torreta viva, NO cambia de objetivo
        if (enemySO.canAttackTurrets &&
            currentTarget != null &&
            currentTargetLayer == LayerMask.NameToLayer("Turret") &&
            currentDamageable != null &&
            !currentDamageable.isDead)
        {
            currentState = EnemyState.AttackingTurret;
            return;
        }

        // Si ya está persiguiendo un player vivo, NO cambia de objetivo
        if (enemySO.canAttackPlayer &&
            currentTarget != null &&
            currentTargetLayer == LayerMask.NameToLayer("Player") &&
            currentDamageable != null &&
            !currentDamageable.isDead)
        {
            currentState = EnemyState.ChasingPlayer;
            return;
        }

        // Primero busca torretas si puede atacarlas
        // Esto permite que deje la mesa si aparece una torreta cerca
        if (enemySO.canAttackTurrets)
        {
            Collider[] turrets = Physics.OverlapSphere(
                transform.position,
                enemySO.turretDetectionRange,
                enemySO.turretLayer
            );

            foreach (Collider turret in turrets)
            {
                IDamageable2 damageable = turret.GetComponentInParent<IDamageable2>();

                if (damageable == null || damageable.isDead)
                    continue;

                currentTarget = turret.transform;
                currentDamageable = damageable;
                currentTargetLayer = LayerMask.NameToLayer("Turret");
                currentState = EnemyState.AttackingTurret;

                Debug.Log("Changing target to TURRET");
                return;
            }
        }

        // Luego busca player si puede atacarlo
        // Ya NO hay probabilidad, si lo detecta lo ataca
        if (enemySO.canAttackPlayer)
        {
            Collider[] players = Physics.OverlapSphere(
                transform.position,
                enemySO.playerDetectionRange,
                enemySO.playerLayer
            );

            foreach (Collider playerCollider in players)
            {
                IDamageable2 damageable = playerCollider.GetComponentInParent<IDamageable2>();

                if (damageable == null || damageable.isDead)
                    continue;

                currentTarget = playerCollider.transform;
                currentDamageable = damageable;
                currentTargetLayer = LayerMask.NameToLayer("Player");
                currentState = EnemyState.ChasingPlayer;

                Debug.Log("Changing target to PLAYER");
                return;
            }
        }

        // Finalmente va a la mesa si puede atacarla
        if (enemySO.canAttackFoodTable)
        {
            if (currentTarget != null &&
                currentTargetLayer == LayerMask.NameToLayer("FoodTable") &&
                currentDamageable != null &&
                !currentDamageable.isDead)
            {
                return;
            }

            currentState = EnemyState.MovingToTarget;
            FindFoodTable();
            return;
        }

        // Si no puede atacar nada
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
                IDamageable2 damageable = table.GetComponentInParent<IDamageable2>();

                if (damageable != null && !damageable.isDead)
                    return true;
            }
        }

        if (enemySO.canAttackTurrets)
        {
            Collider[] turrets = Physics.OverlapSphere(
                transform.position,
                enemySO.turretDetectionRange,
                enemySO.turretLayer
            );

            foreach (Collider turret in turrets)
            {
                IDamageable2 damageable = turret.GetComponentInParent<IDamageable2>();

                if (damageable != null && !damageable.isDead)
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
                IDamageable2 damageable = player.GetComponentInParent<IDamageable2>();

                if (damageable != null && !damageable.isDead)
                    return true;
            }
        }

        return false;
    }
}
