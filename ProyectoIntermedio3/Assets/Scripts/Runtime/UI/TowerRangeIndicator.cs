using UnityEngine;
using UnityEngine.Rendering;

// Draws cell-based attack/effect ranges for tower focus, upgrade preview, and placement preview.
// The indicator uses procedural cell meshes so square grid ranges read as discrete tiles.
[DisallowMultipleComponent]
public sealed class TowerRangeIndicator : MonoBehaviour
{
    #region Inspector Fields

    [Header("Position")]
    [SerializeField] private float yOffset = 0.08f;

    [Header("Visuals")]
    [SerializeField] private bool drawSubtleFill = true;
    [SerializeField] private Color currentFillColor = new(0.22f, 0.72f, 1.00f, 0.070f);
    [SerializeField] private Color currentRingColor = new(0.70f, 0.96f, 1.00f, 0.90f);
    [SerializeField] private Color upgradeFillColor = new(0.48f, 1.00f, 0.36f, 0.055f);
    [SerializeField] private Color upgradeRingColor = new(0.80f, 1.00f, 0.36f, 0.90f);
    [SerializeField] private Color invalidRingColor = new(1.00f, 0.28f, 0.16f, 1.00f);

    [Header("Feel")]
    [SerializeField] private float fadeSpeed = 14f;
    [SerializeField] private float pulseSpeed = 2.2f;
    [SerializeField] private float pulseAmount = 0.035f;

    #endregion

    #region Runtime State

    private Mesh _discMesh;
    private Mesh _ringMesh;
    private Material _fillMaterial;
    private Material _ringMaterial;

    private Vector3 _center;
    // Range stored as cell count (integer). Converted to world units on draw.
    private int _currentRange;
    private int _upgradeRange;
    private bool _hasUpgradeRange;
    private bool _isValid = true;
    private bool _requestedVisible;
    private float _visibility;

    // Placement preview is independent from Tower instances because the building may not exist yet.
    private bool _inPlacementMode;
    private int _placementRange;

    #endregion

    #region Mesh Constants

