using UnityEngine;
using UnityEngine.AI;
public class BaseEnemyAI : MonoBehaviour
{
    [Header("References")] 
    [SerializeField] protected EnemySO enemySO;


    protected NavMeshAgent agent;
    protected Transform currentTarget;
    protected IDamageable currentDamageable;

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
        FindFoodTable();
    }

    protected virtual void Update()
    {
        if (currentTarget == null) return;
        
        LookAtTarget();

        float distance = Vector3.Distance(transform.position, currentTarget.position);

        if (distance > enemySO.attackRange)
        {
            MoveToTarget();
        }
        else
        {
            Attack();
        }
    }
    protected virtual void FindFoodTable()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            100f,
            enemySO.targetLayer
        );

        if (hits.Length <= 0)
        {
            Debug.LogWarning("No target found.");
            return;
        }

        currentTarget = hits[0].transform;
        currentDamageable = hits[0].GetComponent<IDamageable>();
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

            currentDamageable?.TakeDamage(enemySO.damage);

            Debug.Log($"{gameObject.name} attacked {currentTarget.name}");
        }
    }
    
    protected virtual void LookAtTarget()
    {
        Vector3 direction = currentTarget.position - transform.position;
        direction.y = 0f;

        if (direction == Vector3.zero) return;

        transform.rotation = Quaternion.LookRotation(-direction);
    }
}
