using System.Collections.Generic;
using UnityEngine;

public sealed class TowerLaunchSockets : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private Transform aimPivot;
    [SerializeField] private Transform[] shootPoints;
    [SerializeField] private string aimPivotName = "Turret";
    [SerializeField] private string shootPointName = "ShootPoint";

    #endregion

    #region Public API

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

    private void ResolveMissingReferences()
    {
        if (aimPivot == null && !string.IsNullOrEmpty(aimPivotName))
            aimPivot = FindChildRecursive(transform, aimPivotName);

        if ((shootPoints == null || shootPoints.Length == 0) && !string.IsNullOrEmpty(shootPointName))
            shootPoints = FindChildrenRecursive(transform, shootPointName);
    }

    #endregion

    #region Static Hierarchy Utilities

    public static Transform FindChildRecursive(Transform parent, string childName)
    {
        for (int index = 0; index < parent.childCount; index++)
        {
            var child = parent.GetChild(index);
            if (child.name == childName) return child;

            var match = FindChildRecursive(child, childName);
            if (match != null) return match;
        }

        return null;
    }

    public static Transform[] FindChildrenRecursive(Transform parent, string childName)
    {
        var matches = new List<Transform>();
        CollectChildrenNamed(parent, childName, matches);
        return matches.ToArray();
    }

    private static void CollectChildrenNamed(Transform parent, string childName, List<Transform> matches)
    {
        for (int index = 0; index < parent.childCount; index++)
        {
            var child = parent.GetChild(index);
            if (child.name == childName)
                matches.Add(child);

            CollectChildrenNamed(child, childName, matches);
        }
    }

    #endregion
}