    // Unit cell mesh dimensions. Values below 0.5 leave a visible gap between cells.
    private const float FillHalfExtent = 0.42f;
    private const float OutlineOuterHalfExtent = 0.47f;
    private const float OutlineInnerHalfExtent = 0.39f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _discMesh = BuildDiscMesh();
        _ringMesh = BuildRingMesh();
        _fillMaterial = BuildMaterial("TowerRangeFill", currentFillColor);
        _ringMaterial = BuildMaterial("TowerRangeRing", currentRingColor);
    }

    private void OnEnable()
    {
        ConstructionEvents.OnTowerFocused += OnTowerFocused;
        ConstructionEvents.OnTowerUpgradeHovered += OnTowerUpgradeHovered;
        ConstructionEvents.OnPlacementStarted += OnPlacementStarted;
        ConstructionEvents.OnPlacementUpdated += OnPlacementUpdated;
        ConstructionEvents.OnPlacementEnded += OnPlacementEnded;
    }

    private void OnDisable()
    {
        ConstructionEvents.OnTowerFocused -= OnTowerFocused;
        ConstructionEvents.OnTowerUpgradeHovered -= OnTowerUpgradeHovered;
        ConstructionEvents.OnPlacementStarted -= OnPlacementStarted;
        ConstructionEvents.OnPlacementUpdated -= OnPlacementUpdated;
        ConstructionEvents.OnPlacementEnded -= OnPlacementEnded;
    }

    #endregion

    #region Event Handlers

    private void OnTowerFocused(Tower tower)
    {
        // Placement owns the indicator while active, so focus events should not steal it.
        if (_inPlacementMode) return;
        if (tower == null) Hide();
        else ShowCurrent(tower);
    }

    private void OnTowerUpgradeHovered(Tower tower)
    {
        if (!_inPlacementMode && tower != null) ShowUpgradePreview(tower);
    }

    private void OnPlacementStarted(BuildingData data)
    {
        int previewRange = 0;

        if (data is TowerData td && td.towerStats.attackRange > 0)
            previewRange = td.towerStats.attackRange;
        else if (data is TrapData trapData && trapData.role != TrapRole.Spikes && trapData.trapStats.effectRadius > 0)
            previewRange = trapData.trapStats.effectRadius;

        if (previewRange > 0)
        {
            _inPlacementMode = true;
            _placementRange = previewRange;
            // Start hidden at origin until the first snapped placement position arrives.
            Show(Vector3.zero, _placementRange, 0, false, true);
        }
        else
        {
            _inPlacementMode = false;
            Hide();
        }
    }

    private void OnPlacementUpdated(PlacementUpdatedArgs args)
    {
        if (!_inPlacementMode) return;
        // Follow the snapped grid position every frame during placement preview.
        _center = args.WorldPos + Vector3.up * yOffset;
        _currentRange = _placementRange;
        _isValid = args.Validation.IsValid;
        _requestedVisible = _currentRange > 0;
    }

    private void OnPlacementEnded()
    {
        _inPlacementMode = false;
        Hide();
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        if (_discMesh != null) Destroy(_discMesh);
        if (_ringMesh != null) Destroy(_ringMesh);
        if (_fillMaterial != null) Destroy(_fillMaterial);
        if (_ringMaterial != null) Destroy(_ringMaterial);
    }

    #endregion

    #region Frame Rendering

    private void LateUpdate()
    {
        // Fade visibility instead of toggling instantly so range previews feel grounded.
        float target = _requestedVisible && _currentRange > 0 ? 1f : 0f;
        _visibility = Mathf.MoveTowards(_visibility, target, fadeSpeed * Time.unscaledDeltaTime);
        if (_visibility <= 0f) return;

        float time = Time.unscaledTime;
        float pulse = 1f + Mathf.Sin(time * pulseSpeed) * pulseAmount;
        float alpha = Mathf.Clamp01(_visibility * pulse);

        if (_hasUpgradeRange && _upgradeRange > _currentRange)
        {
            DrawCellArea(
                _upgradeRange,
                _currentRange,
                WithAlpha(upgradeFillColor, upgradeFillColor.a * alpha),
                WithAlpha(upgradeRingColor, upgradeRingColor.a * alpha));
        }

        Color activeRingColor = _isValid ? currentRingColor : invalidRingColor;
        DrawCellArea(
            _currentRange,
            -1,
            WithAlpha(currentFillColor, currentFillColor.a * alpha),
            WithAlpha(activeRingColor, activeRingColor.a * alpha));
    }

    #endregion

    #region Public API

    public void ShowBuildPreview(Vector3 worldCenter, int range, bool isValid)
    {
        Show(worldCenter, range, 0, false, isValid);
    }

    public void ShowCurrent(Tower tower)
    {
        ShowTower(tower, false);
    }

    public void ShowUpgradePreview(Tower tower)
    {
        ShowTower(tower, true);
    }

    public void HideUpgradePreview()
    {
        _hasUpgradeRange = false;
    }

    public void ShowTower(Tower tower, bool includeUpgradePreview)
    {
        if (tower == null)
        {
            Hide();
            return;
        }

        int upgradeRange = 0;
        bool hasUpgradeRange = includeUpgradePreview
            && tower.TryPreviewNextUpgrade(out _, out upgradeRange, out _)
            && upgradeRange > tower.AttackRange;

        Show(tower.transform.position, tower.AttackRange, upgradeRange, hasUpgradeRange, true);
    }

    public void Hide()
    {
        _requestedVisible = false;
        _hasUpgradeRange = false;
    }

    #endregion

    #region State Helpers

    private void Show(Vector3 worldCenter, int currentRange, int upgradeRange, bool hasUpgradeRange, bool isValid)
    {
        _center = worldCenter + Vector3.up * yOffset;
        _currentRange = Mathf.Max(0, currentRange);
        _upgradeRange = Mathf.Max(_currentRange, upgradeRange);
        _hasUpgradeRange = hasUpgradeRange;
        _isValid = isValid;
        _requestedVisible = _currentRange > 0;
    }

    #endregion

    #region Drawing Helpers

    // Draws every cell whose Chebyshev distance is <= outerRadius and > innerRadius.
    // This lets upgrade previews show only newly gained cells instead of repainting everything.
    private void DrawCellArea(int outerRadius, int innerRadius, Color fillColor, Color outlineColor)
    {
        float cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        Vector3 scale = new Vector3(cellSize, 1f, cellSize);

        bool drawFill = drawSubtleFill && fillColor.a > 0.001f;
        bool drawOutline = outlineColor.a > 0.001f;

        if (drawFill) SetMaterialColor(_fillMaterial, fillColor);
        if (drawOutline) SetMaterialColor(_ringMaterial, outlineColor);

        for (int x = -outerRadius; x <= outerRadius; x++)
        {
            for (int z = -outerRadius; z <= outerRadius; z++)
            {
                int distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                if (distance > outerRadius || distance <= innerRadius) continue;

                Vector3 cellCenter = _center + new Vector3(x * cellSize, 0f, z * cellSize);
                if (drawFill)
                {
                    Graphics.DrawMesh(_discMesh,
                        Matrix4x4.TRS(cellCenter, Quaternion.identity, scale),
                        _fillMaterial, 0);
                }

                if (drawOutline)
                {
                    Graphics.DrawMesh(_ringMesh,
                        Matrix4x4.TRS(cellCenter + Vector3.up * 0.004f, Quaternion.identity, scale),
                        _ringMaterial, 0);
                }
            }
        }
    }

    #endregion

    #region Mesh Builders

    // Inset unit-cell fill. Scaled to GridManager.CellSize on draw.
    private static Mesh BuildDiscMesh()
    {
        var vertices = new Vector3[]
        {
            new(-FillHalfExtent, 0f, -FillHalfExtent),
            new(FillHalfExtent, 0f, -FillHalfExtent),
            new(FillHalfExtent, 0f, FillHalfExtent),
            new(-FillHalfExtent, 0f, FillHalfExtent),
        };
        var triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        var mesh = new Mesh { name = "RangeSquareFill" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    // Inset unit-cell outline: a hollow border with a small gap between neighbouring cells.
    // 8 vertices (4 outer + 4 inner), 8 triangles (2 per side).
    private static Mesh BuildRingMesh()
    {
        const float o = OutlineOuterHalfExtent;
        const float i = OutlineInnerHalfExtent;

        var v = new Vector3[]
        {
            new(-o, 0f, -o), // 0 outer corners
            new(o, 0f, -o), // 1
            new(o, 0f, o), // 2
            new(-o, 0f, o), // 3
            new(-i, 0f, -i), // 4 inner corners
            new(i, 0f, -i), // 5
            new(i, 0f, i), // 6
            new(-i, 0f, i), // 7
        };
        var t = new int[]
        {
            0, 1, 5, 0, 5, 4, // top side   (–Z)
            1, 2, 6, 1, 6, 5, // right side (+X)
            2, 3, 7, 2, 7, 6, // bottom side(+Z)
            3, 0, 4, 3, 4, 7, // left side  (–X)
        };
        var mesh = new Mesh { name = "RangeSquareOutline" };
        mesh.vertices = v;
        mesh.triangles = t;
        mesh.RecalculateNormals();
        return mesh;
    }

    #endregion

    #region Material Helpers

    private static Material BuildMaterial(string materialName, Color color)
    {
        // Prefer URP unlit but keep fallbacks so the indicator survives render-pipeline changes.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default");

        var material = new Material(shader) { name = materialName };
        material.SetOverrideTag("RenderType", "Transparent");
        SetFloatIfSupported(material, "_Surface", 1f);
        SetFloatIfSupported(material, "_Blend", 0f);
        SetFloatIfSupported(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfSupported(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfSupported(material, "_ZWrite", 0f);
        SetFloatIfSupported(material, "_Cull", (float)CullMode.Off);
        SetFloatIfSupported(material, "_AlphaClip", 0f);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        SetMaterialColor(material, color);
        return material;
    }

    private static void SetFloatIfSupported(Material material, string property, float value)
    {
        if (material != null && material.HasProperty(property))
            material.SetFloat(property, value);
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null) return;
        // URP uses _BaseColor; legacy/simple shaders often use _Color.
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    #endregion
}