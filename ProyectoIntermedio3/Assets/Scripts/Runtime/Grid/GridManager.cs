using UnityEngine;
using System.Collections.Generic;

// Owns and initialises the entire grid data model. Provides coordinate conversion
// and cell lookups for any system that needs to query occupancy (BuildManager,
// GridRenderer, targeting logic, etc.).

// Layout: the nexus sits at the origin (cell 0,0 = nexus centre). Buildable cells
// extend `gridRadius` steps away from the nexus edge using Chebyshev distance
// (max of dx, dy), which produces a square ring rather than a circular one.
public class GridManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Grid Configuration")]
    [SerializeField] private int gridRadius = 3;
    // How many cells wide/tall the nexus footprint is. Centred on the origin.
    [SerializeField] private Vector2Int nexusSize = new Vector2Int(2, 2);
    [SerializeField] private float cellSize = 1f;

    [Header("Scene References")]
    [SerializeField] private Transform nexusTransform;

    #endregion

    #region Runtime State

    private Dictionary<Vector2Int, GridCell> cells = new();

    // Cached nexus bounds (in grid coords) computed once during InitializeGrid.
    private Vector2Int nexusMin;
    private Vector2Int nexusMax;

    #endregion

    #region Public API

    public float CellSize => cellSize;
    public IEnumerable<GridCell> GetAllCells() => cells.Values;

    public GridCell GetCell(Vector2Int coords)
    {
        cells.TryGetValue(coords, out GridCell cell);
        return cell;
    }

    // Subtracts the nexus world origin then divides by cellSize.
    // RoundToInt snaps to the nearest cell rather than truncating.
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        Vector3 origin = nexusTransform != null ? nexusTransform.position : Vector3.zero;
        Vector3 local = worldPos - origin;
        int x = Mathf.RoundToInt(local.x / cellSize);
        int y = Mathf.RoundToInt(local.z / cellSize);
        return new Vector2Int(x, y);
    }

    // Reverses WorldToGrid. Y is always 0 (flat grid).
    public Vector3 GridToWorld(Vector2Int coords)
    {
        Vector3 origin = nexusTransform != null ? nexusTransform.position : Vector3.zero;
        return origin + new Vector3(coords.x * cellSize, 0f, coords.y * cellSize);
    }

    #endregion

    #region Lifecycle

    private void Awake()
    {
        InitializeGrid();
    }

    #endregion

    #region Grid Initialization

    public void InitializeGrid()
    {
        cells.Clear();

        // Centre the nexus footprint on the origin. Integer division floors toward
        // negative, so a 2×2 nexus occupies cells (-1,-1) through (0,0).
        int halfX = nexusSize.x / 2;
        int halfY = nexusSize.y / 2;
        nexusMin = new Vector2Int(-halfX, -halfY);
        nexusMax = new Vector2Int(nexusSize.x - 1 - halfX, nexusSize.y - 1 - halfY);

        int xMin = nexusMin.x - gridRadius;
        int xMax = nexusMax.x + gridRadius;
        int yMin = nexusMin.y - gridRadius;
        int yMax = nexusMax.y + gridRadius;

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                Vector2Int coords = new Vector2Int(x, y);
                GridCell cell = new GridCell(coords);

                bool inNexus = x >= nexusMin.x && x <= nexusMax.x
                            && y >= nexusMin.y && y <= nexusMax.y;

                if (inNexus)
                {
                    cell.IsOccupiedByNexus = true;
                    cell.IsBuildable = false;
                }
                else
                {
                    // Chebyshev distance from this cell to the nearest nexus edge.
                    // Cells within gridRadius steps are buildable; beyond that they
                    // are still added to the dictionary so WorldToGrid always returns
                    // a valid cell even outside the playable ring.
                    int dx = Mathf.Max(0, Mathf.Max(nexusMin.x - x, x - nexusMax.x));
                    int dy = Mathf.Max(0, Mathf.Max(nexusMin.y - y, y - nexusMax.y));
                    int dist = Mathf.Max(dx, dy);
                    cell.IsBuildable = dist <= gridRadius;
                }

                cells[coords] = cell;
            }
        }

        Debug.Log($"[GridManager] Grid initialised — {cells.Count} cells, radius {gridRadius}, nexus {nexusSize}.");
    }

    #endregion

    #region Editor Visualization

    // Draws the full grid in the Scene view without entering Play mode.
    // Blue = nexus, Green = buildable, Red = out-of-range.
    // The logic mirrors InitializeGrid exactly so the gizmo is always in sync.
    private void OnDrawGizmos()
    {
        int halfX = nexusSize.x / 2;
        int halfY = nexusSize.y / 2;
        Vector2Int nMin = new(-halfX, -halfY);
        Vector2Int nMax = new(nexusSize.x - 1 - halfX, nexusSize.y - 1 - halfY);

        int xMin = nMin.x - gridRadius;
        int xMax = nMax.x + gridRadius;
        int yMin = nMin.y - gridRadius;
        int yMax = nMax.y + gridRadius;

        Vector3 origin = nexusTransform != null ? nexusTransform.position : Vector3.zero;
        // Slightly smaller than the full cell so borders show as visible gaps.
        Vector3 cubeSize = new(cellSize * 0.9f, 0.05f, cellSize * 0.9f);

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                bool inNexus = x >= nMin.x && x <= nMax.x && y >= nMin.y && y <= nMax.y;

                if (inNexus)
                {
                    Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.5f);
                }
                else
                {
                    int dx = Mathf.Max(0, Mathf.Max(nMin.x - x, x - nMax.x));
                    int dy = Mathf.Max(0, Mathf.Max(nMin.y - y, y - nMax.y));
                    int dist = Mathf.Max(dx, dy);
                    Gizmos.color = dist <= gridRadius
                        ? new Color(0f, 1f, 0f, 0.3f)
                        : new Color(1f, 0f, 0f, 0.2f);
                }

                Vector3 center = origin + new Vector3(x * cellSize, 0f, y * cellSize);
                Gizmos.DrawCube(center, cubeSize);
            }
        }
    }

    #endregion
}