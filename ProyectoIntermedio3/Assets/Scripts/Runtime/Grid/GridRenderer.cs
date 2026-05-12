using UnityEngine;
using System.Collections.Generic;

// Builds and updates a single multi-submesh Mesh that visualises the entire grid.
// Three submeshes correspond to the three materials (buildable / nexus / occupied)
// so Unity can shade each cell type differently with one draw call per material.
//
// The mesh is rebuilt from scratch whenever a building is placed or demolished.
// For the small grids in this project that is fine; for larger grids a per-cell
// dirty-flag approach would reduce the rebuild cost.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GridRenderer : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private GridManager grid;

    [Header("Layout")]
    // Shrinks each cell quad inward from its edge, creating visible gaps between cells.
    [SerializeField] private float cellInset = 0.04f;
    // Lifts the grid mesh slightly above the terrain to prevent z-fighting.
    [SerializeField] private float yOffset = 0.02f;

    [Header("Materials")]
    [SerializeField] private Material buildableMaterial; // Submesh 0
    [SerializeField] private Material nexusMaterial; // Submesh 1
    [SerializeField] private Material occupiedMaterial; // Submesh 2

    #endregion

    #region Runtime State

    private MeshFilter _mf;
    private MeshRenderer _mr;
    private Mesh _mesh;

    // Prevents double-subscription: OnEnable can fire before Start in some cases.
    private bool _subscribed;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();
    }

    private void Start()
    {
        if (grid == null)
        {
            Debug.LogError("[GridRenderer] 'grid' not assigned.");
            return;
        }

        if (buildableMaterial == null || nexusMaterial == null || occupiedMaterial == null)
            Debug.LogError("[GridRenderer] One or more materials are not assigned in the Inspector.");

        RebuildMesh();
        Subscribe();
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void OnDestroy()
    {
        Unsubscribe();
        // Meshes created via `new Mesh()` are not automatically cleaned up by Unity;
        // destroy it explicitly to avoid a memory leak.
        if (_mesh != null)
            Destroy(_mesh);
    }

    #endregion

    #region Event Subscriptions

    private void Subscribe()
    {
        if (_subscribed) return;
        ConstructionEvents.OnBuildingPlaced += OnBuildingPlaced;
        ConstructionEvents.OnBuildingDemolished += OnBuildingDemolished;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        ConstructionEvents.OnBuildingPlaced -= OnBuildingPlaced;
        ConstructionEvents.OnBuildingDemolished -= OnBuildingDemolished;
        _subscribed = false;
    }

    private void OnBuildingPlaced(BuildingActionArgs _) => RebuildMesh();
    private void OnBuildingDemolished(Vector2Int _) => RebuildMesh();

    #endregion

    #region Mesh Rebuilding

    private void RebuildMesh()
    {
        // `half` is the inset half-extent of each cell quad.
        float half = grid.CellSize * 0.5f - cellInset;

        // Separate vertex/triangle lists per submesh so material indices map cleanly:
        // bv/bt = buildable (submesh 0), nv/nt = nexus (1), ov/ot = occupied (2).
        var bv = new List<Vector3>(); var bt = new List<int>();
        var nv = new List<Vector3>(); var nt = new List<int>();
        var ov = new List<Vector3>(); var ot = new List<int>();

        foreach (var cell in grid.GetAllCells())
        {
            Vector3 c = grid.GridToWorld(cell.Coordinates) + Vector3.up * yOffset;

            if (cell.IsOccupiedByNexus)
                AddNexusCell(cell, c, half, nv, nt);
            else if (cell.IsOccupied && cell.IsBuildable)
                AddQuad(c, half, ov, ot);
            else if (cell.IsBuildable)
                AddBuildableCell(c, half, bv, bt);
            // Out-of-range cells are intentionally skipped (not rendered).
        }

        // The three vertex lists will be merged into one array. Triangle indices
        // within nv and ov are local to their own lists, so they must be shifted
        // by the number of vertices that precede them in the merged array.
        OffsetTriangles(nt, bv.Count);
        OffsetTriangles(ot, bv.Count + nv.Count);

        var allVerts = new List<Vector3>(bv.Count + nv.Count + ov.Count);
        allVerts.AddRange(bv);
        allVerts.AddRange(nv);
        allVerts.AddRange(ov);

        var mesh = new Mesh { name = "GridMesh" };
        mesh.SetVertices(allVerts);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(bt, 0);
        mesh.SetTriangles(nt, 1);
        mesh.SetTriangles(ot, 2);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (_mesh != null) Destroy(_mesh);
        _mesh = mesh;
        _mf.sharedMesh = _mesh;
        _mr.materials = new[] { buildableMaterial, nexusMaterial, occupiedMaterial };
    }

    #endregion

    #region Cell Shape Helpers

    // Empty buildable cell: renders as corner brackets rather than a solid quad so
    // the grid looks light. Built from a small central dot plus 8 thin rects
    // (two arms per corner — one horizontal, one vertical).
    private static void AddBuildableCell(Vector3 center, float half, List<Vector3> verts, List<int> tris)
    {
        float centralHalf = half * 0.30f;
        float cornerThickness = half * 0.085f;
        float cornerLength = half * 0.34f;
        float inner = half - cornerThickness;
        float cornerStart = half - cornerLength;

        // Central dot.
        AddQuad(center, centralHalf, verts, tris);

        // Four corners, two rects each.
        AddRect(center, -half, -cornerStart, inner, half, verts, tris);
        AddRect(center, -half, -inner, cornerStart, half, verts, tris);
        AddRect(center, cornerStart, half, inner, half, verts, tris);
        AddRect(center, inner, half, cornerStart, half, verts, tris);
        AddRect(center, -half, -cornerStart, -half, -inner, verts, tris);
        AddRect(center, -half, -inner, -half, -cornerStart, verts, tris);
        AddRect(center, cornerStart, half, -half, -inner, verts, tris);
        AddRect(center, inner, half, -half, -cornerStart, verts, tris);
    }

    // Nexus cell: only draws border edges that face outward (where the neighbouring
    // cell is not also a nexus cell). This produces a clean outer frame around the
    // whole nexus footprint with no internal dividers between adjacent nexus tiles.
    private void AddNexusCell(GridCell cell, Vector3 center, float half,
                              List<Vector3> verts, List<int> tris)
    {
        float edgeThickness = half * 0.12f;
        float inner = half - edgeThickness;

        bool westOpen = !IsNexusCell(cell.Coordinates + Vector2Int.left);
        bool eastOpen = !IsNexusCell(cell.Coordinates + Vector2Int.right);
        bool southOpen = !IsNexusCell(cell.Coordinates + Vector2Int.down);
        bool northOpen = !IsNexusCell(cell.Coordinates + Vector2Int.up);

        if (northOpen) AddRect(center, -half, half, inner, half, verts, tris);
        if (southOpen) AddRect(center, -half, half, -half, -inner, verts, tris);
        if (westOpen) AddRect(center, -half, -inner, -half, half, verts, tris);
        if (eastOpen) AddRect(center, inner, half, -half, half, verts, tris);

        AddNexusGlyph(center, half, verts, tris);
    }

    private bool IsNexusCell(Vector2Int coords)
    {
        GridCell neighbor = grid.GetCell(coords);
        return neighbor != null && neighbor.IsOccupiedByNexus;
    }

    // Decorative glyph drawn on every nexus cell: a small diamond at the centre
    // plus four short tick marks pointing outward along the cardinal axes.
    private static void AddNexusGlyph(Vector3 center, float half,
                                      List<Vector3> verts, List<int> tris)
    {
        float diamondRadius = half * 0.20f;
        AddDiamond(center, diamondRadius, verts, tris);

        float tickHalfLength = half * 0.20f;
        float tickGap = half * 0.30f;
        float tickThickness = half * 0.030f;

        // Left and right horizontal ticks.
        AddRect(center, -tickGap - tickHalfLength, -tickGap, -tickThickness, tickThickness, verts, tris);
        AddRect(center, tickGap, tickGap + tickHalfLength, -tickThickness, tickThickness, verts, tris);
        // Bottom and top vertical ticks.
        AddRect(center, -tickThickness, tickThickness, -tickGap - tickHalfLength, -tickGap, verts, tris);
        AddRect(center, -tickThickness, tickThickness, tickGap, tickGap + tickHalfLength, verts, tris);
    }

    #endregion

    #region Primitive Helpers

    // Axis-aligned square quad centred on `center`, extending `half` in each direction.
    private static void AddQuad(Vector3 center, float half, List<Vector3> verts, List<int> tris)
    {
        int baseIdx = verts.Count;
        verts.Add(center + new Vector3(-half, 0f, -half));
        verts.Add(center + new Vector3(-half, 0f, half));
        verts.Add(center + new Vector3(half, 0f, half));
        verts.Add(center + new Vector3(half, 0f, -half));
        tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
        tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
    }

    // Axis-aligned rectangle with explicit min/max extents on X and Z.
    private static void AddRect(Vector3 center, float minX, float maxX, float minZ, float maxZ,
                                List<Vector3> verts, List<int> tris)
    {
        int baseIdx = verts.Count;
        verts.Add(center + new Vector3(minX, 0f, minZ));
        verts.Add(center + new Vector3(minX, 0f, maxZ));
        verts.Add(center + new Vector3(maxX, 0f, maxZ));
        verts.Add(center + new Vector3(maxX, 0f, minZ));
        tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
        tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
    }

    // Flat diamond (rotated square) with four vertices on the cardinal axes.
    private static void AddDiamond(Vector3 center, float radius, List<Vector3> verts, List<int> tris)
    {
        int baseIdx = verts.Count;
        verts.Add(center + new Vector3(0f, 0f, radius));
        verts.Add(center + new Vector3(radius, 0f, 0f));
        verts.Add(center + new Vector3(0f, 0f, -radius));
        verts.Add(center + new Vector3(-radius, 0f, 0f));
        tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
        tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
    }

    // Shifts all triangle indices in a list by `offset`. Required when separate
    // per-submesh vertex lists are merged into one shared vertex array.
    private static void OffsetTriangles(List<int> tris, int offset)
    {
        for (int i = 0; i < tris.Count; i++) tris[i] += offset;
    }

    #endregion
}