using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
public class PaintPlacementPreviewRenderer : MonoBehaviour
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

    private const string FillSpriteResource = "PaintPlacementFill";
    private const string BorderSpriteResource = "PaintPlacementBorder";
    private const float BorderYOffset = 0.01f;
    private static readonly Quaternion GroundRotation = Quaternion.Euler(90f, 0f, 0f);

    private readonly List<PaintPlacementCell> _previewCells = new();
    private readonly List<CellView> _cellViews = new();
    private Sprite _fillSprite;
    private Sprite _borderSprite;
    private VisualElement _tooltip;
    private Label _tooltipLabel;
    private string _tooltipText;
    private Color _tooltipAccent;
    private int _visibleCellCount;
    private bool _visible;

    private void Awake()
    {
        _fillSprite = Resources.Load<Sprite>(FillSpriteResource);
        _borderSprite = Resources.Load<Sprite>(BorderSpriteResource);
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
        _tooltip?.RemoveFromHierarchy();
    }

    private void Update()
    {
        _visibleCellCount = 0;

        if (_visible)
        {
            var pulse = 0.86f + Mathf.Sin(Time.unscaledTime * 5.8f) * 0.14f;
            for (var index = 0; index < _previewCells.Count; index++)
                DrawCell(_previewCells[index], pulse);

            PositionTooltip();
        }

        HideUnusedCells();
    }

    private void HandlePreviewUpdated(PaintPlacementPreviewArgs args)
    {
        _previewCells.Clear();
        if (args.Cells != null)
            _previewCells.AddRange(args.Cells);

        _visible = _previewCells.Count > 0;
        if (!_visible)
        {
            HideTooltip();
            return;
        }

        var buildingName = args.Data != null ? args.Data.buildingName : "Build";
        _tooltipText = args.BlockedCount > 0
            ? $"{buildingName} x{args.PlaceableCount}  {args.TotalCost}g\nGold after {args.RemainingGold}  -  Blocked {args.BlockedCount}"
            : $"{buildingName} x{args.PlaceableCount}  {args.TotalCost}g\nGold after {args.RemainingGold}";
        _tooltipAccent = args.BlockedCount > 0 ? noGoldColor : placeableBorderColor;
        ShowTooltip();
    }

    private void Hide()
    {
        _visible = false;
        _previewCells.Clear();
        HideTooltip();
    }

    private void DrawCell(PaintPlacementCell cell, float pulse)
    {
        var fill = FillColorFor(cell);
        var border = BorderColorFor(cell);
        fill.a *= pulse;
        border.a *= Mathf.Lerp(0.72f, 1f, pulse);

        var center = cell.WorldPos + Vector3.up * yOffset;
        var view = GetCellView();
        view.Root.SetActive(true);

        ConfigureRenderer(view.Fill, _fillSprite, center, cell.CellSize * 0.84f, fill, 0);
        ConfigureRenderer(view.Border, _borderSprite, center + Vector3.up * BorderYOffset, cell.CellSize * 0.96f, border, 1);
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

    private CellView GetCellView()
    {
        if (_visibleCellCount >= _cellViews.Count)
            _cellViews.Add(CreateCellView(_cellViews.Count));

        return _cellViews[_visibleCellCount++];
    }

    private CellView CreateCellView(int index)
    {
        var root = new GameObject($"PaintPlacementCell_{index}");
        root.transform.SetParent(transform, false);

        return new CellView
        {
            Root = root,
            Fill = CreateRenderer(root.transform, "Fill"),
            Border = CreateRenderer(root.transform, "Border"),
        };
    }

    private static SpriteRenderer CreateRenderer(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);

        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.enabled = false;
        return renderer;
    }

    private static void ConfigureRenderer(SpriteRenderer renderer, Sprite sprite, Vector3 position, float scale, Color color, int sortingOrder)
    {
        renderer.enabled = sprite != null;
        if (!renderer.enabled) return;

        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        renderer.transform.SetPositionAndRotation(position, GroundRotation);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void HideUnusedCells()
    {
        for (var index = _visibleCellCount; index < _cellViews.Count; index++)
            _cellViews[index].Root.SetActive(false);
    }

    private void BuildTooltip()
    {
        var document = ResolveUIDocument();
        if (document == null || document.rootVisualElement == null) return;

        _tooltip = new VisualElement { pickingMode = PickingMode.Ignore };
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

        _tooltipLabel = new Label { pickingMode = PickingMode.Ignore };
        _tooltipLabel.style.fontSize = 12;
        _tooltipLabel.style.color = new StyleColor(new Color(0.92f, 0.96f, 1.00f));
        _tooltipLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _tooltipLabel.style.whiteSpace = WhiteSpace.Normal;

        _tooltip.Add(_tooltipLabel);
        document.rootVisualElement.Add(_tooltip);
        uiDocument = document;
    }

    private UIDocument ResolveUIDocument()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
            return uiDocument;

        UIDocument fallback = null;
        var documents = FindObjectsByType<UIDocument>();
        for (var index = 0; index < documents.Length; index++)
        {
            var document = documents[index];
            if (document == null || document.rootVisualElement == null)
                continue;

            fallback ??= document;
            if (document.rootVisualElement.Q<VisualElement>("radial-root") != null
                || document.rootVisualElement.Q<Label>("gold-label") != null)
                return document;
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

        var screen = Mouse.current.position.ReadValue();
        screen.y = Screen.height - screen.y;

        var panel = RuntimePanelUtils.ScreenToPanel(uiDocument.rootVisualElement.panel, screen);
        var panelRect = uiDocument.rootVisualElement.panel.visualTree.layout;
        var panelWidth = panelRect.width > 0f ? panelRect.width : Screen.width;
        var panelHeight = panelRect.height > 0f ? panelRect.height : Screen.height;
        var tipWidth = Mathf.Max(_tooltip.resolvedStyle.width, 132f);
        var tipHeight = Mathf.Max(_tooltip.resolvedStyle.height, 42f);

        const float offsetX = 18f;
        const float offsetY = 18f;
        var left = panel.x + offsetX;
        var top = panel.y + offsetY;

        if (left + tipWidth > panelWidth)
            left = panel.x - tipWidth - offsetX * 0.5f;

        if (top + tipHeight > panelHeight)
            top = panel.y - tipHeight - offsetY * 0.5f;

        _tooltip.style.left = Mathf.Clamp(left, 0f, Mathf.Max(0f, panelWidth - tipWidth));
        _tooltip.style.top = Mathf.Clamp(top, 0f, Mathf.Max(0f, panelHeight - tipHeight));
    }

    private class CellView
    {
        public GameObject Root;
        public SpriteRenderer Fill;
        public SpriteRenderer Border;
    }
}