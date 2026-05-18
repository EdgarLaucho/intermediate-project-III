using System.Collections.Generic;
using UnityEngine;

// Drives the per-frame combat loop for a tower: find the closest target, rotate to
// face it, and fire when aligned. All stats are read from the sibling Tower component
// so this class only handles the "how to shoot" mechanics.
[RequireComponent(typeof(Tower))]
public sealed class TowerShooter : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private Transform aimPivot;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float idleSweepAngle = 35f;
    [SerializeField] private float idleSweepFrequency = 0.35f;
    [SerializeField] private float idleTurnSpeed = 90f;
    [SerializeField] private float targetTurnSpeed = 420f;
    // The turret must be within this angle of the target before it may fire,
    // preventing shots that would clearly miss due to rotation lag.
    [SerializeField] private float fireAlignmentAngle = 10f;

    #endregion

    #region Constants

    private const string DefaultAimPivotName = "Turret";
    private const string DefaultShootPointName = "ShootPoint";

    #endregion

    #region Runtime State

    // Pre-allocated overlap buffer to avoid heap allocations every frame.
    private readonly Collider[] _hits = new Collider[64];
    // Secondary buffer reused for cannon splash-evaluation queries.
    private readonly Collider[] _clusterHits = new Collider[64];
    // Reused by the instant-hit splash path (no projectile).
    private readonly HashSet<ITargetable> _splashTargets = new();
    // Reused while scoring clustered targets for cannon towers.
    private readonly HashSet<ITargetable> _clusterTargets = new();
    private Tower _tower;
    private BuildingVisualController _visualController;
    private TowerLaunchSockets _launchSockets;
    private Transform _resolvedAimPivot;
    private Transform[] _resolvedShootPoints;
    private float _cooldown;
    // Captured when the visual is resolved so idle sweep rotates relative to the
    // turret's rest orientation, not world zero.
    private Quaternion _aimPivotRestRotation;
    // Per-instance seed so towers placed at the same time sweep out of sync.
    private float _idleSeed;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        _tower = GetComponent<Tower>();
        _visualController = GetComponent<BuildingVisualController>();
        _idleSeed = Random.Range(0f, 100f);
        ResolveVisualReferences();
    }

    private void OnEnable()
    {
        if (_visualController == null)
            _visualController = GetComponent<BuildingVisualController>();

        if (_visualController != null)
            _visualController.OnVisualChanged += HandleVisualChanged;
    }

    private void OnDisable()
    {
        if (_visualController != null)
            _visualController.OnVisualChanged -= HandleVisualChanged;
    }

    #endregion

    #region Event Handlers

    // Re-resolve references whenever the visual mesh is swapped so aim pivot and
    // shoot points always belong to the currently active model.
    private void HandleVisualChanged(Transform visualRoot)
    {
        ResolveVisualReferences();
    }

    #endregion

    #region Main Update Loop

    private void Update()
    {
        if (_tower == null || !_tower.IsAlive) return;

        ITargetable target = FindTarget();
        UpdateAim(target);

        if (_tower.FireRate <= 0f) return;

        _cooldown -= Time.deltaTime;
        if (_cooldown > 0f) return;
        if (target == null) return;
        if (!IsAlignedWith(target)) return;

        FireAt(target);
        _cooldown = 1f / _tower.FireRate;
    }

    #endregion

    #region Target Selection

    // Selects the target with the smallest squared distance inside AttackRange.
    // OverlapBoxNonAlloc uses Chebyshev (square) distance matching the grid layout;
    // half-extent = (cellRadius + 0.5) * cellSize covers exactly the targeted cells.
    private ITargetable FindTarget()
    {
        float cs = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        float half = (_tower.AttackRange + 0.5f) * cs;
        int count = Physics.OverlapBoxNonAlloc(transform.position, new Vector3(half, half, half), _hits, Quaternion.identity, targetMask, QueryTriggerInteraction.Ignore);
        ITargetable bestTarget = null;
        float bestDistanceSqr = float.MaxValue;
        int bestClusterSize = int.MinValue;
        float bestIcePriority = float.MinValue;

        for (int index = 0; index < count; index++)
        {
            Collider hit = _hits[index];
            if (hit == null) continue;

            ITargetable target = hit.GetComponentInParent<ITargetable>();
            if (target == null || !target.IsAlive) continue;

            float distanceSqr = (hit.transform.position - transform.position).sqrMagnitude;
            switch (_tower.Role)
            {
                case TowerRole.Cannon:
                {
                    int clusterSize = EstimateClusterSize(hit.transform.position);
                    if (clusterSize > bestClusterSize
                        || (clusterSize == bestClusterSize && distanceSqr < bestDistanceSqr))
                    {
                        bestClusterSize = clusterSize;
                        bestDistanceSqr = distanceSqr;
                        bestTarget = target;
                    }
                    break;
                }
                case TowerRole.Ice:
                {
                    float icePriority = EvaluateIcePriority(target, distanceSqr);
                    bool isBetter = icePriority > bestIcePriority + 0.0001f
                        || (Mathf.Abs(icePriority - bestIcePriority) <= 0.0001f && distanceSqr < bestDistanceSqr);
                    if (isBetter)
                    {
                        bestIcePriority = icePriority;
                        bestDistanceSqr = distanceSqr;
                        bestTarget = target;
                    }
                    break;
                }
                default:
                {
                    if (distanceSqr >= bestDistanceSqr) continue;

                    bestDistanceSqr = distanceSqr;
                    bestTarget = target;
                    break;
                }
            }
        }

        return bestTarget;
    }

    #endregion

    #region Aiming

    private void UpdateAim(ITargetable target)
    {
        if (_resolvedAimPivot == null) return;

        if (target != null && TryGetTargetPosition(target, out Vector3 targetPosition))
        {
            RotateToward(targetPosition);
            return;
        }

        SweepIdle();
    }

    private void RotateToward(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - _resolvedAimPivot.position;
        direction.y = 0f; // Keep rotation horizontal – no pitch toward the target.
        if (direction.sqrMagnitude <= 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        _resolvedAimPivot.rotation = Quaternion.RotateTowards(_resolvedAimPivot.rotation, targetRotation, targetTurnSpeed * Time.deltaTime);
    }

    // Oscillates the turret back and forth while idle so it looks alive.
    // Sine wave on Time.time with a per-tower seed avoids synchronized sweeping.
    private void SweepIdle()
    {
        float phase = (Time.time + _idleSeed) * Mathf.PI * 2f * idleSweepFrequency;
        float yaw = Mathf.Sin(phase) * idleSweepAngle;
        Quaternion targetRotation = _aimPivotRestRotation * Quaternion.Euler(0f, yaw, 0f);
        _resolvedAimPivot.localRotation = Quaternion.RotateTowards(_resolvedAimPivot.localRotation, targetRotation, idleTurnSpeed * Time.deltaTime);
    }

    private bool IsAlignedWith(ITargetable target)
    {
        if (_resolvedAimPivot == null || fireAlignmentAngle <= 0f) return true;
        if (!TryGetTargetPosition(target, out Vector3 targetPosition)) return true;

        Vector3 direction = targetPosition - _resolvedAimPivot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f) return true;

        return Vector3.Angle(_resolvedAimPivot.forward, direction) <= fireAlignmentAngle;
    }

    #endregion

    #region Firing

    private void FireAt(ITargetable target)
    {
        int projectileCount = Mathf.Max(1, _tower.ProjectilesPerAttack);

        for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
        {
            Transform launchPoint = GetLaunchPoint(projectileIndex);
            PlayShootEffects(launchPoint);

            if (projectilePrefab != null && target is Component)
                SpawnProjectile(target, launchPoint);
            else
                ApplyImpact(target); // Instant-hit fallback when no projectile prefab is set.
        }
    }

    private void SpawnProjectile(ITargetable target, Transform launchPoint)
    {
        launchPoint = launchPoint != null ? launchPoint : transform;
        GameObject projectileObject = Instantiate(projectilePrefab, launchPoint.position, launchPoint.rotation);
        TowerProjectile projectile = projectileObject.GetComponent<TowerProjectile>();
        if (projectile == null)
            projectile = projectileObject.AddComponent<TowerProjectile>();

        projectile.Launch(target, _tower.ProjectileSpeed, _tower.AttackDamage, _tower.SplashRadius, _tower.SlowPercent, _tower.SlowDuration, targetMask);
    }

    private void PlayShootEffects(Transform launchPoint)
    {
        if (launchPoint == null) return;

        ParticleSystem[] shootEffects = launchPoint.GetComponentsInChildren<ParticleSystem>(true);
        for (int index = 0; index < shootEffects.Length; index++)
        {
            ParticleSystem effect = shootEffects[index];
            if (effect == null) continue;

            // Stop-and-clear before playing so rapid fire resets the effect cleanly.
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Play(true);
        }
    }

    // Instant-hit fallback: applies damage directly without spawning a projectile.
    private void ApplyImpact(ITargetable target)
    {
        if (_tower.SplashRadius > 0 && target is Component targetComponent)
        {
            DamageSplash(targetComponent.transform.position);
            return;
        }

        target.TakeDamage(_tower.AttackDamage);
        if (target.IsAlive) ApplySlow(target);
    }

    private void DamageSplash(Vector3 center)
    {
        _splashTargets.Clear();
        Vector3 boxCenter = SnapToGridCenter(center);
        float cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        float half = (_tower.SplashRadius + 0.5f) * cellSize;
        int count = Physics.OverlapBoxNonAlloc(boxCenter, new Vector3(half, half, half), _hits, Quaternion.identity, targetMask, QueryTriggerInteraction.Ignore);

        for (int index = 0; index < count; index++)
        {
            Collider hit = _hits[index];
            if (hit == null) continue;

            ITargetable target = hit.GetComponentInParent<ITargetable>();
            if (target == null || !target.IsAlive || !_splashTargets.Add(target)) continue;
            target.TakeDamage(_tower.AttackDamage);
            if (target.IsAlive) ApplySlow(target);
        }
    }

    private static Vector3 SnapToGridCenter(Vector3 worldPosition)
    {
        GridManager grid = GridManager.Instance;
        return grid != null ? grid.GridToWorld(grid.WorldToGrid(worldPosition)) : worldPosition;
    }

    private void ApplySlow(ITargetable target)
    {
        if (_tower.SlowPercent <= 0f || _tower.SlowDuration <= 0f) return;
        if (target is ISlowable slowable)
            slowable.ApplySlow(_tower.SlowPercent, _tower.SlowDuration);
    }

    // Cannon towers are most valuable when they splash multiple enemies at once.
    // When no splash radius is configured, fall back to single-target behaviour.
    private int EstimateClusterSize(Vector3 center)
    {
        if (_tower == null || _tower.SplashRadius <= 0)
            return 1;

        _clusterTargets.Clear();
        Vector3 boxCenter = SnapToGridCenter(center);
        float cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        float half = (_tower.SplashRadius + 0.5f) * cellSize;
        int count = Physics.OverlapBoxNonAlloc(boxCenter, new Vector3(half, half, half), _clusterHits, Quaternion.identity, targetMask, QueryTriggerInteraction.Ignore);

        for (int index = 0; index < count; index++)
        {
            Collider hit = _clusterHits[index];
            if (hit == null) continue;

            ITargetable target = hit.GetComponentInParent<ITargetable>();
            if (target != null && target.IsAlive)
                _clusterTargets.Add(target);
        }

        return Mathf.Max(1, _clusterTargets.Count);
    }

    // Ice towers prefer enemies that are still moving at (or near) full speed so
    // they spread the slow across the wave instead of overcommitting to one target.
    private static float EvaluateIcePriority(ITargetable target, float distanceSqr)
    {
        float unslowedPriority = 0f;
        if (target is ISlowable slowable)
            unslowedPriority = Mathf.Clamp01(slowable.MoveSpeedMultiplier);

        // Prioritise fresh targets first, then use distance as a gentle tie-breaker.
        return unslowedPriority * 1000f - distanceSqr;
    }

    #endregion

    #region Reference Resolution

    // Called on Awake and again whenever the visual mesh changes.
    // Priority order: TowerLaunchSockets on the visual > named child in visual > Inspector field > named child on root.
    private void ResolveVisualReferences()
    {
        Transform visualRoot = _visualController != null ? _visualController.ActiveVisualRoot : null;
        Transform searchRoot = visualRoot != null ? visualRoot : transform;

        _launchSockets = searchRoot.GetComponentInChildren<TowerLaunchSockets>(true);
        _resolvedAimPivot = ResolveAimPivot(searchRoot);
        _resolvedShootPoints = ResolveShootPoints(searchRoot);

        if (_resolvedAimPivot != null)
            _aimPivotRestRotation = _resolvedAimPivot.localRotation;
    }

    private Transform ResolveAimPivot(Transform searchRoot)
    {
        if (_launchSockets != null && _launchSockets.AimPivot != null)
            return _launchSockets.AimPivot;

        Transform visualMatch = FindChildRecursive(searchRoot, DefaultAimPivotName);
        if (visualMatch != null) return visualMatch;

        if (aimPivot != null) return aimPivot;

        return FindChildRecursive(transform, DefaultAimPivotName);
    }

    private Transform[] ResolveShootPoints(Transform searchRoot)
    {
        if (_launchSockets != null && _launchSockets.ShootPointCount > 0)
            return null; // TowerLaunchSockets will be used directly; no array needed.

        Transform[] visualShootPoints = TowerLaunchSockets.FindChildrenRecursive(searchRoot, DefaultShootPointName);
        if (visualShootPoints.Length > 0) return visualShootPoints;

        if (shootPoint != null) return new[] { shootPoint };

        return null;
    }

    private Transform GetLaunchPoint(int projectileIndex)
    {
        if (_launchSockets != null && _launchSockets.ShootPointCount > 0)
            return _launchSockets.GetShootPoint(projectileIndex);

        if (_resolvedShootPoints != null && _resolvedShootPoints.Length > 0)
            return _resolvedShootPoints[projectileIndex % _resolvedShootPoints.Length];

        return shootPoint != null ? shootPoint : transform;
    }

    #endregion

    #region Static Helpers

    private static bool TryGetTargetPosition(ITargetable target, out Vector3 position)
    {
        if (target is Component component)
        {
            position = component.transform.position;
            return true;
        }

        position = default;
        return false;
    }

    // Local recursive search used for aim pivot resolution.
    // TowerLaunchSockets exposes the same algorithm as a public static for reuse.
    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == childName) return child;

            Transform match = FindChildRecursive(child, childName);
            if (match != null) return match;
        }

        return null;
    }

    #endregion
}
