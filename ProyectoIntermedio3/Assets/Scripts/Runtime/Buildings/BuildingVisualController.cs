using System;
using UnityEngine;

// Manages the active visual mesh of a building and swaps it out when an upgrade
// provides a new visual prefab. Keeping visuals separate from logic lets designers
// iterate on art without touching gameplay code.
[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingBase))]
public sealed class BuildingVisualController : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private Transform visualParent;
    [SerializeField] private string visualChildName = "Visual";

    #endregion

    #region Runtime State

    private BuildingBase _building;
    private Transform _activeVisualRoot;

    #endregion

    #region Public API

    public event Action<Transform> OnVisualChanged;

    // Lazily resolved so it works correctly even when the visual is spawned after Awake.
    public Transform ActiveVisualRoot
    {
        get
        {
            if (_activeVisualRoot == null)
                _activeVisualRoot = FindVisualRoot();

            return _activeVisualRoot;
        }
    }

    #endregion

    #region Lifecycle

    private void Awake()
    {
        _building = GetComponent<BuildingBase>();
        _activeVisualRoot = FindVisualRoot();
    }

    private void OnEnable()
    {
        if (_building == null)
            _building = GetComponent<BuildingBase>();

        _building.OnUpgraded += HandleUpgraded;
    }

    private void OnDisable()
    {
        if (_building != null)
            _building.OnUpgraded -= HandleUpgraded;
    }

    #endregion

    #region Event Handlers

    private void HandleUpgraded(BuildingBase building, UpgradeLevelData upgradeData)
    {
        if (upgradeData.visualPrefabOverride == null) return;
        ApplyVisualOverride(upgradeData.visualPrefabOverride);
    }

    #endregion

    #region Private Helpers

    private void ApplyVisualOverride(GameObject visualPrefab)
    {
        Transform parent = visualParent != null ? visualParent : transform;
        Transform previousVisual = ActiveVisualRoot;

        // visualParent is a container on several prefabs; never treat the container
        // itself as the disposable visual root.
        if (previousVisual == parent)
            previousVisual = null;

        GameObject visualObject = Instantiate(visualPrefab, parent, false);
        visualObject.name = visualChildName;
        Transform visualTransform = visualObject.transform;

        if (previousVisual != null)
        {
            // Preserve the local transform of the old visual so the new mesh slots in
            // at the exact same position, rotation, and scale.
            visualTransform.localPosition = previousVisual.localPosition;
            visualTransform.localRotation = previousVisual.localRotation;
            visualTransform.localScale = previousVisual.localScale;
        }

        if (previousVisual != null && previousVisual != parent)
            Destroy(previousVisual.gameObject);

        _activeVisualRoot = visualTransform;
        OnVisualChanged?.Invoke(_activeVisualRoot);
    }

    // When visualParent is assigned, it acts as a stable container. Prefer a named
    // child, then the first child, and only fall back to the parent when no container
    // was configured.
    private Transform FindVisualRoot()
    {
        Transform parent = visualParent != null ? visualParent : transform;
        Transform directChild = parent.Find(visualChildName);
        if (directChild != null)
            return directChild;

        if (visualParent != null)
            return parent.childCount > 0 ? parent.GetChild(0) : null;

        return parent;
    }

    #endregion
}