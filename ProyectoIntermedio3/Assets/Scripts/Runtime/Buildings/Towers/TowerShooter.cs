using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Tower))]
public class TowerShooter : MonoBehaviour
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
    [SerializeField] private float fireAlignmentAngle = 10f;

    #endregion

    #region Constants

    private const string DefaultAimPivotName = "Turret";
    private const string DefaultShootPointName = "ShootPoint";

    #endregion

    #region Runtime State

    private readonly Collider[] _hits = new Collider[64];
    private readonly Collider[] _clusterHits = new Collider[64];
    private readonly HashSet<ITargetable> _splashTargets = new();
    private readonly HashSet<ITargetable> _clusterTargets = new();
    private Tower _tower;
    private BuildingVisualController _visualController;
    private TowerLaunchSockets _launchSockets;
    private CannonCatapultAnimator _cannonAnimator;
    private Transform _resolvedAimPivot;
    private Transform[] _resolvedShootPoints;
    private float _cooldown;
    private Quaternion _aimPivotRestRotation;
    private float _idleSeed;
    private bool _isGamePaused;
    private bool _fireSequenceInProgress;
    private ITargetable _sequenceTarget;

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

        GamePauseEvents.OnGamePaused += HandleGamePaused;
        GamePauseEvents.OnGameResumed += HandleGameResumed;
        _isGamePaused = GamePauseEvents.IsPaused;
    }

    private void OnDisable()
    {
        if (_visualController != null)
            _visualController.OnVisualChanged -= HandleVisualChanged;

        GamePauseEvents.OnGamePaused -= HandleGamePaused;
        GamePauseEvents.OnGameResumed -= HandleGameResumed;
    }

    #endregion

    #region Event Handlers

    private void HandleVisualChanged(Transform visualRoot)
    {
        ResolveVisualReferences();
    }

    private void HandleGamePaused()
    {
        _isGamePaused = true;
    }

    private void HandleGameResumed()
    {
        _isGamePaused = false;
    }

    #endregion

    #region Main Update Loop

    private void Update()
    {
        if (_isGamePaused) return;
        if (_tower == null || !_tower.IsAlive) return;

        var target = _fireSequenceInProgress && _sequenceTarget != null && _sequenceTarget.IsAlive
            ? _sequenceTarget
            : FindTarget();
        UpdateAim(target);

        if (_tower.FireRate <= 0f) return;

        _cooldown -= Time.deltaTime;
        if (_fireSequenceInProgress) return;
        if (_cooldown > 0f) return;
        if (target == null) return;
        if (!IsAlignedWith(target)) return;

        if (FireAt(target))
            _cooldown = 1f / _tower.FireRate;
    }

    #endregion

    #region Target Selection

    private ITargetable FindTarget()
    {
        var cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        var half = (_tower.AttackRange + 0.5f) * cellSize;
        var count = Physics.OverlapBoxNonAlloc(transform.position, new Vector3(half, half, half), _hits, Quaternion.identity, targetMask, QueryTriggerInteraction.Ignore);
        var bestTarget = default(ITargetable);
        var bestDistanceSqr = float.MaxValue;
        var bestClusterSize = int.MinValue;
        var bestIcePriority = float.MinValue;

        for (var index = 0; index < count; index++)
        {
            var hit = _hits[index];
            if (hit == null) continue;

            var target = hit.GetComponentInParent<ITargetable>();
            if (target == null || !target.IsAlive) continue;

            var distanceSqr = (hit.transform.position - transform.position).sqrMagnitude;
            switch (_tower.Role)
            {
                case TowerRole.Cannon:
                {
                    var clusterSize = EstimateClusterSize(hit.transform.position);
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
                    var icePriority = EvaluateIcePriority(target, distanceSqr);
                    var isBetter = icePriority > bestIcePriority + 0.0001f
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

        if (target != null && TryGetTargetPosition(target, out var targetPosition))
        {
            RotateToward(targetPosition);
            return;
        }

        SweepIdle();
    }

    private void RotateToward(Vector3 targetPosition)
    {
        var direction = targetPosition - _resolvedAimPivot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f) return;

        var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        _resolvedAimPivot.rotation = Quaternion.RotateTowards(_resolvedAimPivot.rotation, targetRotation, targetTurnSpeed * Time.deltaTime);
    }

    private void SweepIdle()
    {
        var phase = (Time.time + _idleSeed) * Mathf.PI * 2f * idleSweepFrequency;
        var yaw = Mathf.Sin(phase) * idleSweepAngle;
        var targetRotation = _aimPivotRestRotation * Quaternion.Euler(0f, yaw, 0f);
        _resolvedAimPivot.localRotation = Quaternion.RotateTowards(_resolvedAimPivot.localRotation, targetRotation, idleTurnSpeed * Time.deltaTime);
    }

    private bool IsAlignedWith(ITargetable target)
    {
        if (_resolvedAimPivot == null || fireAlignmentAngle <= 0f) return true;
        if (!TryGetTargetPosition(target, out var targetPosition)) return true;

        var direction = targetPosition - _resolvedAimPivot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f) return true;

        return Vector3.Angle(_resolvedAimPivot.forward, direction) <= fireAlignmentAngle;
    }

    #endregion

    #region Firing

    private bool FireAt(ITargetable target)
    {
        if (TryStartCannonSequence(target))
            return true;

        FireNow(target);
        return true;
    }

    private void FireNow(ITargetable target)
    {
        if (target == null || !target.IsAlive) return;

        var projectileCount = Mathf.Max(1, _tower.ProjectilesPerAttack);

        for (var projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
        {
            var launchPoint = GetLaunchPoint(projectileIndex);
            PlayShootEffects(launchPoint);
            PlayIceBeamIfNeeded(target, launchPoint);

            if (projectilePrefab != null && target is Component)
                SpawnProjectile(target, launchPoint);
            else
                ApplyImpact(target);
        }
    }

    private void PlayIceBeamIfNeeded(ITargetable target, Transform launchPoint)
    {
        if (_tower == null || _tower.Role != TowerRole.Ice) return;
        if (!TryGetTargetPosition(target, out var targetPosition)) return;

        var startPosition = ResolveIceBeamOrigin(launchPoint, targetPosition);
        var endPosition = targetPosition + Vector3.up * 0.55f;
        IceBeamEffect.Play(startPosition, endPosition);
    }

    private Vector3 ResolveIceBeamOrigin(Transform launchPoint, Vector3 targetPosition)
    {
        if (launchPoint != null && launchPoint != transform)
            return launchPoint.position;

        var bounds = CalculateVisibleBounds();
        var origin = bounds.center;
        origin.y = bounds.max.y - bounds.size.y * 0.18f;

        var direction = targetPosition - origin;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            origin += direction.normalized * Mathf.Min(0.35f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.55f);

        return origin;
    }

    private Bounds CalculateVisibleBounds()
    {
        var bounds = new Bounds(transform.position, Vector3.one);
        var hasBounds = false;
        var searchRoot = GetVisualSearchRoot();

        foreach (var renderer in searchRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                continue;

            if (renderer.GetComponentInParent<BuildingHealthBar>() != null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private bool TryStartCannonSequence(ITargetable target)
    {
        if (_cannonAnimator == null || _cannonAnimator.IsPlaying)
            return false;

        var launchPoint = GetLaunchPoint(0);
        _cannonAnimator.Configure(projectilePrefab, launchPoint, GetVisualSearchRoot());
        if (!_cannonAnimator.CanAnimate)
            return false;

        _fireSequenceInProgress = true;
        _sequenceTarget = target;
        var started = _cannonAnimator.Play(() => FireNow(target), () =>
            {
                _fireSequenceInProgress = false;
                _sequenceTarget = null;
            });

        if (!started)
        {
            _fireSequenceInProgress = false;
            _sequenceTarget = null;
        }

        return started;
    }

    private void SpawnProjectile(ITargetable target, Transform launchPoint)
    {
        launchPoint = launchPoint != null ? launchPoint : transform;
        var projectileObject = Instantiate(projectilePrefab, launchPoint.position, launchPoint.rotation);
        var projectile = projectileObject.GetComponent<TowerProjectile>();
        if (projectile == null)
            projectile = projectileObject.AddComponent<TowerProjectile>();

        projectile.Launch(target, _tower.ProjectileSpeed, _tower.AttackDamage, _tower.SplashRadius, _tower.SlowPercent, _tower.SlowDuration, targetMask);
    }

    private void PlayShootEffects(Transform launchPoint)
    {
        if (launchPoint == null) return;

        var shootEffects = launchPoint.GetComponentsInChildren<ParticleSystem>(true);
        for (var index = 0; index < shootEffects.Length; index++)
        {
            var effect = shootEffects[index];
            if (effect == null) continue;

            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Play(true);
        }
    }

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
        var boxCenter = SnapToGridCenter(center);
        var cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        var half = (_tower.SplashRadius + 0.5f) * cellSize;
        var count = Physics.OverlapBoxNonAlloc(boxCenter, new Vector3(half, half, half), _hits, Quaternion.identity, targetMask, QueryTriggerInteraction.Ignore);

        for (var index = 0; index < count; index++)
        {
            var hit = _hits[index];
            if (hit == null) continue;

            var target = hit.GetComponentInParent<ITargetable>();
            if (target == null || !target.IsAlive || !_splashTargets.Add(target)) continue;
            target.TakeDamage(_tower.AttackDamage);
            if (target.IsAlive) ApplySlow(target);
        }
    }

    private static Vector3 SnapToGridCenter(Vector3 worldPosition)
    {
        var grid = GridManager.Instance;
        return grid != null ? grid.GridToWorld(grid.WorldToGrid(worldPosition)) : worldPosition;
    }

    private void ApplySlow(ITargetable target)
    {
        if (_tower.SlowPercent <= 0f || _tower.SlowDuration <= 0f) return;
        if (target is ISlowable slowable)
            slowable.ApplySlow(_tower.SlowPercent, _tower.SlowDuration);
    }

    private int EstimateClusterSize(Vector3 center)
    {
        if (_tower == null || _tower.SplashRadius <= 0)
            return 1;

        _clusterTargets.Clear();
        var boxCenter = SnapToGridCenter(center);
        var cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        var half = (_tower.SplashRadius + 0.5f) * cellSize;
        var count = Physics.OverlapBoxNonAlloc(boxCenter, new Vector3(half, half, half), _clusterHits, Quaternion.identity, targetMask, QueryTriggerInteraction.Ignore);

        for (var index = 0; index < count; index++)
        {
            var hit = _clusterHits[index];
            if (hit == null) continue;

            var target = hit.GetComponentInParent<ITargetable>();
            if (target != null && target.IsAlive)
                _clusterTargets.Add(target);
        }

        return Mathf.Max(1, _clusterTargets.Count);
    }

    private static float EvaluateIcePriority(ITargetable target, float distanceSqr)
    {
        var unslowedPriority = 0f;
        if (target is ISlowable slowable)
            unslowedPriority = Mathf.Clamp01(slowable.MoveSpeedMultiplier);

        return unslowedPriority * 1000f - distanceSqr;
    }

    #endregion

    #region Reference Resolution

    private void ResolveVisualReferences()
    {
        var searchRoot = GetVisualSearchRoot();

        _launchSockets = searchRoot.GetComponentInChildren<TowerLaunchSockets>(true);
        _resolvedAimPivot = ResolveAimPivot(searchRoot);
        _resolvedShootPoints = ResolveShootPoints(searchRoot);
        ResolveCannonAnimator(searchRoot);

        if (_resolvedAimPivot != null)
            _aimPivotRestRotation = _resolvedAimPivot.localRotation;
    }

    private Transform GetVisualSearchRoot()
    {
        var visualRoot = _visualController != null ? _visualController.ActiveVisualRoot : null;
        return visualRoot != null ? visualRoot : transform;
    }

    private void ResolveCannonAnimator(Transform searchRoot)
    {
        if (searchRoot == null || FindChildRecursive(searchRoot, "CatapultG_Arm02") == null)
            return;

        if (_cannonAnimator == null && !TryGetComponent(out _cannonAnimator))
            _cannonAnimator = gameObject.AddComponent<CannonCatapultAnimator>();

        var launchPoint = GetLaunchPoint(0);
        _cannonAnimator.Configure(projectilePrefab, launchPoint, searchRoot);
    }

    private Transform ResolveAimPivot(Transform searchRoot)
    {
        if (_launchSockets != null && _launchSockets.AimPivot != null)
            return _launchSockets.AimPivot;

        var visualMatch = FindChildRecursive(searchRoot, DefaultAimPivotName);
        if (visualMatch != null) return visualMatch;

        if (aimPivot != null) return aimPivot;

        return FindChildRecursive(transform, DefaultAimPivotName);
    }

    private Transform[] ResolveShootPoints(Transform searchRoot)
    {
        if (_launchSockets != null && _launchSockets.ShootPointCount > 0)
            return null;

        var visualShootPoints = TowerLaunchSockets.FindChildrenRecursive(searchRoot, DefaultShootPointName);
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

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        for (var index = 0; index < parent.childCount; index++)
        {
            var child = parent.GetChild(index);
            if (child.name == childName) return child;

            var match = FindChildRecursive(child, childName);
            if (match != null) return match;
        }

        return null;
    }

    #endregion
}
