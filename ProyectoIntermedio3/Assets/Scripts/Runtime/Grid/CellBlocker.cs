using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Placed in the scene to permanently reserve grid cells so buildings cannot be
// constructed there.  The footprint is derived automatically from the combined
// world-space bounds of every Renderer on this object and its children, so the
// blocked area always matches the visible mesh — no manual offset configuration
// needed.
//
// Usage:
//   1. Drop this component on any GameObject that has one or more Renderers.
//   2. Adjust `boundsPadding` if you want to grow the blocked area slightly beyond
//      the visible mesh (e.g. 0.1 to catch cells that barely touch the edges).
//   3. Enable/Disable the GameObject at runtime to toggle the block on and off.
//
// Overlap-safe: multiple CellBlockers can share cells.  A cell only becomes
// buildable again once every overlapping blocker has been removed.
public class CellBlocker : MonoBehaviour
{
    #region Inspector Fields

    [Tooltip("Extra world-space padding added uniformly to the combined renderer bounds " +
             "before converting to grid cells. Useful when the mesh barely overlaps a cell edge.")]
    [SerializeField] private float boundsPadding = 0f;

    #endregion

    #region Runtime State

    private GridManager _grid;

    // Precomputed in Awake — renderers are static so this never needs to change.
    private List<Vector2Int> _blockedCells = new();

    // Guards OnEnable so it does nothing before Start has run.
    // On the very first frame, Start handles the initial BlockCells call after
    // GridManager.Awake has already populated its cells dictionary.
    private bool _started;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _grid = FindAnyObjectByType<GridManager>();

        if (_grid == null)
        {
            Debug.LogWarning($"[CellBlocker] '{name}': No GridManager found in scene.", this);
            return;
        }

        // Build the cell list now — renderers are already present, but we do NOT
        // call BlockCells here because GridManager.InitializeGrid() runs in its own
        // Awake, which may not have executed yet.  Blocking is deferred to Start.
        BuildBlockedList(_grid);
    }

    // Start is called after all Awake calls, so the grid is fully initialised.
    private void Start()
    {
        if (_grid == null || _blockedCells.Count == 0) return;

        _grid.BlockCells(_blockedCells);
        _started = true;
    }

    private void OnEnable()
    {
        // Skip the very first enable; Start handles it once the grid is ready.
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

    // Each Renderer gets its own footprint rectangle; the results are unioned.
    // Using a HashSet avoids double-registering cells that are covered by more
    // than one renderer, which would inflate the ref-count inside GridManager.
    private void BuildBlockedList(GridManager grid)
    {
        _blockedCells.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            // No renderers at all — fall back to the single cell under this object.
            _blockedCells.Add(grid.WorldToGrid(transform.position));
            return;
        }

        HashSet<Vector2Int> seen = new();

        foreach (Renderer r in renderers)
            AddCellsForBounds(r.bounds, grid, seen);

        _blockedCells.AddRange(seen);
    }

    // Projects one Renderer's XZ bounds footprint onto the grid and adds every
    // cell inside it to `seen` (duplicates are silently ignored by the HashSet).
    private void AddCellsForBounds(Bounds bounds, GridManager grid, HashSet<Vector2Int> seen)
    {
        bounds.Expand(boundsPadding * 2f);

        Vector2Int minCell = grid.WorldToGrid(bounds.min);
        Vector2Int maxCell = grid.WorldToGrid(bounds.max);

        int xMin = Mathf.Min(minCell.x, maxCell.x);
        int xMax = Mathf.Max(minCell.x, maxCell.x);
        int yMin = Mathf.Min(minCell.y, maxCell.y);
        int yMax = Mathf.Max(minCell.y, maxCell.y);

        for (int x = xMin; x <= xMax; x++)
            for (int y = yMin; y <= yMax; y++)
                seen.Add(new Vector2Int(x, y));
    }

    #endregion

    #region Editor Visualization

    // Shows the blocked footprint in the Scene view as semi-transparent red tiles
    // so designers can verify coverage without entering Play mode.
    private void OnDrawGizmos()
    {
        GridManager grid = _grid != null ? _grid : FindAnyObjectByType<GridManager>();
        if (grid == null) return;

        // In Edit mode _blockedCells is empty, so rebuild per-renderer on the fly.
        List<Vector2Int> cells;
        if (_blockedCells.Count > 0)
        {
            cells = _blockedCells;
        }
        else
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            HashSet<Vector2Int> seen = new();
            if (renderers.Length > 0)
                foreach (Renderer r in renderers)
                    AddCellsForBounds(r.bounds, grid, seen);
            else
                seen.Add(grid.WorldToGrid(transform.position));
            cells = seen.ToList();
        }

        float tileSize = grid.CellSize * 0.9f;

        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.45f);
        foreach (Vector2Int c in cells)
        {
            Vector3 centre = grid.GridToWorld(c) + Vector3.up * 0.05f;
            Gizmos.DrawCube(centre, new Vector3(tileSize, 0.1f, tileSize));
        }

        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.85f);
        foreach (Vector2Int c in cells)
        {
            Vector3 centre = grid.GridToWorld(c) + Vector3.up * 0.05f;
            Gizmos.DrawWireCube(centre, new Vector3(tileSize, 0.1f, tileSize));
        }
    }

    #endregion
}
