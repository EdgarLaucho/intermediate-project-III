using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TowerRangeIndicator : MonoBehaviour
{
    [Header("Position")]
    [SerializeField] private float yOffset = 0.13f;

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

    private const string FillSpriteResource = "TowerRangeFill";
    private const string OutlineSpriteResource = "TowerRangeOutline";
    private const float OutlineYOffset = 0.004f;
    private static readonly Quaternion GroundRotation = Quaternion.Euler(90f, 0f, 0f);

    private readonly List<CellView> _cells = new();
    private Transform _poolRoot;
    private Sprite _fillSprite;
    private Sprite _outlineSprite;
    private Vector3 _center;
    private int _currentRange;
    private int _upgradeRange;
    private int _visibleCellCount;
    private bool _hasUpgradeRange;
    private bool _isValid = true;
    private bool _requestedVisible;
    private bool _inPlacementMode;
    private int _placementRange;
    private float _visibility;

    private void Awake()
    {
        _fillSprite = Resources.Load<Sprite>(FillSpriteResource);
        _outlineSprite = Resources.Load<Sprite>(OutlineSpriteResource);
        CreatePoolRoot();
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
        HideCells();
    }

    private void OnDestroy()
    {
        if (_poolRoot != null)
            Destroy(_poolRoot.gameObject);
    }

    private void LateUpdate()
    {
        var target = _requestedVisible && _currentRange > 0 ? 1f : 0f;
        _visibility = Mathf.MoveTowards(_visibility, target, fadeSpeed * Time.unscaledDeltaTime);
        _visibleCellCount = 0;

        if (_visibility > 0f)
        {
            var pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            var alpha = Mathf.Clamp01(_visibility * pulse);

            if (_hasUpgradeRange && _upgradeRange > _currentRange)
            {
                DrawCellArea(
                    _upgradeRange,
                    _currentRange,
                    WithAlpha(upgradeFillColor, upgradeFillColor.a * alpha),
                    WithAlpha(upgradeRingColor, upgradeRingColor.a * alpha));
            }

            var activeRingColor = _isValid ? currentRingColor : invalidRingColor;
            DrawCellArea(
                _currentRange,
                -1,
                WithAlpha(currentFillColor, currentFillColor.a * alpha),
                WithAlpha(activeRingColor, activeRingColor.a * alpha));
        }

        HideUnusedCells();
    }

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

        var upgradeRange = 0;
        var hasUpgradeRange = includeUpgradePreview
            && tower.TryPreviewNextUpgrade(out _, out upgradeRange, out _)
            && upgradeRange > tower.AttackRange;

        Show(tower.transform.position, tower.AttackRange, upgradeRange, hasUpgradeRange, true);
    }

    public void Hide()
    {
        _requestedVisible = false;
        _hasUpgradeRange = false;
    }

    private void OnTowerFocused(Tower tower)
    {
        if (_inPlacementMode) return;

        if (tower == null)
            Hide();
        else
            ShowCurrent(tower);
    }

    private void OnTowerUpgradeHovered(Tower tower)
    {
        if (!_inPlacementMode && tower != null)
            ShowUpgradePreview(tower);
    }

    private void OnPlacementStarted(BuildingData data)
    {
        var previewRange = 0;

        if (data is TowerData towerData && towerData.towerStats.attackRange > 0)
            previewRange = towerData.towerStats.attackRange;
        else if (data is TrapData trapData && trapData.role != TrapRole.Spikes && trapData.trapStats.effectRadius > 0)
            previewRange = trapData.trapStats.effectRadius;

        if (previewRange > 0)
        {
            _inPlacementMode = true;
            _placementRange = previewRange;
            Show(Vector3.zero, _placementRange, 0, false, true);
            return;
        }

        _inPlacementMode = false;
        Hide();
    }

    private void OnPlacementUpdated(PlacementUpdatedArgs args)
    {
        if (!_inPlacementMode) return;

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

    private void Show(Vector3 worldCenter, int currentRange, int upgradeRange, bool hasUpgradeRange, bool isValid)
    {
        _center = worldCenter + Vector3.up * yOffset;
        _currentRange = Mathf.Max(0, currentRange);
        _upgradeRange = Mathf.Max(_currentRange, upgradeRange);
        _hasUpgradeRange = hasUpgradeRange;
        _isValid = isValid;
        _requestedVisible = _currentRange > 0;
    }

    private void DrawCellArea(int outerRadius, int innerRadius, Color fillColor, Color outlineColor)
    {
        var cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        var drawFill = drawSubtleFill && fillColor.a > 0.001f && _fillSprite != null;
        var drawOutline = outlineColor.a > 0.001f && _outlineSprite != null;

        if (!drawFill && !drawOutline) return;

        for (var x = -outerRadius; x <= outerRadius; x++)
        {
            for (var z = -outerRadius; z <= outerRadius; z++)
            {
                var distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                if (distance > outerRadius || distance <= innerRadius) continue;

                var cellCenter = _center + new Vector3(x * cellSize, 0f, z * cellSize);
                DrawCell(cellCenter, cellSize, drawFill, fillColor, drawOutline, outlineColor);
            }
        }
    }

    private void DrawCell(Vector3 center, float cellSize, bool drawFill, Color fillColor, bool drawOutline, Color outlineColor)
    {
        var cell = GetCell();
        cell.Root.SetActive(true);

        ConfigureRenderer(cell.Fill, _fillSprite, center, cellSize, drawFill, fillColor, 20);
        ConfigureRenderer(cell.Outline, _outlineSprite, center + Vector3.up * OutlineYOffset, cellSize, drawOutline, outlineColor, 21);
    }

    private CellView GetCell()
    {
        if (_visibleCellCount >= _cells.Count)
            _cells.Add(CreateCell(_cells.Count));

        return _cells[_visibleCellCount++];
    }

    private CellView CreateCell(int index)
    {
        if (_poolRoot == null)
            CreatePoolRoot();

        var root = new GameObject($"TowerRangeCell_{index}");
        root.transform.SetParent(_poolRoot, false);

        return new CellView
        {
            Root = root,
            Fill = CreateRenderer(root.transform, "Fill"),
            Outline = CreateRenderer(root.transform, "Outline"),
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

    private void CreatePoolRoot()
    {
        var root = new GameObject($"{nameof(TowerRangeIndicator)}Sprites");
        _poolRoot = root.transform;
        _poolRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _poolRoot.localScale = Vector3.one;
    }

    private static void ConfigureRenderer(SpriteRenderer renderer, Sprite sprite, Vector3 position, float cellSize, bool visible, Color color, int sortingOrder)
    {
        renderer.enabled = visible;
        if (!visible) return;

        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        renderer.transform.SetPositionAndRotation(position, GroundRotation);
        var spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y, 0.001f);
        var scale = cellSize / spriteSize;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void HideUnusedCells()
    {
        for (var index = _visibleCellCount; index < _cells.Count; index++)
            _cells[index].Root.SetActive(false);
    }

    private void HideCells()
    {
        _visibleCellCount = 0;
        HideUnusedCells();
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private class CellView
    {
        public GameObject Root;
        public SpriteRenderer Fill;
        public SpriteRenderer Outline;
    }
}