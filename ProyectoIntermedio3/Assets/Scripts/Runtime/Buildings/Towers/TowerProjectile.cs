using System.Collections.Generic;
using UnityEngine;

public sealed class TowerProjectile : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private float impactDistance = 0.18f;
    [SerializeField] private float maxLifetime = 5f;
    [SerializeField] private float turnSpeed = 900f;
    [SerializeField] private float arcHeight = 1.15f;
    [SerializeField] private float arcHeightPerDistance = 0.16f;
    [SerializeField] private float minFlightDuration = 0.18f;

    #endregion

    #region Runtime State
    private readonly Collider[] _hits = new Collider[64];
    private readonly HashSet<ITargetable> _splashTargets = new();

    private ITargetable _target;
    private Component _targetComponent;
    private LayerMask _targetMask;
    private float _speed;
    private int _damage;
    private int _splashRadius;
    private float _slowPercent;
    private float _slowDuration;
    private float _age;
    private Vector3 _startPosition;
    private float _flightDuration;
    private float _arcPeakHeight;
    private float _progress;
    private bool _launched;
    private bool _impacted;
    private bool _isGamePaused;

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        GamePauseEvents.OnGamePaused += HandleGamePaused;
        GamePauseEvents.OnGameResumed += HandleGameResumed;
        _isGamePaused = GamePauseEvents.IsPaused;
    }

    private void OnDisable()
    {
        GamePauseEvents.OnGamePaused -= HandleGamePaused;
        GamePauseEvents.OnGameResumed -= HandleGameResumed;
    }

    #endregion

    #region Event Handlers

    private void HandleGamePaused()
    {
        _isGamePaused = true;
    }

    private void HandleGameResumed()
    {
        _isGamePaused = false;
    }

    #endregion

    #region Public API

    public void Launch(ITargetable target, float speed, int damage, int splashRadius, float slowPercent, float slowDuration, LayerMask targetMask)
    {
        _target = target;
        _targetComponent = target as Component;
        _speed = Mathf.Max(0.01f, speed);
        _damage = damage;
        _splashRadius = splashRadius;
        _slowPercent = slowPercent;
        _slowDuration = slowDuration;
        _targetMask = targetMask;
        _startPosition = transform.position;
        _progress = 0f;
        _age = 0f;
        ConfigureArc();
        _launched = true;
        _impacted = false;
        _isGamePaused = GamePauseEvents.IsPaused;
    }

    #endregion

    #region Flight Update

    private void Update()
    {
        if (_isGamePaused) return;
        if (!_launched || _impacted) return;

        _age += Time.deltaTime;
        if (_age >= maxLifetime || _target == null || !_target.IsAlive || _targetComponent == null)
        {
            Destroy(gameObject);
            return;
        }

        var targetPosition = GetTargetPosition(_targetComponent);
        var previousPosition = transform.position;

        _progress = Mathf.Clamp01(_progress + Time.deltaTime / _flightDuration);

        var nextPosition = EvaluateArcPosition(targetPosition, _progress);
        transform.position = nextPosition;

        var velocity = nextPosition - previousPosition;
        if (velocity.sqrMagnitude > 0.0001f)
        {
            var targetRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        if (_progress >= 1f || Vector3.Distance(nextPosition, targetPosition) <= impactDistance)
            Impact(targetPosition);
    }

    #endregion

    #region Arc Math

    private void ConfigureArc()
    {
        var targetPosition = _targetComponent != null ? GetTargetPosition(_targetComponent) : _startPosition;
        var distance = Vector3.Distance(_startPosition, targetPosition);
        _flightDuration = Mathf.Max(0.01f, Mathf.Max(minFlightDuration, distance / _speed));
        _arcPeakHeight = Mathf.Max(0f, arcHeight + distance * arcHeightPerDistance);
    }

    private Vector3 EvaluateArcPosition(Vector3 targetPosition, float progress)
    {
        var position = Vector3.Lerp(_startPosition, targetPosition, progress);
        position.y += Mathf.Sin(progress * Mathf.PI) * _arcPeakHeight;
        return position;
    }

    #endregion

    #region Impact & Damage

    private void Impact(Vector3 impactPosition)
    {
        if (_impacted) return;
        _impacted = true;

        if (_splashRadius > 0)
            DamageSplash(impactPosition);
        else
            DamageTarget(_target);

        Destroy(gameObject);
    }

    private void DamageSplash(Vector3 center)
    {
        _splashTargets.Clear();
        var boxCenter = SnapToGridCenter(center);
        var cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        var half = (_splashRadius + 0.5f) * cellSize;
        var count = Physics.OverlapBoxNonAlloc(boxCenter, new Vector3(half, half, half), _hits, Quaternion.identity, _targetMask, QueryTriggerInteraction.Ignore);

        for (int index = 0; index < count; index++)
        {
            var hit = _hits[index];
            if (hit == null) continue;

            var target = hit.GetComponentInParent<ITargetable>();
            if (target == null || !target.IsAlive || !_splashTargets.Add(target)) continue;
            DamageTarget(target);
        }
    }

    private static Vector3 SnapToGridCenter(Vector3 worldPosition)
    {
        var grid = GridManager.Instance;
        return grid != null ? grid.GridToWorld(grid.WorldToGrid(worldPosition)) : worldPosition;
    }

    private void DamageTarget(ITargetable target)
    {
        if (target == null || !target.IsAlive) return;

        target.TakeDamage(_damage);
        if (target.IsAlive) ApplySlow(target);
    }

    private void ApplySlow(ITargetable target)
    {
        if (_slowPercent <= 0f || _slowDuration <= 0f) return;
        if (target is ISlowable slowable)
            slowable.ApplySlow(_slowPercent, _slowDuration);
    }

    #endregion

    #region Static Helpers

    private static Vector3 GetTargetPosition(Component targetComponent)
    {
        var targetCollider = targetComponent.GetComponentInChildren<Collider>();
        return targetCollider != null ? targetCollider.bounds.center : targetComponent.transform.position;
    }

    #endregion
}