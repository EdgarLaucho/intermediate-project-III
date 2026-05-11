using UnityEngine;
using UnityEngine.Rendering;

// Draws animated attack-range rings for tower focus, upgrade preview, and placement preview.
// The indicator uses procedural meshes so it can scale cleanly to any tower range.
[DisallowMultipleComponent]
public sealed class TowerRangeIndicator : MonoBehaviour
{
    #region Inspector Fields

    [Header("Position")]
    [SerializeField] private float yOffset = 0.08f;

    [Header("Visuals")]
    [SerializeField] private bool drawSubtleFill = true;
    [SerializeField] private Color currentFillColor = new(0.22f, 0.72f, 1.00f, 0.045f);
    [SerializeField] private Color currentRingColor = new(0.70f, 0.96f, 1.00f, 1.00f);
    [SerializeField] private Color upgradeFillColor = new(0.48f, 1.00f, 0.36f, 0.032f);
    [SerializeField] private Color upgradeRingColor = new(0.80f, 1.00f, 0.36f, 1.00f);
    [SerializeField] private Color invalidRingColor = new(1.00f, 0.28f, 0.16f, 1.00f);

    [Header("Feel")]
    [SerializeField] private float fadeSpeed = 14f;
    [SerializeField] private float pulseSpeed = 2.2f;
    [SerializeField] private float pulseAmount = 0.06f;
    [SerializeField] private float currentRingRotationSpeed = 22f;
    [SerializeField] private float upgradeRingRotationSpeed = -16f;
    [SerializeField] private float invalidJitterDegrees = 2.2f;

    #endregion

    #region Runtime State

    private Mesh _discMesh;
    private Mesh _ringMesh;
    private Material _fillMaterial;
    private Material _ringMaterial;

    private Vector3 _center;
    private float _currentRange;
    private float _upgradeRange;
    private bool _hasUpgradeRange;
    private bool _isValid = true;
    private bool _requestedVisible;
    private float _visibility;

    // Placement preview is independent from Tower instances because the building may not exist yet.
    private bool  _inPlacementMode;
    private float _placementRange;

    #endregion

    #region Mesh Constants

