using System.Collections.Generic;
using UnityEngine;

public class TarSlowField : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private float tickInterval = 0.1f;

    #endregion

    #region Runtime State

    private readonly Collider[] _hits = new Collider[64];
    private readonly HashSet<ITargetable> _uniqueTargets = new();

    private int _cellRadius;
    private float _slowPercent;
    private float _expireAt;
    private float _nextTick;
    private LayerMask _targetMask;

    #endregion

    #region Public API

    public void Initialize(int cellRadius, float duration, float slowPercent, LayerMask targetMask)
    {
        _cellRadius = Mathf.Max(0, cellRadius);
        _slowPercent = Mathf.Clamp01(slowPercent);
        _targetMask = targetMask;
        _expireAt = Time.time + Mathf.Max(0.05f, duration);
        ApplySlowTick();
    }

    #endregion

    #region Update Loop

    private void Update()
    {
        if (GamePauseEvents.IsPaused) return;

        if (Time.time >= _expireAt)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time < _nextTick) return;
        ApplySlowTick();
    }

    #endregion

    #region Slow Application

    private void ApplySlowTick()
    {
        _nextTick = Time.time + Mathf.Max(0.02f, tickInterval);
        if (_slowPercent <= 0f) return;

        _uniqueTargets.Clear();

        var cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        var half = (_cellRadius + 0.5f) * cellSize;
        var center = SnapToGridCenter(transform.position);

        var count = Physics.OverlapBoxNonAlloc(center, new Vector3(half, half, half), _hits,
                Quaternion.identity, _targetMask, QueryTriggerInteraction.Ignore);

        float refreshDuration = Mathf.Max(0.08f, tickInterval * 2f);
        for (int index = 0; index < count; index++)
        {
            var hit = _hits[index];
            if (hit == null) continue;

            var target = hit.GetComponentInParent<ITargetable>();
            if (target == null || !target.IsAlive || !_uniqueTargets.Add(target)) continue;

            if (target is ISlowable slowable)
                slowable.ApplySlow(_slowPercent, refreshDuration);
        }
    }

    private static Vector3 SnapToGridCenter(Vector3 worldPosition)
    {
        var grid = GridManager.Instance;
        return grid != null ? grid.GridToWorld(grid.WorldToGrid(worldPosition)) : worldPosition;
    }

    #endregion
}