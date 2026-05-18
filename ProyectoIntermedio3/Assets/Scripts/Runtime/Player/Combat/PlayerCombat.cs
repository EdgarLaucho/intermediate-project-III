using UnityEngine;
using UnityEngine.AI;

public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private Transform shootPoint;

    [Header("Settings")]
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private float rotationSpeed = 12f;

    private NavMeshAgent agent;
    private PlayerWeaponController weaponController;

    private Transform currentTarget;

    private bool hasManualTarget;

    private float attackTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        weaponController =
            GetComponent<PlayerWeaponController>();
    }

    private void Update()
    {
        ValidateTarget();

        if (!hasManualTarget)
        {
            FindAutomaticTarget();
        }

        HandleCombat();
    }

    public void SetTarget(Transform newTarget)
    {
        currentTarget = newTarget;

        hasManualTarget = true;
    }

    private void ValidateTarget()
    {
        if (currentTarget == null)
        {
            hasManualTarget = false;
            return;
        }

        IDamageable damageable =
            currentTarget.GetComponent<IDamageable>();

        if (damageable == null || !damageable.IsAlive)
        {
            currentTarget = null;
            hasManualTarget = false;
        }
    }

    private void FindAutomaticTarget()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            detectionRadius,
            enemyLayer);

        if (hits.Length <= 0)
        {
            currentTarget = null;
            return;
        }

        float closestDistance = float.MaxValue;

        Transform closestTarget = null;

        foreach (Collider hit in hits)
        {
            float distance = Vector3.Distance(
                transform.position,
                hit.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = hit.transform;
            }
        }

        currentTarget = closestTarget;
    }

    private void HandleCombat()
    {
        if (currentTarget == null)
            return;

        WeaponData weapon =
            weaponController.CurrentWeapon;

        if (weapon == null)
            return;

        float distance = Vector3.Distance(
            transform.position,
            currentTarget.position);

        if (distance > weapon.attackRange)
        {
            agent.SetDestination(
                currentTarget.position);

            return;
        }

        agent.ResetPath();

        RotateTowardsTarget();

        attackTimer -= Time.deltaTime;

        if (attackTimer > 0f)
            return;

        Attack(weapon);

        attackTimer = 1f / weapon.attackRate;
    }

    private void RotateTowardsTarget()
    {
        Vector3 direction =
            (currentTarget.position - transform.position).normalized;

        direction.y = 0f;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }

    private void Attack(WeaponData weapon)
    {
        Projectile projectile = Instantiate(
            weapon.projectilePrefab,
            shootPoint.position,
            Quaternion.identity);

        projectile.Initialize(
            currentTarget,
            weapon.damage);
    }
}