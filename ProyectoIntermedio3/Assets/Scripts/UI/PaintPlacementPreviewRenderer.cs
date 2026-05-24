using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
public sealed class PaintPlacementPreviewRenderer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Colors")]
    [SerializeField] private Color placeableColor = new(0.24f, 1.00f, 0.42f, 0.75f);
    [SerializeField] private Color placeableBorderColor = new(0.76f, 1.00f, 0.78f, 1.00f);
    [SerializeField] private Color noGoldColor = new(1.00f, 0.78f, 0.18f, 0.65f);
    [SerializeField] private Color invalidColor = new(1.00f, 0.18f, 0.14f, 0.65f);
    [SerializeField] private Color blockedBorderColor = new(1.00f, 0.46f, 0.30f, 1.00f);
    [SerializeField] private float yOffset = 0.095f;

    private readonly List<PaintPlacementCell> _cells = new();
    private Mesh _fillMesh;
    private Mesh _borderMesh;
    private Material _fillMaterial;
    private Material _borderMaterial;
    private VisualElement _tooltip;
    private Label _tooltipLabel;
    private string _tooltipText;
    private Color _tooltipAccent;
    private bool _visible;

    private void Awake()
    {
        _fillMesh = BuildFillMesh();
        _borderMesh = BuildBorderMesh();
        _fillMaterial = BuildMaterial(placeableColor, "PaintPlacementFill");
        _borderMaterial = BuildMaterial(placeableBorderColor, "PaintPlacementBorder");
        BuildTooltip();
    }

    private void OnEnable()
    {
        ConstructionEvents.OnPaintPlacementPreviewUpdated += HandlePreviewUpdated;
        ConstructionEvents.OnPaintPlacementPreviewEnded += Hide;
        ConstructionEvents.OnPlacementEnded += Hide;
    }

    private void OnDisable()
    {
        ConstructionEvents.OnPaintPlacementPreviewUpdated -= HandlePreviewUpdated;
        ConstructionEvents.OnPaintPlacementPreviewEnded -= Hide;
        ConstructionEvents.OnPlacementEnded -= Hide;
        Hide();
    }

    private void OnDestroy()
    {
        if (_fillMesh != null) Destroy(_fillMesh);
        if (_borderMesh != null) Destroy(_borderMesh);
        if (_fillMaterial != null) Destroy(_fillMaterial);
        if (_borderMaterial != null) Destroy(_borderMaterial);
        _tooltip?.RemoveFromHierarchy();
    }

    private void Update()
    {
        if (!_visible) return;

        float pulse = 0.86f + Mathf.Sin(Time.unscaledTime * 5.8f) * 0.14f;
        foreach (PaintPlacementCell cell in _cells)
            DrawCell(cell, pulse);

        PositionTooltip();
    }

    private void HandlePreviewUpdated(PaintPlacementPreviewArgs args)
    {
        _cells.Clear();
        if (args.Cells != null)
            _cells.AddRange(args.Cells);

        _visible = _cells.Count > 0;
        if (!_visible)
        {
            HideTooltip();
            return;
        }

        string name = args.Data != null ? args.Data.buildingName : "Build";
        _tooltipText = args.BlockedCount > 0
            ? $"{name} x{args.PlaceableCount}  {args.TotalCost}g\nGold after {args.RemainingGold}  ·  Blocked {args.BlockedCount}"
            : $"{name} x{args.PlaceableCount}  {args.TotalCost}g\nGold after {args.RemainingGold}";
        _tooltipAccent = args.BlockedCount > 0 ? noGoldColor : placeableBorderColor;
        ShowTooltip();
    }

    private void Hide()
    {
        _visible = false;
        _cells.Clear();
        HideTooltip();
    }

    private void DrawCell(PaintPlacementCell cell, float pulse)
    {
        Color fill = FillColorFor(cell);
        Color border = BorderColorFor(cell);
        fill.a *= pulse;
        border.a *= Mathf.Lerp(0.72f, 1f, pulse);

        SetMaterialColor(_fillMaterial, fill);
        SetMaterialColor(_borderMaterial, border);

        Vector3 center = cell.WorldPos + Vector3.up * yOffset;
        float fillScale = cell.CellSize * 0.84f;
        float borderScale = cell.CellSize * 0.96f;

        Graphics.DrawMesh(_fillMesh,
            Matrix4x4.TRS(center, Quaternion.identity, new Vector3(fillScale, 1f, fillScale)),
            _fillMaterial, 0);

        Graphics.DrawMesh(_borderMesh,
            Matrix4x4.TRS(center + Vector3.up * 0.01f, Quaternion.identity, new Vector3(borderScale, 1f, borderScale)),
            _borderMaterial, 0);
    }

    private Color FillColorFor(PaintPlacementCell cell)
    {
        if (cell.WillPlace) return placeableColor;
        return cell.Validation.State == BuildManager.PlacementState.InsufficientGold ? noGoldColor : invalidColor;
    }

    private Color BorderColorFor(PaintPlacementCell cell)
    {
        if (cell.WillPlace) return placeableBorderColor;
        return cell.Validation.State == BuildManager.PlacementState.InsufficientGold ? noGoldColor : blockedBorderColor;
    }

    private void BuildTooltip()
    {
        UIDocument doc = ResolveUIDocument();
        if (doc == null || doc.rootVisualElement == null) return;

        _tooltip = new VisualElement
        {
            pickingMode = PickingMode.Ignore,
        };
        _tooltip.style.position = Position.Absolute;
        _tooltip.style.backgroundColor = new StyleColor(new Color(0.03f, 0.05f, 0.10f, 0.90f));
        _tooltip.style.borderTopLeftRadius = 4;
        _tooltip.style.borderTopRightRadius = 4;
        _tooltip.style.borderBottomLeftRadius = 4;
        _tooltip.style.borderBottomRightRadius = 4;
        _tooltip.style.borderLeftWidth = 3;
        _tooltip.style.paddingLeft = 8;
        _tooltip.style.paddingRight = 8;
        _tooltip.style.paddingTop = 5;
        _tooltip.style.paddingBottom = 5;
        _tooltip.style.display = DisplayStyle.None;

        _tooltipLabel = new Label
        {
            pickingMode = PickingMode.Ignore,
        };
        _tooltipLabel.style.fontSize = 12;
        _tooltipLabel.style.color = new StyleColor(new Color(0.92f, 0.96f, 1.00f));
        _tooltipLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _tooltipLabel.style.whiteSpace = WhiteSpace.Normal;

        _tooltip.Add(_tooltipLabel);
        doc.rootVisualElement.Add(_tooltip);
        uiDocument = doc;
    }

    private UIDocument ResolveUIDocument()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
            return uiDocument;

        UIDocument fallback = null;
        foreach (UIDocument doc in FindObjectsByType<UIDocument>())
        {
            if (doc == null || doc.rootVisualElement == null)
                continue;

            fallback ??= doc;
            if (doc.rootVisualElement.Q<VisualElement>("radial-root") != null
                || doc.rootVisualElement.Q<Label>("gold-label") != null)
                return doc;
        }

        return fallback;
    }

    private void ShowTooltip()
    {
        if (_tooltip == null)
            BuildTooltip();

        if (_tooltip == null || _tooltipLabel == null) return;
        _tooltipLabel.text = _tooltipText;
        _tooltip.style.borderLeftColor = new StyleColor(_tooltipAccent);
        _tooltip.style.display = DisplayStyle.Flex;
    }

    private void HideTooltip()
    {
        if (_tooltip != null)
            _tooltip.style.display = DisplayStyle.None;
    }

    private void PositionTooltip()
    {
        if (_tooltip == null || _tooltip.style.display == DisplayStyle.None) return;
        if (uiDocument?.rootVisualElement?.panel == null || Mouse.current == null) return;

        Vector2 screen = Mouse.current.position.ReadValue();
        screen.y = Screen.height - screen.y;
        Vector2 panel = RuntimePanelUtils.ScreenToPanel(uiDocument.rootVisualElement.panel, screen);
        Rect panelRect = uiDocument.rootVisualElement.panel.visualTree.layout;
        float panelWidth = panelRect.width > 0f ? panelRect.width : Screen.width;
        float panelHeight = panelRect.height > 0f ? panelRect.height : Screen.height;
        float tipW = Mathf.Max(_tooltip.resolvedStyle.width, 132f);
        float tipH = Mathf.Max(_tooltip.resolvedStyle.height, 42f);

        const float offsetX = 18f;
        const float offsetY = 18f;
        float left = panel.x + offsetX;
        float top = panel.y + offsetY;

        if (left + tipW > panelWidth) left = panel.x - tipW - offsetX * 0.5f;
        if (top + tipH > panelHeight) top = panel.y - tipH - offsetY * 0.5f;

        _tooltip.style.left = Mathf.Clamp(left, 0f, Mathf.Max(0f, panelWidth - tipW));
        _tooltip.style.top = Mathf.Clamp(top, 0f, Mathf.Max(0f, panelHeight - tipH));
    }

    private static Mesh BuildFillMesh()
    {
        var mesh = new Mesh { name = "PaintPlacementFill" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, 0.5f),
            new Vector3(-0.5f, 0f, 0.5f),
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh BuildBorderMesh()
    {
        const float thickness = 0.055f;
        float inner = 0.5f - thickness;
        var verts = new Vector3[16];
        var tris = new int[24];

        SetStrip(verts, tris, 0, 0,
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, -inner), new Vector3(-0.5f, 0f, -inner));
        SetStrip(verts, tris, 4, 6,
            new Vector3(-0.5f, 0f, inner), new Vector3(0.5f, 0f, inner),
            new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f));
        SetStrip(verts, tris, 8, 12,
            new Vector3(-0.5f, 0f, -inner), new Vector3(-inner, 0f, -inner),
            new Vector3(-inner, 0f, inner), new Vector3(-0.5f, 0f, inner));
        SetStrip(verts, tris, 12, 18,
            new Vector3(inner, 0f, -inner), new Vector3(0.5f, 0f, -inner),
            new Vector3(0.5f, 0f, inner), new Vector3(inner, 0f, inner));

        var mesh = new Mesh { name = "PaintPlacementBorder" };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        return mesh;
    }

    private static void SetStrip(Vector3[] verts, int[] tris, int vBase, int tBase,
                                 Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        verts[vBase] = a;
        verts[vBase + 1] = b;
        verts[vBase + 2] = c;
        verts[vBase + 3] = d;
        tris[tBase] = vBase;
        tris[tBase + 1] = vBase + 2;
        tris[tBase + 2] = vBase + 1;
        tris[tBase + 3] = vBase;
        tris[tBase + 4] = vBase + 3;
        tris[tBase + 5] = vBase + 2;
    }

    private static Material BuildMaterial(Color color, string name)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default");
        var material = new Material(shader) { name = name };
        material.SetOverrideTag("RenderType", "Transparent");
        SetFloatIfSupported(material, "_Surface", 1f);
        SetFloatIfSupported(material, "_Blend", 0f);
        SetFloatIfSupported(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfSupported(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfSupported(material, "_ZWrite", 0f);
        SetFloatIfSupported(material, "_Cull", (float)CullMode.Off);
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
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }
}
