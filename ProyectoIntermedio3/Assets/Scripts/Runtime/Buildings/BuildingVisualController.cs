using System;
using UnityEngine;

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
        var parent = visualParent != null ? visualParent : transform;
        var previousVisual = ActiveVisualRoot;

        if (previousVisual == parent)
            previousVisual = null;

        var visualObject = Instantiate(visualPrefab, parent, false);
        visualObject.name = visualChildName;
        var visualTransform = visualObject.transform;

        if (previousVisual != null)
        {
            visualTransform.localPosition = previousVisual.localPosition;
            visualTransform.localRotation = previousVisual.localRotation;
            visualTransform.localScale = previousVisual.localScale;
        }

        if (previousVisual != null && previousVisual != parent)
            Destroy(previousVisual.gameObject);

        _activeVisualRoot = visualTransform;
        OnVisualChanged?.Invoke(_activeVisualRoot);
    }

    private Transform FindVisualRoot()
    {
        var parent = visualParent != null ? visualParent : transform;
        var directChild = parent.Find(visualChildName);
        if (directChild != null)
            return directChild;

        if (visualParent != null)
            return parent.childCount > 0 ? parent.GetChild(0) : null;

        return parent;
    }

    #endregion
}