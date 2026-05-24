using UnityEngine;
using System.Collections.Generic;

public class GridRenderer : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private GridManager grid;

    [Header("Layout")]
    [SerializeField] private float markerScale = 0.52f;
    [SerializeField] private float yOffset = 0.02f;
    [SerializeField] private Sprite cellSprite;

    [Header("Colors")]
    [SerializeField] private Color buildableColor = new(0.25f, 0.9f, 0.35f, 0.75f);
    [SerializeField] private Color nexusColor = new(0.25f, 0.55f, 1f, 0.58f);
    [SerializeField] private Color occupiedColor = new(0.75f, 0.75f, 0.75f, 0.5f);

    #endregion

    #region Runtime State

    private readonly List<GameObject> _markers = new();
    private bool _initialized;
    private bool _subscribed;

    #endregion

    #region Lifecycle

    private void Start()
    {
        if (!HasRequiredReferences())
            return;

        RebuildGrid();
        ApplyPhaseVisibility(PhaseEvents.CurrentPhase);
        _initialized = true;
        Subscribe();
    }

    private void OnEnable()
    {
        if (_initialized)
            Subscribe();
    }

    private void OnDisable() => Unsubscribe();

    private void OnDestroy()
    {
        Unsubscribe();
        ClearMarkers();
    }

    #endregion

    #region Validation

    private bool HasRequiredReferences()
    {
        if (grid != null && cellSprite != null)
            return true;

        if (grid == null)
            Debug.LogError("[GridRenderer] 'grid' not assigned.", this);

        if (cellSprite == null)
            Debug.LogError("[GridRenderer] 'cellSprite' not assigned.", this);

        enabled = false;
        return false;
    }

    #endregion

    #region Event Subscriptions

    private void Subscribe()
    {
        if (_subscribed) return;
        if (grid != null)
            grid.OnCellsChanged += OnGridCellsChanged;
        ConstructionEvents.OnBuildingPlaced += OnBuildingPlaced;
        ConstructionEvents.OnBuildingDemolished += OnBuildingDemolished;
        PhaseEvents.OnPhaseChanged += ApplyPhaseVisibility;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (grid != null)
            grid.OnCellsChanged -= OnGridCellsChanged;
        ConstructionEvents.OnBuildingPlaced -= OnBuildingPlaced;
        ConstructionEvents.OnBuildingDemolished -= OnBuildingDemolished;
        PhaseEvents.OnPhaseChanged -= ApplyPhaseVisibility;
        _subscribed = false;
    }

    private void OnGridCellsChanged() => RebuildGrid();
    private void OnBuildingPlaced(BuildingActionArgs _) => RebuildGrid();
    private void OnBuildingDemolished(Vector2Int _) => RebuildGrid();

    private void ApplyPhaseVisibility(GamePhase phase)
    {
        bool visible = phase == GamePhase.Preparation;
        for (int i = 0; i < _markers.Count; i++)
        {
            if (_markers[i] != null)
                _markers[i].SetActive(visible);
        }
    }

    #endregion

    #region Grid Rebuilding

    private void RebuildGrid()
    {
        ClearMarkers();

        foreach (var cell in grid.GetAllCells())
        {
            Vector3 c = grid.GridToWorld(cell.Coordinates) + Vector3.up * yOffset;

            if (cell.IsOccupiedByNexus)
                AddMarker(cell, c, nexusColor, "Nexus");
            else if (cell.IsOccupied && cell.IsBuildable)
                AddMarker(cell, c, occupiedColor, "Occupied");
            else if (cell.IsBuildable)
                AddMarker(cell, c, buildableColor, "Buildable");
        }

        ApplyPhaseVisibility(PhaseEvents.CurrentPhase);
    }

    private void AddMarker(GridCell cell, Vector3 center, Color color, string category)
    {
        GameObject marker = new($"Grid {category} {cell.Coordinates.x},{cell.Coordinates.y}");
        marker.transform.SetParent(transform, false);
        marker.transform.position = center;
        marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        float size = grid.CellSize * markerScale;
        marker.transform.localScale = new Vector3(size, size, 1f);

        var renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = cellSprite;
        renderer.color = color;
        renderer.sortingOrder = -20;

        _markers.Add(marker);
    }

    private void ClearMarkers()
    {
        for (int i = _markers.Count - 1; i >= 0; i--)
        {
            if (_markers[i] != null)
                Destroy(_markers[i]);
        }

        _markers.Clear();
    }

    #endregion
}
