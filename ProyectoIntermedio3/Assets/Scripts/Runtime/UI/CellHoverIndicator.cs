using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Draws a colored overlay on the grid cell currently under the cursor.
// Empty buildable cell: orange fill quad. Cell with building: cyan fill + border outline.
// Uses Graphics.DrawMesh every frame — no prefab needed, works with any URP setup.
// The optional UIDocument is only needed to show the tooltip overlay.
public class CellHoverIndicator : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private GridManager grid;
    [SerializeField] private UIDocument uiDocument; // optional — only needed for tooltip

    [Header("Colors")]
    [SerializeField] private Color buildableColor = new Color(1.00f, 0.66f, 0.12f, 0.26f); // orange
    [SerializeField] private Color occupiedColor = new Color(0.24f, 0.88f, 1.00f, 0.30f); // cyan
    [SerializeField] private Color invalidColor = new Color(1.00f, 0.12f, 0.10f, 0.32f); // red
    [SerializeField] private Color noGoldColor = new Color(1.00f, 0.78f, 0.12f, 0.30f); // yellow
    [SerializeField] private Color phaseColor = new Color(0.55f, 0.58f, 0.66f, 0.24f); // grey
    [SerializeField] private Color outlineColor = new Color(1.00f, 1.00f, 1.00f, 0.70f); // white border
    [SerializeField] private Color validPlacementColor = new Color(0.28f, 1.00f, 0.46f, 0.26f);
    [SerializeField] private Color validAccentColor = new Color(0.66f, 1.00f, 0.68f, 0.82f);

    [Header("Pulse")]
    [SerializeField] private float pulseSpeed = 5f;
    [SerializeField] private float pulseAmplitude = 0.14f; // how much alpha oscillates

    [Header("Feel")]
    [SerializeField] private float reticleRotationSpeed = 34f;
    [SerializeField] private float scanPulseSpeed = 2.8f;

    #endregion

    #region Runtime State

    private Mesh _fillMesh; // solid XZ quad
    private Mesh _borderMesh; // hollow XZ border (4 thin quads)
    private Mesh _cornerMesh;
    private Mesh _hatchMesh;
    private Material _fillMat;
    private Material _borderMat;
    private Material _accentMat;
    private Material _hatchMat;

    private bool _visible;
    private Color _currentColor;
    private Color _currentBorderColor;
    private Color _currentAccentColor;
    private Vector3 _currentCenter;
    private float _currentCellSize;
    private bool _drawBorder;
    private bool _drawCorners;
    private bool _drawHatch;
    private bool _drawReticle;
    private Vector2Int? _lastCoords;

    // Bounce on cell-change
    private float _bounceT = 999f;
    private const float BounceDur = 0.13f;
    private const float BounceOver = 0.10f;
    private const float BounceStartScale = 0.96f;

    // Tooltip
    private VisualElement _tooltip;
    private Label _tooltipLabel;
    private GridCell _tooltipCell;
    private BuildManager.PlacementValidation? _tooltipPlacement;
    private BuildingBase _observedTooltipBuilding;
    private int _lastTooltipHealth = int.MinValue;
    private int _lastTooltipMaxHealth = int.MinValue;
    private int _lastTooltipLevel = int.MinValue;
    private bool _lastTooltipMaxLevel;
    private bool _suppressTooltip;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        ConstructionEvents.OnCellHovered += OnCellHovered;
        ConstructionEvents.OnCellLost += Hide;
        ConstructionEvents.OnPlacementEnded += Hide;
        ConstructionEvents.OnPaintPlacementPreviewUpdated += SuppressTooltip;
        ConstructionEvents.OnPaintPlacementPreviewEnded += UnsuppressTooltip;
    }

    private void OnDisable()
    {
        ConstructionEvents.OnCellHovered -= OnCellHovered;
        ConstructionEvents.OnCellLost -= Hide;
        ConstructionEvents.OnPlacementEnded -= Hide;
        ConstructionEvents.OnPaintPlacementPreviewUpdated -= SuppressTooltip;
        ConstructionEvents.OnPaintPlacementPreviewEnded -= UnsuppressTooltip;
        StopObservingTooltipBuilding();
    }

    private void OnCellHovered(CellHoveredArgs args)
    {
        ShowCell(args.Coords, args.Cell, args.WorldPos, args.CellSize, args.Validation);
    }

    private void Start()
    {
        _fillMesh = BuildFillMesh();
        _borderMesh = BuildBorderMesh();
        _cornerMesh = BuildCornerMesh();
        _hatchMesh = BuildHatchMesh();
        _fillMat = BuildMaterial(new Color(1f, 0.6f, 0f, 0.75f));
        _borderMat = BuildMaterial(outlineColor);
        _accentMat = BuildMaterial(validAccentColor);
        _hatchMat = BuildMaterial(invalidColor);
        BuildTooltip();
    }

    private void OnDestroy()
    {
        if (_fillMesh != null) Destroy(_fillMesh);
        if (_borderMesh != null) Destroy(_borderMesh);
        if (_cornerMesh != null) Destroy(_cornerMesh);
        if (_hatchMesh != null) Destroy(_hatchMesh);
        if (_fillMat != null) Destroy(_fillMat);
        if (_borderMat != null) Destroy(_borderMat);
        if (_accentMat != null) Destroy(_accentMat);
        if (_hatchMat != null) Destroy(_hatchMat);
        StopObservingTooltipBuilding();
        _tooltip?.RemoveFromHierarchy();
    }

    private void Update()
    {
        if (!_visible || _fillMesh == null || _fillMat == null) return;

        RefreshTooltipIfNeeded();

        // ── Bounce scale ──────────────────────────────────────────────────────
        float bounce = 1f;
        if (_bounceT < BounceDur)
        {
            _bounceT += Time.deltaTime;
            float t = Mathf.Clamp01(_bounceT / BounceDur);
            bounce = Mathf.Lerp(BounceStartScale, 1f, EaseOutCubic(t))
                   + Mathf.Sin(t * Mathf.PI) * BounceOver;
        }

        // ── Pulse alpha ───────────────────────────────────────────────────────
        float alpha = _currentColor.a
                     * (1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude);
        Color fillC = _currentColor; fillC.a = Mathf.Clamp01(alpha);

        SetMaterialColor(_fillMat, fillC);

        // ── Draw fill ─────────────────────────────────────────────────────────
        float fillScale = _currentCellSize * 0.88f * bounce;
        Matrix4x4 fillMatrix = Matrix4x4.TRS(_currentCenter, Quaternion.identity, new Vector3(fillScale, 1f, fillScale));
        Graphics.DrawMesh(_fillMesh, fillMatrix, _fillMat, 0);

        float scanPulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * scanPulseSpeed);
        Color accentColor = _currentAccentColor;

        if (_drawHatch && _hatchMesh != null && _hatchMat != null)
        {
            Color hatchColor = _currentBorderColor;
            hatchColor.a = Mathf.Clamp01(hatchColor.a * (0.42f + scanPulse * 0.18f));
            SetMaterialColor(_hatchMat, hatchColor);
            float hatchScale = _currentCellSize * 0.78f * bounce;
            Matrix4x4 hatchMatrix = Matrix4x4.TRS(_currentCenter + Vector3.up * 0.003f, Quaternion.identity, new Vector3(hatchScale, 1f, hatchScale));
            Graphics.DrawMesh(_hatchMesh, hatchMatrix, _hatchMat, 0);
        }

        if (_drawReticle && _borderMesh != null && _accentMat != null)
        {
            accentColor.a = Mathf.Clamp01(_currentAccentColor.a * (0.16f + scanPulse * 0.16f));
            SetMaterialColor(_accentMat, accentColor);
            float reticleScale = _currentCellSize * Mathf.Lerp(0.38f, 0.50f, scanPulse) * bounce;
            Quaternion reticleRotation = Quaternion.Euler(0f, Time.unscaledTime * reticleRotationSpeed, 0f);
            Matrix4x4 reticleMatrix = Matrix4x4.TRS(_currentCenter + Vector3.up * 0.006f, reticleRotation, new Vector3(reticleScale, 1f, reticleScale));
            Graphics.DrawMesh(_borderMesh, reticleMatrix, _accentMat, 0);
        }

        if (_drawCorners && _cornerMesh != null && _accentMat != null)
        {
            accentColor = _currentAccentColor;
            accentColor.a = Mathf.Clamp01(_currentAccentColor.a * (0.72f + scanPulse * 0.24f));
            SetMaterialColor(_accentMat, accentColor);
            float cornerScale = _currentCellSize * Mathf.Lerp(0.94f, 1.00f, scanPulse) * bounce;
            Matrix4x4 cornerMatrix = Matrix4x4.TRS(_currentCenter + Vector3.up * 0.009f, Quaternion.identity, new Vector3(cornerScale, 1f, cornerScale));
            Graphics.DrawMesh(_cornerMesh, cornerMatrix, _accentMat, 0);
        }

        // ── Draw border if occupied ───────────────────────────────────────────
        if (_drawBorder && _borderMesh != null && _borderMat != null)
        {
            Color borderColor = _currentBorderColor;
            borderColor.a = Mathf.Clamp01(_currentBorderColor.a * (0.82f + scanPulse * 0.22f));
            SetMaterialColor(_borderMat, borderColor);
            float borderScale = _currentCellSize * 0.96f * bounce;
            Matrix4x4 borderMatrix = Matrix4x4.TRS(_currentCenter + Vector3.up * 0.012f, Quaternion.identity, new Vector3(borderScale, 1f, borderScale));
            Graphics.DrawMesh(_borderMesh, borderMatrix, _borderMat, 0);
        }

        if (!_suppressTooltip)
            PositionTooltip();
    }

    #endregion

    #region Public API

    public void UpdateCell(Vector2Int? coords)
    {
        UpdateCell(coords, null);
    }

    public void UpdateCell(Vector2Int? coords, BuildManager.PlacementValidation? placement)
    {
        if (coords == null) { Hide(); return; }

        if (grid == null)
        {
            Hide();
            return;
        }

        var cell = grid.GetCell(coords.Value);
        if (cell == null || (placement == null && !cell.IsBuildable && !cell.IsOccupied))
        {
            Hide();
            return;
        }

        ShowCell(coords.Value, cell, grid.GridToWorld(coords.Value), grid.CellSize, placement);
    }

    public void ShowCell(Vector2Int coords, GridCell cell, Vector3 worldCenter, float cellSize, BuildManager.PlacementValidation? placement)
    {
        if (cell == null || (placement == null && !cell.IsBuildable && !cell.IsOccupied))
        {
            Hide();
            return;
        }

        bool isNewCell = !_lastCoords.HasValue || _lastCoords.Value != coords;
        _lastCoords = coords;
        _currentColor = ResolveFillColor(cell, placement);
        _currentBorderColor = ResolveBorderColor(cell, placement);
        _currentAccentColor = ResolveAccentColor(cell, placement);
        _drawBorder = cell.IsOccupied || (placement.HasValue && !placement.Value.IsValid);
        _drawCorners = !cell.IsOccupied && (!placement.HasValue || placement.Value.IsValid);
        _drawHatch = placement.HasValue && !placement.Value.IsValid;
        _drawReticle = !cell.IsOccupied && (!placement.HasValue || placement.Value.State == BuildManager.PlacementState.Valid);
        _currentCenter = worldCenter + Vector3.up * 0.06f;
        _currentCellSize = cellSize;
        _visible = true;
        _tooltipCell = cell;
        _tooltipPlacement = placement;
        bool changedObservedBuilding = cell.CurrentBuilding != _observedTooltipBuilding;
        ObserveTooltipBuilding(cell, placement);

        if (isNewCell || placement.HasValue || changedObservedBuilding)
        {
            if (isNewCell) _bounceT = 0f;
            SetTooltipContent(cell, placement);
            if (!_suppressTooltip)
                ShowTooltip();
        }
    }

    public void Hide()
    {
        _visible = false;
        _lastCoords = null;
        _tooltipCell = null;
        _tooltipPlacement = null;
        _suppressTooltip = false;
        StopObservingTooltipBuilding();
        HideTooltip();
    }

    private void SuppressTooltip(PaintPlacementPreviewArgs args)
    {
        _suppressTooltip = true;
        HideTooltip();
    }

    private void UnsuppressTooltip()
    {
        _suppressTooltip = false;
    }

    #endregion

    #region Mesh Builders

    // Flat XZ quad, size 1×1, centered at origin.
    private static Mesh BuildFillMesh()
    {
        var mesh = new Mesh { name = "CellHoverFill" };
        mesh.vertices = new Vector3[] {
            new(-0.5f, 0f, -0.5f), new(0.5f, 0f, -0.5f),
            new(0.5f, 0f, 0.5f), new(-0.5f, 0f, 0.5f),
        };
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.uv = new Vector2[] {
            new(0,0), new(1,0), new(1,1), new(0,1)
        };
        mesh.RecalculateNormals();
        return mesh;
    }

    // Four thin XZ quads forming a hollow border ring (no center fill).
    private static Mesh BuildBorderMesh()
    {
        const float t = 0.055f; // border thickness (fraction of cell)
        float i = 0.5f - t; // inner half-extent

        var verts = new Vector3[16];
        var tris = new int[24];

        // Bottom strip
        SetStrip(verts, tris, 0, 0,
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, -i), new Vector3(-0.5f, 0f, -i));
        // Top strip
        SetStrip(verts, tris, 4, 6,
            new Vector3(-0.5f, 0f, i), new Vector3(0.5f, 0f, i),
            new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f));
        // Left strip
        SetStrip(verts, tris, 8, 12,
            new Vector3(-0.5f, 0f, -i), new Vector3(-i, 0f, -i),
            new Vector3(-i, 0f, i), new Vector3(-0.5f, 0f, i));
        // Right strip
        SetStrip(verts, tris, 12, 18,
            new Vector3(i, 0f, -i), new Vector3(0.5f, 0f, -i),
            new Vector3(0.5f, 0f, i), new Vector3(i, 0f, i));

        var mesh = new Mesh { name = "CellHoverBorder" };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh BuildCornerMesh()
    {
        const float halfExtent = 0.5f;
        const float thickness = 0.035f;
        const float length = 0.22f;
        float inner = halfExtent - thickness;
        float longEdge = halfExtent - length;

        var verts = new System.Collections.Generic.List<Vector3>(32);
        var tris = new System.Collections.Generic.List<int>(48);

        AddRectXZ(verts, tris, longEdge, halfExtent, inner, halfExtent);
        AddRectXZ(verts, tris, inner, halfExtent, longEdge, halfExtent);
        AddRectXZ(verts, tris, -halfExtent, -longEdge, inner, halfExtent);
        AddRectXZ(verts, tris, -halfExtent, -inner, longEdge, halfExtent);
        AddRectXZ(verts, tris, longEdge, halfExtent, -halfExtent, -inner);
        AddRectXZ(verts, tris, inner, halfExtent, -halfExtent, -longEdge);
        AddRectXZ(verts, tris, -halfExtent, -longEdge, -halfExtent, -inner);
        AddRectXZ(verts, tris, -halfExtent, -inner, -halfExtent, -longEdge);

        var mesh = new Mesh { name = "CellHoverCorners" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh BuildHatchMesh()
    {
        var verts = new System.Collections.Generic.List<Vector3>(24);
        var tris = new System.Collections.Generic.List<int>(36);
        Vector3 direction = new Vector3(1f, 0f, 1f).normalized;
        Vector3 perpendicular = new Vector3(-1f, 0f, 1f).normalized;

        for (int stripeIndex = -2; stripeIndex <= 2; stripeIndex++)
        {
            Vector3 center = perpendicular * (stripeIndex * 0.16f);
            AddSlantedStrip(verts, tris, center, direction, perpendicular, 0.42f, 0.018f);
        }

        var mesh = new Mesh { name = "CellHoverHatch" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    private static void AddRectXZ(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<int> tris,
                                  float minX, float maxX, float minZ, float maxZ)
    {
        int baseIndex = verts.Count;
        verts.Add(new Vector3(minX, 0f, minZ));
        verts.Add(new Vector3(maxX, 0f, minZ));
        verts.Add(new Vector3(maxX, 0f, maxZ));
        verts.Add(new Vector3(minX, 0f, maxZ));
        tris.Add(baseIndex); tris.Add(baseIndex + 2); tris.Add(baseIndex + 1);
        tris.Add(baseIndex); tris.Add(baseIndex + 3); tris.Add(baseIndex + 2);
    }

    private static void AddSlantedStrip(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<int> tris,
                                        Vector3 center, Vector3 direction, Vector3 perpendicular, float halfLength, float halfWidth)
    {
        int baseIndex = verts.Count;
        verts.Add(center - direction * halfLength - perpendicular * halfWidth);
        verts.Add(center + direction * halfLength - perpendicular * halfWidth);
        verts.Add(center + direction * halfLength + perpendicular * halfWidth);
        verts.Add(center - direction * halfLength + perpendicular * halfWidth);
        tris.Add(baseIndex); tris.Add(baseIndex + 2); tris.Add(baseIndex + 1);
        tris.Add(baseIndex); tris.Add(baseIndex + 3); tris.Add(baseIndex + 2);
    }

    private static void SetStrip(Vector3[] verts, int[] tris,
                                 int vBase, int tBase,
                                 Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        verts[vBase] = a; verts[vBase + 1] = b;
        verts[vBase + 2] = c; verts[vBase + 3] = d;
        tris[tBase] = vBase; tris[tBase + 1] = vBase + 2; tris[tBase + 2] = vBase + 1;
        tris[tBase + 3] = vBase; tris[tBase + 4] = vBase + 3; tris[tBase + 5] = vBase + 2;
    }

    #endregion

    #region Material Helpers

    // Creates a transparent unlit material compatible with URP and legacy pipelines.
    private static Material BuildMaterial(Color initialColor)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Unlit/Color")
                  ?? Shader.Find("Sprites/Default");

        var mat = new Material(shader) { name = "CellHoverMat" };
        mat.SetOverrideTag("RenderType", "Transparent");
        SetFloatIfSupported(mat, "_Surface", 1f);
        SetFloatIfSupported(mat, "_Blend", 0f);
        SetFloatIfSupported(mat, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfSupported(mat, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfSupported(mat, "_ZWrite", 0f);
        SetFloatIfSupported(mat, "_Cull", (float)CullMode.Off);
        SetFloatIfSupported(mat, "_AlphaClip", 0f);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        SetMaterialColor(mat, initialColor);
        return mat;
    }

    private static void SetFloatIfSupported(Material material, string property, float value)
    {
        if (material != null && material.HasProperty(property))
            material.SetFloat(property, value);
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null) return;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    #endregion

    #region Tooltip

    private void BuildTooltip()
    {
        if (uiDocument == null) return;

        _tooltip = new VisualElement();
        _tooltip.style.position = Position.Absolute;
        _tooltip.style.backgroundColor = new StyleColor(new Color(0.03f, 0.05f, 0.10f, 0.88f));
        _tooltip.style.borderTopLeftRadius = 4;
        _tooltip.style.borderTopRightRadius = 4;
        _tooltip.style.borderBottomLeftRadius = 4;
        _tooltip.style.borderBottomRightRadius = 4;
        _tooltip.style.paddingLeft = 8;
        _tooltip.style.paddingRight = 8;
        _tooltip.style.paddingTop = 5;
        _tooltip.style.paddingBottom = 5;
        _tooltip.style.borderLeftWidth = 3;
        _tooltip.style.borderLeftColor = new StyleColor(buildableColor);
        _tooltip.style.display = DisplayStyle.None;
        _tooltip.pickingMode = PickingMode.Ignore;

        _tooltipLabel = new Label();
        _tooltipLabel.style.fontSize = 12;
        _tooltipLabel.style.color = new StyleColor(new Color(0.92f, 0.95f, 1.00f));
        _tooltipLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _tooltipLabel.style.whiteSpace = WhiteSpace.Normal;
        _tooltipLabel.pickingMode = PickingMode.Ignore;

        _tooltip.Add(_tooltipLabel);
        uiDocument.rootVisualElement.Add(_tooltip);
    }

    private void SetTooltipContent(GridCell cell, BuildManager.PlacementValidation? placement)
    {
        if (_tooltipLabel == null) return;

        if (placement.HasValue && !placement.Value.IsValid)
        {
            Color c = TooltipColorFor(placement.Value.State);
            _tooltipLabel.text = placement.Value.Reason;
            _tooltipLabel.style.color = new StyleColor(c);
            _tooltip.style.borderLeftColor = new StyleColor(c);
            return;
        }

        if (cell.IsOccupied && cell.CurrentBuilding != null)
        {
            var b = cell.CurrentBuilding;
            string level = b.IsMaxLevel ? "MAX" : $"Lv {b.CurrentLevel + 1}";
            _tooltipLabel.text = $"{b.Data.buildingName}  {level}\nHP  {b.CurrentHealth} / {b.MaxHealth}";
            _tooltipLabel.style.color = new StyleColor(new Color(0.30f, 0.92f, 1.00f));
            _tooltip.style.borderLeftColor = new StyleColor(occupiedColor);
            CacheTooltipBuildingState(b);
        }
        else
        {
            _tooltipLabel.text = "Buildable";
            _tooltipLabel.style.color = new StyleColor(new Color(1.00f, 0.80f, 0.20f));
            _tooltip.style.borderLeftColor = new StyleColor(buildableColor);
            ResetTooltipBuildingState();
        }
    }

    private void ObserveTooltipBuilding(GridCell cell, BuildManager.PlacementValidation? placement)
    {
        BuildingBase building = placement.HasValue ? null : cell?.CurrentBuilding;
        if (_observedTooltipBuilding == building) return;

        StopObservingTooltipBuilding();
        _observedTooltipBuilding = building;

        if (_observedTooltipBuilding == null) return;

        _observedTooltipBuilding.OnHealthChanged += HandleObservedTooltipBuildingChanged;
        _observedTooltipBuilding.OnUpgraded += HandleObservedTooltipBuildingUpgraded;
        _observedTooltipBuilding.OnDeath += HandleObservedTooltipBuildingDeath;
        CacheTooltipBuildingState(_observedTooltipBuilding);
    }

    private void StopObservingTooltipBuilding()
    {
        if (_observedTooltipBuilding != null)
        {
            _observedTooltipBuilding.OnHealthChanged -= HandleObservedTooltipBuildingChanged;
            _observedTooltipBuilding.OnUpgraded -= HandleObservedTooltipBuildingUpgraded;
            _observedTooltipBuilding.OnDeath -= HandleObservedTooltipBuildingDeath;
        }

        _observedTooltipBuilding = null;
        ResetTooltipBuildingState();
    }

    private void HandleObservedTooltipBuildingChanged()
    {
        RefreshTooltipFromTrackedCell();
    }

    private void HandleObservedTooltipBuildingUpgraded(BuildingBase building, UpgradeLevelData upgradeData)
    {
        RefreshTooltipFromTrackedCell();
    }

    private void HandleObservedTooltipBuildingDeath(IDamageable damageable)
    {
        StopObservingTooltipBuilding();
        RefreshTooltipFromTrackedCell();
    }

    private void RefreshTooltipIfNeeded()
    {
        if (_tooltipCell == null || _tooltipLabel == null) return;

        BuildingBase building = _tooltipCell.CurrentBuilding;
        if (building != _observedTooltipBuilding)
        {
            ObserveTooltipBuilding(_tooltipCell, _tooltipPlacement);
            RefreshTooltipFromTrackedCell();
            return;
        }

        if (building == null) return;
        if (building.CurrentHealth == _lastTooltipHealth
            && building.MaxHealth == _lastTooltipMaxHealth
            && building.CurrentLevel == _lastTooltipLevel
            && building.IsMaxLevel == _lastTooltipMaxLevel)
            return;

        RefreshTooltipFromTrackedCell();
    }

    private void RefreshTooltipFromTrackedCell()
    {
        if (_tooltipCell == null || !_visible || _suppressTooltip) return;

        SetTooltipContent(_tooltipCell, _tooltipPlacement);
        ShowTooltip();
    }

    private void CacheTooltipBuildingState(BuildingBase building)
    {
        if (building == null)
        {
            ResetTooltipBuildingState();
            return;
        }

        _lastTooltipHealth = building.CurrentHealth;
        _lastTooltipMaxHealth = building.MaxHealth;
        _lastTooltipLevel = building.CurrentLevel;
        _lastTooltipMaxLevel = building.IsMaxLevel;
    }

    private void ResetTooltipBuildingState()
    {
        _lastTooltipHealth = int.MinValue;
        _lastTooltipMaxHealth = int.MinValue;
        _lastTooltipLevel = int.MinValue;
        _lastTooltipMaxLevel = false;
    }

    private void PositionTooltip()
    {
        if (_tooltip == null || _tooltip.style.display == DisplayStyle.None) return;
        if (uiDocument?.rootVisualElement?.panel == null) return;
        if (Mouse.current == null) return;

        Vector2 screen = Mouse.current.position.ReadValue();
        screen.y = Screen.height - screen.y;
        Vector2 panel = RuntimePanelUtils.ScreenToPanel(uiDocument.rootVisualElement.panel, screen);
        float tipH = Mathf.Max(_tooltip.resolvedStyle.height, 36f);
        float tipW = Mathf.Max(_tooltip.resolvedStyle.width, 90f);
        Rect panelRect = uiDocument.rootVisualElement.panel.visualTree.layout;

        const float offsetX = 20f;
        const float offsetY = 16f;

        float left = panel.x + offsetX;
        float top = panel.y + offsetY;

        // Flip left if tooltip would overflow right edge.
        if (left + tipW > panelRect.width) left = panel.x - tipW - offsetX * 0.5f;
        // Flip up if tooltip would overflow bottom edge.
        if (top + tipH > panelRect.height) top = panel.y - tipH - offsetY * 0.5f;

        _tooltip.style.left = Mathf.Clamp(left, 0f, Mathf.Max(0f, panelRect.width - tipW));
        _tooltip.style.top = Mathf.Clamp(top, 0f, Mathf.Max(0f, panelRect.height - tipH));
    }

    private void ShowTooltip() { if (_tooltip != null) _tooltip.style.display = DisplayStyle.Flex; }
    private void HideTooltip() { if (_tooltip != null) _tooltip.style.display = DisplayStyle.None; }

    #endregion

    #region Color Resolvers

    private Color ResolveFillColor(GridCell cell, BuildManager.PlacementValidation? placement)
    {
        if (placement.HasValue)
            return FillColorFor(placement.Value.State, cell);

        return cell.IsOccupied ? occupiedColor : buildableColor;
    }

    private Color ResolveBorderColor(GridCell cell, BuildManager.PlacementValidation? placement)
    {
        if (placement.HasValue && !placement.Value.IsValid)
            return TooltipColorFor(placement.Value.State);

        return cell.IsOccupied ? outlineColor : new Color(1f, 1f, 1f, 0.65f);
    }

    private Color ResolveAccentColor(GridCell cell, BuildManager.PlacementValidation? placement)
    {
        if (placement.HasValue)
        {
            if (placement.Value.State == BuildManager.PlacementState.Valid)
                return validAccentColor;

            Color invalidAccent = TooltipColorFor(placement.Value.State);
            invalidAccent.a = 0.78f;
            return invalidAccent;
        }

        return cell.IsOccupied ? occupiedColor : new Color(1.00f, 0.82f, 0.32f, 0.76f);
    }

    private Color FillColorFor(BuildManager.PlacementState state, GridCell cell)
    {
        return state switch
        {
            BuildManager.PlacementState.Valid => validPlacementColor,
            BuildManager.PlacementState.Occupied => invalidColor,
            BuildManager.PlacementState.InsufficientGold => noGoldColor,
            BuildManager.PlacementState.InvalidPhase => phaseColor,
            BuildManager.PlacementState.NotBuildable => invalidColor,
            BuildManager.PlacementState.OutOfBounds => invalidColor,
            _ => cell.IsOccupied ? occupiedColor : invalidColor,
        };
    }

    private Color TooltipColorFor(BuildManager.PlacementState state)
    {
        return state switch
        {
            BuildManager.PlacementState.InsufficientGold => new Color(1.00f, 0.82f, 0.24f),
            BuildManager.PlacementState.InvalidPhase => new Color(0.74f, 0.78f, 0.88f),
            BuildManager.PlacementState.Valid => new Color(1.00f, 0.80f, 0.20f),
            _ => new Color(1.00f, 0.34f, 0.28f),
        };
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    #endregion
}