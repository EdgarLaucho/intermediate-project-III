using UnityEngine;
using System.Collections.Generic;

public class GridRenderer : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private GridManager grid;

    [Header("Layout")]
    [SerializeField] private float markerScale = 0.52f;
    [SerializeField] private float yOffset = 0.02f;

    [Header("Materials")]
    [SerializeField] private Material buildableMaterial;
    [SerializeField] private Material nexusMaterial;
    [SerializeField] private Material occupiedMaterial;

    #endregion

    #region Runtime State

    private readonly List<GameObject> _markers = new();
    private Sprite _cellSprite;

    private bool _subscribed;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        _cellSprite = CreateCellSprite();

        if (TryGetComponent(out MeshFilter meshFilter))
            meshFilter.sharedMesh = null;

        if (TryGetComponent(out MeshRenderer meshRenderer))
            meshRenderer.enabled = false;
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

        RebuildGrid();
        ApplyPhaseVisibility(PhaseEvents.CurrentPhase);
        Subscribe();
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void OnDestroy()
    {
        Unsubscribe();
        ClearMarkers();

        if (_cellSprite != null)
            Destroy(_cellSprite.texture);

        if (_cellSprite != null)
            Destroy(_cellSprite);
    }

    #endregion

    #region Event Subscriptions

    private void Subscribe()
    {
        if (_subscribed) return;
        ConstructionEvents.OnBuildingPlaced += OnBuildingPlaced;
        ConstructionEvents.OnBuildingDemolished += OnBuildingDemolished;
        PhaseEvents.OnPhaseChanged += ApplyPhaseVisibility;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        ConstructionEvents.OnBuildingPlaced -= OnBuildingPlaced;
        ConstructionEvents.OnBuildingDemolished -= OnBuildingDemolished;
        PhaseEvents.OnPhaseChanged -= ApplyPhaseVisibility;
        _subscribed = false;
    }

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
                AddMarker(cell, c, nexusMaterial, "Nexus");
            else if (cell.IsOccupied && cell.IsBuildable)
                AddMarker(cell, c, occupiedMaterial, "Occupied");
            else if (cell.IsBuildable)
                AddMarker(cell, c, buildableMaterial, "Buildable");
        }

        ApplyPhaseVisibility(PhaseEvents.CurrentPhase);
    }

    private void AddMarker(GridCell cell, Vector3 center, Material sourceMaterial, string category)
    {
        GameObject marker = new($"Grid {category} {cell.Coordinates.x},{cell.Coordinates.y}");
        marker.transform.SetParent(transform, false);
        marker.transform.position = center;
        marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        float size = grid.CellSize * markerScale;
        marker.transform.localScale = new Vector3(size, size, 1f);

        var renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = _cellSprite;
        renderer.color = ResolveMaterialColor(sourceMaterial, Color.white);
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

    #region Sprite Helpers

    private static Sprite CreateCellSprite()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "GridCellMarkerTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        var pixels = new Color32[size * size];
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        float half = size * 0.34f;
        float borderStart = size * 0.24f;
        float cornerRadius = size * 0.075f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new(x, y);
                Vector2 q = Abs(p - center) - new Vector2(half - cornerRadius, half - cornerRadius);
                float outside = Length(Max(q, Vector2.zero)) - cornerRadius;
                float edgeFade = Mathf.Clamp01(1f - outside / 2.2f);
                float border = Mathf.SmoothStep(0f, 1f, Mathf.Abs(Mathf.Max(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - borderStart) / 5.5f);
                float centerGlow = Mathf.Clamp01(1f - Vector2.Distance(p, center) / (size * 0.42f));
                byte alpha = (byte)Mathf.RoundToInt(255f * edgeFade * Mathf.Lerp(0.54f, 1f, border) * Mathf.Lerp(0.82f, 1f, centerGlow));
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Color ResolveMaterialColor(Material material, Color fallback)
    {
        if (material == null) return fallback;
        if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color")) return material.GetColor("_Color");
        return fallback;
    }

    private static Vector2 Abs(Vector2 value)
    {
        return new Vector2(Mathf.Abs(value.x), Mathf.Abs(value.y));
    }

    private static Vector2 Max(Vector2 value, Vector2 min)
    {
        return new Vector2(Mathf.Max(value.x, min.x), Mathf.Max(value.y, min.y));
    }

    private static float Length(Vector2 value)
    {
        return Mathf.Sqrt(value.x * value.x + value.y * value.y);
    }

    #endregion
}
