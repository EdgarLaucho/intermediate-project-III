using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Grid Configuration")]
    [SerializeField, Min(1)] private int gridRadius = 3;
    [SerializeField] private Vector2Int nexusSize = new(2, 2);
    [SerializeField, Min(0.01f)] private float cellSize = 1f;

    [Header("Scene References")]
    [SerializeField] private Transform nexusTransform;

    #endregion

    #region Runtime State

    private readonly Dictionary<Vector2Int, GridCell> cells = new();
    private readonly Dictionary<Vector2Int, int> _blockerCounts = new();

    private Vector2Int nexusMin;
    private Vector2Int nexusMax;

    #endregion

    #region Public API

    public static GridManager Instance { get; private set; }

    public float CellSize => cellSize;
    public IEnumerable<GridCell> GetAllCells() => cells.Values;

    public void BlockCells(IReadOnlyList<Vector2Int> coords)
    {
        foreach (Vector2Int c in coords)
        {
            _blockerCounts.TryGetValue(c, out var count);
            _blockerCounts[c] = count + 1;

            if (cells.TryGetValue(c, out var cell))
                cell.IsBuildable = false;
        }
    }

    public void UnblockCells(IReadOnlyList<Vector2Int> coords)
    {
        foreach (Vector2Int c in coords)
        {
            if (!_blockerCounts.TryGetValue(c, out var count)) continue;

            var next = count - 1;
            if (next <= 0)
            {
                _blockerCounts.Remove(c);

                if (cells.TryGetValue(c, out var cell) && !cell.IsOccupiedByNexus)
                    cell.IsBuildable = IsInsideBuildRadius(c, nexusMin, nexusMax);
            }
            else
            {
                _blockerCounts[c] = next;
            }
        }
    }

    public GridCell GetCell(Vector2Int coords)
    {
        cells.TryGetValue(coords, out GridCell cell);
        return cell;
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        var local = worldPos - Origin;
        var x = Mathf.RoundToInt(local.x / cellSize);
        var y = Mathf.RoundToInt(local.z / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 GridToWorld(Vector2Int coords)
    {
        return Origin + new Vector3(coords.x * cellSize, 0f, coords.y * cellSize);
    }

    #endregion

    #region Lifecycle

    private void Awake()
    {
        Instance = this;
        InitializeGrid();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region Grid Initialization

    private void InitializeGrid()
    {
        cells.Clear();
        _blockerCounts.Clear();

        var safeNexusSize = SafeNexusSize;
        GetNexusBounds(safeNexusSize, out nexusMin, out nexusMax);
        GetGridBounds(nexusMin, nexusMax, out int xMin, out int xMax, out int yMin, out int yMax);

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                var coords = new Vector2Int(x, y);
                var cell = new GridCell(coords);

                if (IsInsideRect(coords, nexusMin, nexusMax))
                {
                    cell.IsOccupiedByNexus = true;
                    cell.IsBuildable = false;
                }
                else
                {
                    cell.IsBuildable = IsInsideBuildRadius(coords, nexusMin, nexusMax);
                }

                cells[coords] = cell;
            }
        }

        Debug.Log($"[GridManager] Grid initialized - {cells.Count} cells, radius {gridRadius}, nexus {safeNexusSize}.");
    }

    #endregion

    #region Grid Helpers

    private Vector3 Origin => nexusTransform != null ? nexusTransform.position : Vector3.zero;

    private Vector2Int SafeNexusSize => new(
        Mathf.Max(1, nexusSize.x),
        Mathf.Max(1, nexusSize.y));

    private static void GetNexusBounds(Vector2Int size, out Vector2Int min, out Vector2Int max)
    {
        var halfX = size.x / 2;
        var halfY = size.y / 2;
        min = new Vector2Int(-halfX, -halfY);
        max = new Vector2Int(size.x - 1 - halfX, size.y - 1 - halfY);
    }

    private void GetGridBounds(Vector2Int min, Vector2Int max, out int xMin, out int xMax, out int yMin, out int yMax)
    {
        xMin = min.x - gridRadius;
        xMax = max.x + gridRadius;
        yMin = min.y - gridRadius;
        yMax = max.y + gridRadius;
    }

    private bool IsInsideBuildRadius(Vector2Int coords, Vector2Int min, Vector2Int max)
    {
        var dx = Mathf.Max(0, Mathf.Max(min.x - coords.x, coords.x - max.x));
        var dy = Mathf.Max(0, Mathf.Max(min.y - coords.y, coords.y - max.y));
        return Mathf.Max(dx, dy) <= gridRadius;
    }

    private static bool IsInsideRect(Vector2Int coords, Vector2Int min, Vector2Int max)
    {
        return coords.x >= min.x && coords.x <= max.x
            && coords.y >= min.y && coords.y <= max.y;
    }

    #endregion

    #region Editor Visualization

    private void OnDrawGizmos()
    {
        var safeNexusSize = SafeNexusSize;
        GetNexusBounds(safeNexusSize, out var nMin, out var nMax);
        GetGridBounds(nMin, nMax, out var xMin, out var xMax, out var yMin, out var yMax);

        var cubeSize = new Vector3(cellSize * 0.9f, 0.05f, cellSize * 0.9f);

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                var coords = new Vector2Int(x, y);

                Gizmos.color = IsInsideRect(coords, nMin, nMax)
                    ? new Color(0.2f, 0.5f, 1f, 0.5f)
                    : BuildableGizmoColor(coords, nMin, nMax);

                var center = Origin + new Vector3(x * cellSize, 0f, y * cellSize);
                Gizmos.DrawCube(center, cubeSize);
            }
        }
    }

    private Color BuildableGizmoColor(Vector2Int coords, Vector2Int min, Vector2Int max)
    {
        return IsInsideBuildRadius(coords, min, max)
            ? new Color(0f, 1f, 0f, 0.3f)
            : new Color(1f, 0f, 0f, 0.2f);
    }

    #endregion
}