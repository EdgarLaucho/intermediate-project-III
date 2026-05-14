using UnityEngine;
using UnityEngine.AI;

public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private Transform shootPoint;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 12f;

    private NavMeshAgent _agent;
    private PlayerWeapon _weapon;
    private Transform _currentTarget;

    private float _attackTimer;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _weapon = GetComponent<PlayerWeapon>();
    }

    private void OnEnable()
    {
        InputEvents.OnPrimaryPressed += HandlePrimaryPressed;
    }

    private void OnDisable()
    {
        InputEvents.OnPrimaryPressed -= HandlePrimaryPressed;
    }

    private void Update()
    {
        HandleCombat();
    }

    private void HandlePrimaryPressed()
    {
        Ray ray = Camera.main.ScreenPointToRay(
            UnityEngine.InputSystem.Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, enemyLayer))
        {
            _currentTarget = hit.transform;
        }
    }

    private void HandleCombat()
    {
        if (_currentTarget == null)
            return;

        if (_weapon == null)
            return;

        if (_weapon.CurrentWeapon == null)
        {
            _agent.SetDestination(_currentTarget.position);
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            _currentTarget.position);

        if (distance > _weapon.CurrentWeapon.attackRange)
        {
            Vector3 direction =
                (_currentTarget.position - transform.position).normalized;

            Vector3 targetPosition =
                _currentTarget.position - direction * 1.5f;

            _agent.SetDestination(targetPosition);
        }
        else
        {
            _agent.ResetPath();

            RotateTowardsTarget();

            _attackTimer -= Time.deltaTime;

            if (_attackTimer <= 0f)
            {
                Attack();
                _attackTimer = 1f / _weapon.CurrentWeapon.attackRate;
            }
        }
    }

    private void RotateTowardsTarget()
    {
        Vector3 direction = (
            _currentTarget.position - transform.position).normalized;

        direction.y = 0f;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }

    private void Attack()
    {
        if (_currentTarget == null)
            return;

        WeaponData weapon =
            _weapon.CurrentWeapon;

        Projectile projectile = Instantiate(
            weapon.projectilePrefab,
            shootPoint.position,
            Quaternion.identity);

        projectile.Initialize(
            _currentTarget,
            weapon.damage);
    }
}