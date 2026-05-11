using System.Collections.Generic;
using UnityEngine;

// Marks and exposes the physical attachment points used by TowerShooter.
// Placing this component on the visual mesh lets designers configure shoot points
// per-prefab without touching TowerShooter's serialized fields on the root GameObject.
public sealed class TowerLaunchSockets : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private Transform aimPivot;
    [SerializeField] private Transform[] shootPoints;
    [SerializeField] private string aimPivotName = "Turret";
    [SerializeField] private string shootPointName = "ShootPoint";

    #endregion

    #region Public API

    // Lazy resolution ensures references survive visual swaps at runtime.
    public Transform AimPivot
    {
        get
        {
            ResolveMissingReferences();
            return aimPivot;
        }
    }

    public int ShootPointCount
    {
        get
        {
            ResolveMissingReferences();
            return shootPoints?.Length ?? 0;
        }
    }

    // Wraps the index so callers can cycle through all shoot points without
    // worrying about going out of bounds.
    public Transform GetShootPoint(int index)
    {
        ResolveMissingReferences();
        if (shootPoints == null || shootPoints.Length == 0) return transform;
        return shootPoints[Mathf.Abs(index) % shootPoints.Length];
    }

    #endregion

    #region Lifecycle

    private void Awake()
    {
        ResolveMissingReferences();
    }

    #endregion

    #region Reference Resolution

    // Fills in any references left blank in the Inspector by searching the hierarchy
    // by name. This means the component works even on dynamically instantiated prefabs
    // that haven't been manually wired up.
    private void ResolveMissingReferences()
    {
        if (aimPivot == null && !string.IsNullOrEmpty(aimPivotName))
            aimPivot = FindChildRecursive(transform, aimPivotName);

        if ((shootPoints == null || shootPoints.Length == 0) && !string.IsNullOrEmpty(shootPointName))
            shootPoints = FindChildrenRecursive(transform, shootPointName);
    }

    #endregion

    #region Static Hierarchy Utilities

    // Depth-first search that returns the first child whose name matches.
    public static Transform FindChildRecursive(Transform parent, string childName)
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

    // Collects every child in the hierarchy whose name matches – used for
    // multi-barrel towers that have several shoot points.
    public static Transform[] FindChildrenRecursive(Transform parent, string childName)
    {
        List<Transform> matches = new();
        CollectChildrenNamed(parent, childName, matches);
        return matches.ToArray();
    }

    private static void CollectChildrenNamed(Transform parent, string childName, List<Transform> matches)
    {
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == childName)
                matches.Add(child);

            CollectChildrenNamed(child, childName, matches);
        }
    }

    #endregion
}
