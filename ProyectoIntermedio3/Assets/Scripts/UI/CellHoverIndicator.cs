using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class CellHoverIndicator : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private GridManager grid;
    [SerializeField] private UIDocument uiDocument; // optional, only needed for tooltip

    [Header("Sprites")]
    [SerializeField] private Sprite fillSprite;
    [SerializeField] private Sprite borderSprite;
    [SerializeField] private Sprite focusSprite;
    [SerializeField] private Sprite hatchSprite;

    [Header("Colors")]
    [SerializeField] private Color buildableColor = new(1.00f, 0.66f, 0.12f, 0.45f);
    [SerializeField] private Color occupiedColor = new(0.24f, 0.88f, 1.00f, 0.55f);
    [SerializeField] private Color invalidColor = new(1.00f, 0.12f, 0.10f, 0.65f);
    [SerializeField] private Color noGoldColor = new(1.00f, 0.78f, 0.12f, 0.62f);
    [SerializeField] private Color phaseColor = new(0.55f, 0.58f, 0.66f, 0.50f);
    [SerializeField] private Color outlineColor = new(1.00f, 1.00f, 1.00f, 0.90f);
    [SerializeField] private Color validPlacementColor = new(0.18f, 1.00f, 0.32f, 0.88f);
    [SerializeField] private Color validAccentColor = new(1.00f, 1.00f, 1.00f, 1.00f);

    [Header("Pulse")]
    [SerializeField] private float pulseSpeed = 5f;
    [SerializeField] private float pulseAmplitude = 0.14f;

    [Header("Feel")]
    [SerializeField] private float reticleRotationSpeed = 34f;
    [SerializeField] private float scanPulseSpeed = 2.8f;

    #endregion

    #region Runtime State

    private SpriteRenderer _fillRenderer;
    private SpriteRenderer _borderRenderer;
    private SpriteRenderer _focusRenderer;
    private SpriteRenderer _hatchRenderer;
    private SpriteRenderer _reticleRenderer;

    private bool _visible;
    private Color _currentColor;
    private Color _currentBorderColor;
    private Color _currentAccentColor;
    private Vector3 _currentCenter;
    private float _currentCellSize;
    private bool _drawBorder;
    private bool _drawFocusMarker;
    private bool _drawHatch;
    private bool _drawReticle;
    private Vector2Int? _lastCoords;

    private float _bounceT = 999f;
    private const float BounceDur = 0.13f;
    private const float BounceOver = 0.10f;
    private const float BounceStartScale = 0.96f;

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
        _fillRenderer = CreateLayer("Cell Hover Fill", fillSprite, 20);
        _hatchRenderer = CreateLayer("Cell Hover Hatch", hatchSprite, 21);
        _reticleRenderer = CreateLayer("Cell Hover Reticle", borderSprite, 22);
        _focusRenderer = CreateLayer("Cell Hover Focus", focusSprite, 23);
        _borderRenderer = CreateLayer("Cell Hover Border", borderSprite, 24);
        SetVisualsVisible(false);
        BuildTooltip();
    }

    private void OnDestroy()
    {
        StopObservingTooltipBuilding();
        _tooltip?.RemoveFromHierarchy();
    }

    private void Update()
    {
        if (!_visible || _fillRenderer == null) return;

        RefreshTooltipIfNeeded();

        var bounce = 1f;
        if (_bounceT < BounceDur)
        {
            _bounceT += Time.deltaTime;
            var t = Mathf.Clamp01(_bounceT / BounceDur);
            bounce = Mathf.Lerp(BounceStartScale, 1f, EaseOutCubic(t))
                   + Mathf.Sin(t * Mathf.PI) * BounceOver;
        }

        var alpha = _currentColor.a * (1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude);
        var fillColor = _currentColor;
        fillColor.a = Mathf.Clamp01(alpha);

        var scanPulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * scanPulseSpeed);
        DrawLayer(_fillRenderer, true, _currentCenter, _currentCellSize * 0.88f * bounce, fillColor, 0f);

        var hatchColor = _currentBorderColor;
        hatchColor.a = Mathf.Clamp01(hatchColor.a * (0.42f + scanPulse * 0.18f));
        DrawLayer(_hatchRenderer, _drawHatch, _currentCenter + Vector3.up * 0.003f,
            _currentCellSize * 0.78f * bounce, hatchColor, 0f);

        var reticleColor = _currentAccentColor;
        reticleColor.a = Mathf.Clamp01(_currentAccentColor.a * (0.16f + scanPulse * 0.16f));
        DrawLayer(_reticleRenderer, _drawReticle, _currentCenter + Vector3.up * 0.006f,
            _currentCellSize * Mathf.Lerp(0.38f, 0.50f, scanPulse) * bounce, reticleColor,
            Time.unscaledTime * reticleRotationSpeed);

        var focusColor = _currentAccentColor;
        focusColor.a = Mathf.Clamp01(_currentAccentColor.a * (0.58f + scanPulse * 0.20f));
        DrawLayer(_focusRenderer, _drawFocusMarker, _currentCenter + Vector3.up * 0.009f,
            _currentCellSize * Mathf.Lerp(0.72f, 0.82f, scanPulse) * bounce, focusColor, 0f);

        var borderColor = _currentBorderColor;
        borderColor.a = Mathf.Clamp01(_currentBorderColor.a * (0.82f + scanPulse * 0.22f));
        DrawLayer(_borderRenderer, _drawBorder, _currentCenter + Vector3.up * 0.012f,
            _currentCellSize * 0.96f * bounce, borderColor, 0f);

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

        var isNewCell = !_lastCoords.HasValue || _lastCoords.Value != coords;
        _lastCoords = coords;
        _currentColor = ResolveFillColor(cell, placement);
        _currentBorderColor = ResolveBorderColor(cell, placement);
        _currentAccentColor = ResolveAccentColor(cell, placement);
        _drawBorder = cell.IsOccupied || (placement.HasValue && !placement.Value.IsValid);
        _drawFocusMarker = !cell.IsOccupied && (!placement.HasValue || placement.Value.IsValid);
        _drawHatch = placement.HasValue && !placement.Value.IsValid;
        _drawReticle = !cell.IsOccupied && (!placement.HasValue || placement.Value.State == BuildManager.PlacementState.Valid);
        _currentCenter = worldCenter + Vector3.up * 0.06f;
        _currentCellSize = cellSize;
        _visible = true;
        SetVisualsVisible(true);
        _tooltipCell = cell;
        _tooltipPlacement = placement;
        var changedObservedBuilding = cell.CurrentBuilding != _observedTooltipBuilding;
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
        SetVisualsVisible(false);
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

    #region Sprite Visuals

    private SpriteRenderer CreateLayer(string layerName, Sprite sprite, int sortingOrder)
    {
        var layer = new GameObject(layerName);
        layer.transform.SetParent(transform, false);
        var renderer = layer.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        renderer.enabled = false;
        return renderer;
    }

    private static void DrawLayer(SpriteRenderer renderer, bool visible, Vector3 position, float size, Color color, float yawDegrees)
    {
        if (renderer == null) return;

        renderer.enabled = visible;
        if (!visible) return;

        renderer.color = color;
        renderer.transform.position = position;
        renderer.transform.rotation = Quaternion.Euler(90f, 0f, yawDegrees);
        renderer.transform.localScale = new Vector3(size, size, 1f);
    }

    private void SetVisualsVisible(bool visible)
    {
        SetRendererVisible(_fillRenderer, visible);
        SetRendererVisible(_hatchRenderer, visible && _drawHatch);
        SetRendererVisible(_reticleRenderer, visible && _drawReticle);
        SetRendererVisible(_focusRenderer, visible && _drawFocusMarker);
        SetRendererVisible(_borderRenderer, visible && _drawBorder);
    }

    private static void SetRendererVisible(SpriteRenderer renderer, bool visible)
    {
        if (renderer != null)
            renderer.enabled = visible;
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
            var c = TooltipColorFor(placement.Value.State);
            _tooltipLabel.text = placement.Value.Reason;
            _tooltipLabel.style.color = new StyleColor(c);
            _tooltip.style.borderLeftColor = new StyleColor(c);
            return;
        }

        if (cell.IsOccupied && cell.CurrentBuilding != null)
        {
            var b = cell.CurrentBuilding;
            var level = b.IsMaxLevel ? "MAX" : $"Lv {b.CurrentLevel + 1}";
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
        var building = placement.HasValue ? null : cell?.CurrentBuilding;
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

        var building = _tooltipCell.CurrentBuilding;
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

        var screen = Mouse.current.position.ReadValue();
        screen.y = Screen.height - screen.y;
        var panel = RuntimePanelUtils.ScreenToPanel(uiDocument.rootVisualElement.panel, screen);
        var tipH = Mathf.Max(_tooltip.resolvedStyle.height, 36f);
        var tipW = Mathf.Max(_tooltip.resolvedStyle.width, 90f);
        var panelRect = uiDocument.rootVisualElement.panel.visualTree.layout;

        const float offsetX = 20f;
        const float offsetY = 16f;

        var left = panel.x + offsetX;
        var top = panel.y + offsetY;

        if (left + tipW > panelRect.width) left = panel.x - tipW - offsetX * 0.5f;
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
        if (placement.HasValue && placement.Value.IsValid)
            return new Color(1f, 1f, 1f, 0.95f);

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

            var invalidAccent = TooltipColorFor(placement.Value.State);
            invalidAccent.a = 0.78f;
            return invalidAccent;
        }

        return cell.IsOccupied ? occupiedColor : new Color(1.00f, 0.82f, 0.32f, 0.92f);
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