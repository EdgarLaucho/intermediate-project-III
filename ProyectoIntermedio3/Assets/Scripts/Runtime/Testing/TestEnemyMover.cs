using UnityEngine;

[RequireComponent(typeof(EnemyStub))]
public sealed class TestEnemyMover : MonoBehaviour
{
    [SerializeField] private string nexusTag = "Nexus";
    [SerializeField] private float reachDistance = 0.35f;
    [SerializeField] private float turnSpeed = 720f;
    [SerializeField] private bool attackTowers = true;
    [SerializeField] private float towerSearchRadius = 18f;
    [SerializeField] private float towerSearchInterval = 0.25f;
    [SerializeField] private float attackRange = 0.9f;
    [SerializeField] private float attackInterval = 1f;

    private EnemyStub _enemy;
    private Transform _nexusTarget;
    private Tower _towerTarget;
    private float _nextTowerSearchTime;
    private float _nextAttackTime;

    private void Awake()
    {
        _enemy = GetComponent<EnemyStub>();
        FindNexus();
    }

    private void Update()
    {
        if (_enemy == null || !_enemy.IsAlive) return;

        RefreshTowerTarget();
        if (HasLiveTowerTarget())
        {
            MoveOrAttackTower();
            return;
        }

        if (_nexusTarget == null && !FindNexus()) return;
        MoveOrHitNexus();
    }

    private void MoveOrAttackTower()
    {
        Vector3 targetPosition = _towerTarget.transform.position;
        targetPosition.y = transform.position.y;

        Vector3 offset = targetPosition - transform.position;
        float stopDistance = Mathf.Max(0.1f, attackRange);
        if (offset.magnitude > stopDistance)
        {
            MoveToward(targetPosition);
            return;
        }

        Face(offset);
        AttackTower();
    }

    private void MoveOrHitNexus()
    {
        Vector3 targetPosition = _nexusTarget.position;
        targetPosition.y = transform.position.y;

        Vector3 offset = targetPosition - transform.position;
        if (offset.magnitude <= reachDistance)
        {
            HitNexus();
            return;
        }

        MoveToward(targetPosition);
    }

    private void MoveToward(Vector3 targetPosition)
    {
        targetPosition.y = transform.position.y;

        Vector3 offset = targetPosition - transform.position;
        Vector3 direction = offset.normalized;
        float speed = _enemy.EffectiveMoveSpeed;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        Face(direction);
    }

    private void Face(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }

    private void RefreshTowerTarget()
    {
        if (!attackTowers || HasLiveTowerTarget()) return;
        if (Time.time < _nextTowerSearchTime) return;

        _nextTowerSearchTime = Time.time + Mathf.Max(0.05f, towerSearchInterval);
        _towerTarget = FindClosestTower();
    }

    private bool HasLiveTowerTarget()
    {
        if (_towerTarget == null) return false;
        if (_towerTarget.IsAlive) return true;

        _towerTarget = null;
        return false;
    }

    private Tower FindClosestTower()
    {
        Tower closest = null;
        float bestSqrDistance = towerSearchRadius > 0f
            ? towerSearchRadius * towerSearchRadius
            : float.PositiveInfinity;

        Tower[] towers = FindObjectsByType<Tower>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Tower tower in towers)
        {
            if (tower == null || !tower.IsAlive) continue;

            float sqrDistance = HorizontalSqrDistance(transform.position, tower.transform.position);
            if (sqrDistance <= bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                closest = tower;
            }
        }

        return closest;
    }

    private void AttackTower()
    {
        if (Time.time < _nextAttackTime || !HasLiveTowerTarget()) return;

        _nextAttackTime = Time.time + Mathf.Max(0.05f, attackInterval);
        int damage = _enemy.ContactDamage;
        if (damage <= 0) return;

        _towerTarget.TakeDamage(damage);
        if (!_towerTarget.IsAlive)
            _towerTarget = null;
    }

    private bool FindNexus()
    {
        GameObject nexus = null;

        try
        {
            nexus = GameObject.FindGameObjectWithTag(nexusTag);
        }
        catch (UnityException)
        {
            Debug.LogWarning($"[TestEnemyMover] Tag '{nexusTag}' does not exist.");
        }

        _nexusTarget = nexus != null ? nexus.transform : null;
        return _nexusTarget != null;
    }

    private void HitNexus()
    {
        IDamageable damageable = _nexusTarget.GetComponentInParent<IDamageable>();
        if (damageable != null && damageable.IsAlive && _enemy.ContactDamage > 0)
            damageable.TakeDamage(_enemy.ContactDamage);

        Destroy(gameObject);
    }

    private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return (a - b).sqrMagnitude;
    }
}