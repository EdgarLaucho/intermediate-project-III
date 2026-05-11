using System.Collections.Generic;
using UnityEngine;

// Polls for enemies inside the trap's radius each frame and forwards the hit list
// to Trap.ApplyToTargets. Keeping the physics detection here lets Trap remain
// independent of Unity's physics API, which simplifies testing.
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
    }

    #endregion

    #region Update Loop

    private void Update()
    {
        if (_trap == null || !_trap.IsAlive) return;

        _cooldown -= Time.deltaTime;
        if (_cooldown > 0f) return;

        CollectTargets();
        if (_targets.Count == 0) return;

        _trap.ApplyToTargets(_targets);
        _cooldown = Mathf.Max(0.02f, _trap.Cooldown);
    }

    #endregion

    #region Target Collection

    private void CollectTargets()
    {
        _targets.Clear();
        _uniqueTargets.Clear();

        float radius = Mathf.Max(0.05f, _trap.TriggerRadius);
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, _hits, targetMask, QueryTriggerInteraction.Ignore);

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