    private const int Segments  = 96;
    private const int DashCount = 36;
    private const int DashSteps = 9;   // higher = smoother arcs at large range

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
        ConstructionEvents.OnTowerFocused        += OnTowerFocused;
        ConstructionEvents.OnTowerUpgradeHovered += OnTowerUpgradeHovered;
        ConstructionEvents.OnPlacementStarted    += OnPlacementStarted;
        ConstructionEvents.OnPlacementUpdated    += OnPlacementUpdated;
        ConstructionEvents.OnPlacementEnded      += OnPlacementEnded;
    }

    private void OnDisable()
    {
        ConstructionEvents.OnTowerFocused        -= OnTowerFocused;
        ConstructionEvents.OnTowerUpgradeHovered -= OnTowerUpgradeHovered;
        ConstructionEvents.OnPlacementStarted    -= OnPlacementStarted;
        ConstructionEvents.OnPlacementUpdated    -= OnPlacementUpdated;
        ConstructionEvents.OnPlacementEnded      -= OnPlacementEnded;
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
        if (data is TowerData td && td.towerStats.attackRange > 0f)
        {
            _inPlacementMode = true;
            _placementRange  = td.towerStats.attackRange;
            // Start hidden at origin until the first snapped placement position arrives.
            Show(Vector3.zero, _placementRange, 0f, false, true);
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
        _center           = args.WorldPos + Vector3.up * yOffset;
        _currentRange     = _placementRange;
        _isValid          = args.Validation.IsValid;
        _requestedVisible = _currentRange > 0f;
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
        float target = _requestedVisible && _currentRange > 0f ? 1f : 0f;
        _visibility = Mathf.MoveTowards(_visibility, target, fadeSpeed * Time.unscaledDeltaTime);
        if (_visibility <= 0f) return;

        float time = Time.unscaledTime;
        float pulse = 1f + Mathf.Sin(time * pulseSpeed) * pulseAmount;
        float alpha = Mathf.Clamp01(_visibility * pulse);
        float invalidJitter = _isValid ? 0f : Mathf.Sin(time * 22f) * invalidJitterDegrees;
        float currentRotation = time * currentRingRotationSpeed + invalidJitter;
        float upgradeRotation = time * upgradeRingRotationSpeed;

        if (_hasUpgradeRange && _upgradeRange > _currentRange + 0.05f)
        {
            DrawFillIfEnabled(_upgradeRange, WithAlpha(upgradeFillColor, upgradeFillColor.a * alpha));
            DrawRing(_upgradeRange, WithAlpha(upgradeRingColor, upgradeRingColor.a * alpha), upgradeRotation);
        }

        Color activeRingColor = _isValid ? currentRingColor : invalidRingColor;
        DrawFillIfEnabled(_currentRange, WithAlpha(currentFillColor, currentFillColor.a * alpha));
        DrawRing(_currentRange, WithAlpha(activeRingColor, activeRingColor.a * alpha), currentRotation);
    }

    #endregion

    #region Public API

    public void ShowBuildPreview(Vector3 worldCenter, float range, bool isValid)
    {
        Show(worldCenter, range, 0f, false, isValid);
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

        float upgradeRange = 0f;
        bool hasUpgradeRange = includeUpgradePreview
            && tower.TryPreviewNextUpgrade(out _, out upgradeRange, out _)
            && upgradeRange > tower.AttackRange + 0.05f;

        Show(tower.transform.position, tower.AttackRange, upgradeRange, hasUpgradeRange, true);
    }

    public void Hide()
    {
        _requestedVisible = false;
        _hasUpgradeRange = false;
    }

    #endregion

    #region State Helpers

    private void Show(Vector3 worldCenter, float currentRange, float upgradeRange, bool hasUpgradeRange, bool isValid)
    {
        _center = worldCenter + Vector3.up * yOffset;
        _currentRange = Mathf.Max(0f, currentRange);
        _upgradeRange = Mathf.Max(_currentRange, upgradeRange);
        _hasUpgradeRange = hasUpgradeRange;
        _isValid = isValid;
        _requestedVisible = _currentRange > 0f;
    }

    #endregion

    #region Drawing Helpers

    private void DrawFillIfEnabled(float range, Color color)
    {
        if (!drawSubtleFill || color.a <= 0.001f) return;
        DrawDisc(range, color);
    }

    private void DrawDisc(float range, Color color)
    {
        SetMaterialColor(_fillMaterial, color);
        Matrix4x4 matrix = Matrix4x4.TRS(_center, Quaternion.identity, new Vector3(range * 2f, 1f, range * 2f));
        Graphics.DrawMesh(_discMesh, matrix, _fillMaterial, 0);
    }

    private void DrawRing(float range, Color color, float rotationDegrees)
    {
        SetMaterialColor(_ringMaterial, color);
        Matrix4x4 matrix = Matrix4x4.TRS(_center + Vector3.up * 0.004f, Quaternion.Euler(0f, rotationDegrees, 0f), new Vector3(range * 2f, 1f, range * 2f));
        Graphics.DrawMesh(_ringMesh, matrix, _ringMaterial, 0);
    }

    #endregion

    #region Mesh Builders

    private static Mesh BuildDiscMesh()
    {
        var vertices = new Vector3[Segments + 1];
        var triangles = new int[Segments * 3];
        vertices[0] = Vector3.zero;

        for (int i = 0; i < Segments; i++)
        {
            float angle = i / (float)Segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
        }

        for (int i = 0; i < Segments; i++)
        {
            int next = i == Segments - 1 ? 1 : i + 2;
            int index = i * 3;
            triangles[index] = 0;
            triangles[index + 1] = next;
            triangles[index + 2] = i + 1;
        }

        var mesh = new Mesh { name = "TowerRangeDisc" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh BuildRingMesh()
    {
        const float outerRadius = 0.500f;
        const float innerRadius = 0.455f;  // 4.5% — thick stroke, reads clearly at any range
        const float dashFill    = 0.58f;   // visible gap between dashes

        // Each dash is a short curved strip built from paired outer/inner vertices.
        int vertsPerDash = (DashSteps + 1) * 2;
        int trisPerDash = DashSteps * 6;
        var vertices = new Vector3[DashCount * vertsPerDash];
        var triangles = new int[DashCount * trisPerDash];

        for (int dash = 0; dash < DashCount; dash++)
        {
            float center = (dash + 0.5f) / DashCount * Mathf.PI * 2f;
            float halfSpan = Mathf.PI * 2f / DashCount * dashFill * 0.5f;
            int vertexBase = dash * vertsPerDash;
            int triangleBase = dash * trisPerDash;

            for (int step = 0; step <= DashSteps; step++)
            {
                float t = step / (float)DashSteps;
                float angle = Mathf.Lerp(center - halfSpan, center + halfSpan, t);
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                vertices[vertexBase + step * 2] = new Vector3(x * outerRadius, 0f, z * outerRadius);
                vertices[vertexBase + step * 2 + 1] = new Vector3(x * innerRadius, 0f, z * innerRadius);
            }

            for (int step = 0; step < DashSteps; step++)
            {
                int outer = vertexBase + step * 2;
                int inner = outer + 1;
                int nextOuter = outer + 2;
                int nextInner = nextOuter + 1;
                int index = triangleBase + step * 6;

                triangles[index] = outer;
                triangles[index + 1] = inner;
                triangles[index + 2] = nextOuter;
                triangles[index + 3] = inner;
                triangles[index + 4] = nextInner;
                triangles[index + 5] = nextOuter;
            }
        }

        var mesh = new Mesh { name = "TowerRangeSegmentedRing" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
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