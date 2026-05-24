using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Trap))]
public class TrapTrigger : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private LayerMask targetMask = ~0;

    #endregion

    #region Runtime State

    private readonly Collider[] _hits = new Collider[64];
    private readonly List<ITargetable> _targets = new();
    private readonly HashSet<ITargetable> _uniqueTargets = new();
    private Trap _trap;
    private float _cooldown;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        _trap = GetComponent<Trap>();
        if (_trap != null)
            _trap.SetRuntimeTargetMask(targetMask);
    }

    #endregion

    #region Update Loop

    private void Update()
    {
        if (GamePauseEvents.IsPaused) return;
        if (_trap == null || !_trap.IsAlive) return;

        _cooldown -= Time.deltaTime;
        if (_cooldown > 0f) return;

        if (!DetectEnemy()) return;

        CollectTargets(_trap.GetCollectionRadius());
        if (_targets.Count == 0) return;

        _trap.OnTriggered(_targets);
        _cooldown = Mathf.Max(0.02f, _trap.Cooldown);
    }

    #endregion

    #region Detection & Collection

    private bool DetectEnemy()
    {
        var cellSiz = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        var half = 0.5f * cellSiz;
        var count = Physics.OverlapBoxNonAlloc(
            transform.position, new Vector3(half, half, half), _hits, Quaternion.identity, targetMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (_hits[i] == null) continue;
            var t = _hits[i].GetComponentInParent<ITargetable>();
            if (t != null && t.IsAlive) return true;
        }
        return false;
    }

    private void CollectTargets(int cellRadius)
    {
        _targets.Clear();
        _uniqueTargets.Clear();

        var cs = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        var half = (cellRadius + 0.5f) * cs;
        var count = Physics.OverlapBoxNonAlloc(
            transform.position, new Vector3(half, half, half), _hits, Quaternion.identity, targetMask, QueryTriggerInteraction.Ignore);

        for (int index = 0; index < count; index++)
        {
            var hit = _hits[index];
            if (hit == null) continue;

            var target = hit.GetComponentInParent<ITargetable>();
            if (target == null || !target.IsAlive || !_uniqueTargets.Add(target)) continue;
            _targets.Add(target);
        }
    }

    #endregion
}