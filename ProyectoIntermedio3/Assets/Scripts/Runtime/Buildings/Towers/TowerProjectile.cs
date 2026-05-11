using System.Collections.Generic;
using UnityEngine;

// A self-contained projectile that arcs toward a moving target and delivers
// damage (and an optional slow) on impact. The arc is computed at launch time
// so the projectile follows a smooth parabola even as the target moves.
public sealed class TowerProjectile : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private float impactDistance = 0.18f;
    [SerializeField] private float maxLifetime = 5f;
    [SerializeField] private float turnSpeed = 900f;
    [SerializeField] private float arcHeight = 1.15f;
    // Extra arc height added per unit of horizontal distance to the target, so
    // distant targets get a more pronounced arc that looks natural.
    [SerializeField] private float arcHeightPerDistance = 0.16f;
    [SerializeField] private float minFlightDuration = 0.18f;

    #endregion

    #region Runtime State

    // Reusable overlap buffer – avoids per-frame heap allocations for splash damage.
    private readonly Collider[] _hits = new Collider[64];
    // HashSet prevents the same target from being damaged twice in one splash.
    private readonly HashSet<ITargetable> _splashTargets = new();

    private ITargetable _target;
    private Component _targetComponent;
    private LayerMask _targetMask;
    private float _speed;
    private int _damage;
    private float _splashRadius;
    private float _slowPercent;
    private float _slowDuration;
    private float _age;
    private Vector3 _startPosition;
    private float _flightDuration;
    private float _arcPeakHeight;
    // 0–1 progress along the arc used to evaluate the parabola each frame.
    private float _progress;
    private bool _launched;
    private bool _impacted;

    #endregion

    #region Public API

    public void Launch(ITargetable target, float speed, int damage, float splashRadius, float slowPercent, float slowDuration, LayerMask targetMask)
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
    }

    #endregion

    #region Flight Update

    private void Update()
    {
        if (!_launched || _impacted) return;

        _age += Time.deltaTime;
        if (_age >= maxLifetime || _target == null || !_target.IsAlive || _targetComponent == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition = GetTargetPosition(_targetComponent);
        Vector3 previousPosition = transform.position;

        // Advance along the arc based on elapsed time vs. the pre-computed flight duration.
        _progress = Mathf.Clamp01(_progress + Time.deltaTime / _flightDuration);

        Vector3 nextPosition = EvaluateArcPosition(targetPosition, _progress);
        transform.position = nextPosition;

        // Rotate to face the direction of travel so the projectile model aligns with its arc.
        Vector3 velocity = nextPosition - previousPosition;
        if (velocity.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        if (_progress >= 1f || Vector3.Distance(nextPosition, targetPosition) <= impactDistance)
            Impact(targetPosition);
    }

    #endregion

    #region Arc Math

    private void ConfigureArc()
    {
        Vector3 targetPosition = _targetComponent != null ? GetTargetPosition(_targetComponent) : _startPosition;
        float distance = Vector3.Distance(_startPosition, targetPosition);
        _flightDuration = Mathf.Max(0.01f, Mathf.Max(minFlightDuration, distance / _speed));
        _arcPeakHeight = Mathf.Max(0f, arcHeight + distance * arcHeightPerDistance);
    }

    // Linear XZ interpolation + a sine-based Y offset creates a smooth parabolic arc.
    private Vector3 EvaluateArcPosition(Vector3 targetPosition, float progress)
    {
        Vector3 position = Vector3.Lerp(_startPosition, targetPosition, progress);
        position.y += Mathf.Sin(progress * Mathf.PI) * _arcPeakHeight;
        return position;
    }

    #endregion

    #region Impact & Damage

    private void Impact(Vector3 impactPosition)
    {
        if (_impacted) return;
        _impacted = true;

        if (_splashRadius > 0.05f)
            DamageSplash(impactPosition);
        else
            DamageTarget(_target);

        Destroy(gameObject);
    }

    private void DamageSplash(Vector3 center)
    {
        _splashTargets.Clear();
        int count = Physics.OverlapSphereNonAlloc(center, _splashRadius, _hits, _targetMask, QueryTriggerInteraction.Ignore);

        for (int index = 0; index < count; index++)
        {
            Collider hit = _hits[index];
            if (hit == null) continue;

            ITargetable target = hit.GetComponentInParent<ITargetable>();
            if (target == null || !target.IsAlive || !_splashTargets.Add(target)) continue;
            DamageTarget(target);
        }
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

    // Prefer the collider's center over the root transform position so the
    // projectile aims at the visual body of the target, not its pivot point.
    private static Vector3 GetTargetPosition(Component targetComponent)
    {
        Collider targetCollider = targetComponent.GetComponentInChildren<Collider>();
        return targetCollider != null ? targetCollider.bounds.center : targetComponent.transform.position;
    }

    #endregion
}
