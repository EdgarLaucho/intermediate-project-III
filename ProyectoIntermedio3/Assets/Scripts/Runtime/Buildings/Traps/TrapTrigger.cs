using System.Collections.Generic;
using UnityEngine;

// Shared trigger component for ALL trap types (Spikes, Tar, Mine).
//
// Two-phase logic, each frame after cooldown expires:
//   Phase 1 — Detection : at least one live enemy must be inside the trap's own
//             cell footprint. If none, nothing happens.
//   Phase 2 — Collection: gather every live enemy inside trap.GetCollectionRadius().
//             Spikes return 0 (same cell); Mine/Tar override it for their AOE.
//
// Once targets are collected, trap.OnTriggered(targets) is called — each Trap
// subclass decides what to do (damage, slow, explosion log, VFX, etc.).
[RequireComponent(typeof(Trap))]
public sealed class TrapTrigger : MonoBehaviour
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
        if (_trap == null || !_trap.IsAlive) return;

        _cooldown -= Time.deltaTime;
        if (_cooldown > 0f) return;

        // Phase 1 — detection: bail out if no enemy is on the trap's cell.
        if (!DetectEnemy()) return;

        // Phase 2 — collection: sweep the trap's effect radius (may be larger than the cell).
        CollectTargets(_trap.GetCollectionRadius());
        if (_targets.Count == 0) return;

        _trap.OnTriggered(_targets);
        _cooldown = Mathf.Max(0.02f, _trap.Cooldown);
    }

    #endregion

    #region Detection & Collection

    // Returns true when at least one live enemy is inside the (small) trigger cell.
    private bool DetectEnemy()
    {
        float cs = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        float half = 0.5f * cs;
        int count = Physics.OverlapBoxNonAlloc(
            transform.position, new Vector3(half, half, half), _hits, Quaternion.identity, targetMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (_hits[i] == null) continue;
            ITargetable t = _hits[i].GetComponentInParent<ITargetable>();
            if (t != null && t.IsAlive) return true;
        }
        return false;
    }

    // Populates _targets with every unique live enemy within the given cell radius.
    private void CollectTargets(int cellRadius)
    {
        _targets.Clear();
        _uniqueTargets.Clear();

        float cs = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        float half = (cellRadius + 0.5f) * cs;
        int count = Physics.OverlapBoxNonAlloc(
            transform.position, new Vector3(half, half, half), _hits, Quaternion.identity, targetMask, QueryTriggerInteraction.Ignore);

        for (int index = 0; index < count; index++)
        {
            Collider hit = _hits[index];
            if (hit == null) continue;

            ITargetable target = hit.GetComponentInParent<ITargetable>();
            if (target == null || !target.IsAlive || !_uniqueTargets.Add(target)) continue;
            _targets.Add(target);
        }
    }

    #endregion
}