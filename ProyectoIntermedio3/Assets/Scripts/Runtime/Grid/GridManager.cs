using UnityEngine;
using System.Collections.Generic;


public class GridManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Grid Configuration")]
    [SerializeField] private int gridRadius = 3;
    [SerializeField] private Vector2Int nexusSize = new(2, 2);
    [SerializeField] private float cellSize = 1f;

    [Header("Scene References")]
    [SerializeField] private Transform nexusTransform;

    #endregion

    #region Runtime State

    private Dictionary<Vector2Int, GridCell> cells = new();

    private Dictionary<Vector2Int, int> _blockerCounts = new();

    private Vector2Int nexusMin;
    private Vector2Int nexusMax;

    #endregion

    #region Public API

    public float CellSize => cellSize;
    public IEnumerable<GridCell> GetAllCells() => cells.Values;

    public static GridManager Instance { get; private set; }

    public void BlockCells(IReadOnlyList<Vector2Int> coords)
    {
        foreach (Vector2Int c in coords)
        {
            _blockerCounts.TryGetValue(c, out int count);
            _blockerCounts[c] = count + 1;

            if (cells.TryGetValue(c, out GridCell cell))
                cell.IsBuildable = false;
        }
    }

    public void UnblockCells(IReadOnlyList<Vector2Int> coords)
    {
        foreach (Vector2Int c in coords)
        {
            if (!_blockerCounts.TryGetValue(c, out int count)) continue;

            int next = count - 1;
            if (next <= 0)
            {
                _blockerCounts.Remove(c);

                if (cells.TryGetValue(c, out GridCell cell) && !cell.IsOccupiedByNexus)
                {
                    int dx = Mathf.Max(0, Mathf.Max(nexusMin.x - c.x, c.x - nexusMax.x));
                    int dy = Mathf.Max(0, Mathf.Max(nexusMin.y - c.y, c.y - nexusMax.y));
                    cell.IsBuildable = Mathf.Max(dx, dy) <= gridRadius;
                }
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
        Vector3 origin = nexusTransform != null ? nexusTransform.position : Vector3.zero;
        Vector3 local = worldPos - origin;
        int x = Mathf.RoundToInt(local.x / cellSize);
        int y = Mathf.RoundToInt(local.z / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 GridToWorld(Vector2Int coords)
    {
        Vector3 origin = nexusTransform != null ? nexusTransform.position : Vector3.zero;
        return origin + new Vector3(coords.x * cellSize, 0f, coords.y * cellSize);
    }

    #endregion

    #region Lifecycle

    private void Awake()
    {
        Instance = this;
        InitializeGrid();
    }

    #endregion

    #region Grid Initialization

    public void InitializeGrid()
    {
        cells.Clear();

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