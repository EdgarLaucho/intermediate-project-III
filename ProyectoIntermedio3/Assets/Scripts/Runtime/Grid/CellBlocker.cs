using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CellBlocker : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private bool useManualSize = false;
    [SerializeField, Min(1)] private int manualSizeX = 1;
    [SerializeField, Min(1)] private int manualSizeY = 1;
    [SerializeField] private float boundsPadding = 0f;

    #endregion

    #region Runtime State

    private GridManager _grid;

    private List<Vector2Int> _blockedCells = new();

    private bool _started;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _grid = GridManager.Instance;

        if (_grid == null)
        {
            Debug.LogWarning($"[CellBlocker] '{name}': No GridManager found in scene.", this);
            return;
        }

        BuildBlockedList(_grid);
    }

    private void Start()
    {
        if (_grid == null || _blockedCells.Count == 0) return;

        _grid.BlockCells(_blockedCells);
        _started = true;
    }

    private void OnEnable()
    {
        if (!_started || _grid == null || _blockedCells.Count == 0) return;

        _grid.BlockCells(_blockedCells);
    }

    private void OnDisable()
    {
        if (!_started || _grid == null || _blockedCells.Count == 0) return;

        _grid.UnblockCells(_blockedCells);
    }

    #endregion

    #region Cell Resolution

    private void BuildBlockedList(GridManager grid)
    {
        _blockedCells.Clear();

        if (useManualSize)
        {
            AddManualCells(grid, _blockedCells);
            return;
        }

        var renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            _blockedCells.Add(grid.WorldToGrid(transform.position));
            return;
        }

        var seen = new HashSet<Vector2Int>();

        foreach (Renderer r in renderers)
            AddCellsForBounds(r.bounds, grid, seen);

        _blockedCells.AddRange(seen);
    }

    private void AddCellsForBounds(Bounds bounds, GridManager grid, HashSet<Vector2Int> seen)
    {
        bounds.Expand(boundsPadding * 2f);

        var minCell = grid.WorldToGrid(bounds.min);
        var maxCell = grid.WorldToGrid(bounds.max);

        var xMin = Mathf.Min(minCell.x, maxCell.x);
        var xMax = Mathf.Max(minCell.x, maxCell.x);
        var yMin = Mathf.Min(minCell.y, maxCell.y);
        var yMax = Mathf.Max(minCell.y, maxCell.y);

        for (int x = xMin; x <= xMax; x++)
            for (int y = yMin; y <= yMax; y++)
                seen.Add(new Vector2Int(x, y));
    }

    private void AddManualCells(GridManager grid, List<Vector2Int> cells)
    {
        var origin = grid.WorldToGrid(transform.position);

        var width = Mathf.Max(1, manualSizeX);
        var height = Mathf.Max(1, manualSizeY);
        
        var startX = origin.x - (width - 1) / 2;
        var startY = origin.y - (height - 1) / 2;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cells.Add(new Vector2Int(startX + x, startY + y));
    }

    #endregion

    #region Editor Visualization

    private void OnDrawGizmos()
    {
        var grid = _grid != null ? _grid : FindAnyObjectByType<GridManager>();
        if (grid == null) return;

        List<Vector2Int> cells;
        if (_blockedCells.Count > 0)
        {
            cells = _blockedCells;
        }
        else if (useManualSize)
        {
            cells = new List<Vector2Int>();
            AddManualCells(grid, cells);
        }
        else
        {
            var renderers = GetComponentsInChildren<Renderer>();
            HashSet<Vector2Int> seen = new();
            if (renderers.Length > 0)
                foreach (Renderer r in renderers)
                    AddCellsForBounds(r.bounds, grid, seen);
            else
                seen.Add(grid.WorldToGrid(transform.position));
            cells = seen.ToList();
        }

        var tileSize = grid.CellSize * 0.9f;

        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.45f);
        foreach (Vector2Int c in cells)
        {
            var centre = grid.GridToWorld(c) + Vector3.up * 0.05f;
            Gizmos.DrawCube(centre, new Vector3(tileSize, 0.1f, tileSize));
        }

        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.85f);
        foreach (Vector2Int c in cells)
        {
            var centre = grid.GridToWorld(c) + Vector3.up * 0.05f;
            Gizmos.DrawWireCube(centre, new Vector3(tileSize, 0.1f, tileSize));
        }
    }

    #endregion
}
