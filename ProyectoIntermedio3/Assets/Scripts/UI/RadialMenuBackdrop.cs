using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class RadialMenuBackdrop : VisualElement
{
    private static readonly Color ShadowSoft = new(0.025f, 0.018f, 0.012f, 0.24f);
    private static readonly Color CompassLeather = new(0.105f, 0.072f, 0.047f, 0.68f);
    private static readonly Color CompassEdge = new(0.55f, 0.34f, 0.13f, 0.54f);
    private static readonly Color AmberGlow = new(1.00f, 0.58f, 0.18f, 0.28f);

    private readonly List<VisualElement> _connectors = new();
    private VisualElement _shadow;
    private VisualElement _orbitOuter;
    private VisualElement _orbitInner;

    private float _outerRadius;
    private float _innerRadius;
    private float _orbitRadius;
    private float _nodeRadius;
    private int _sectorCount;
    private int _hoveredIndex = -1;
    private int _flashIndex = -1;
    private bool _centerHovered;

    public float OuterRadius { get => _outerRadius; set { _outerRadius = value; Rebuild(); } }
    public float InnerRadius { get => _innerRadius; set { _innerRadius = value; Rebuild(); } }
    public float OrbitRadius { get => _orbitRadius; set { _orbitRadius = value; Rebuild(); } }
    public float NodeRadius { get => _nodeRadius; set { _nodeRadius = value; Rebuild(); } }
    public int SectorCount { get => _sectorCount; set { _sectorCount = value; Rebuild(); } }
    public int HoveredIndex { get => _hoveredIndex; set { _hoveredIndex = value; RefreshState(); } }
    public int FlashIndex { get => _flashIndex; set { _flashIndex = value; RefreshState(); } }
    public bool CenterHovered { get => _centerHovered; set { _centerHovered = value; RefreshState(); } }

    public RadialMenuBackdrop()
    {
        AddToClassList("radial-backdrop");
        pickingMode = PickingMode.Ignore;
        RegisterCallback<GeometryChangedEvent>(_ => Rebuild());
    }

    private void Rebuild()
    {
        if (layout.width <= 0f || layout.height <= 0f) return;

        Clear();
        _connectors.Clear();

        var center = new Vector2(layout.width * 0.5f, layout.height * 0.5f);
        _shadow = CreateCircle("radial-backdrop-shadow", center + new Vector2(0f, 11f), OuterRadius + 25f, ShadowSoft, 0f);
        _orbitOuter = CreateCircle("radial-backdrop-orbit-outer", center, OrbitRadius, Color.clear, 4.8f);
        _orbitInner = CreateCircle("radial-backdrop-orbit-inner", center, OrbitRadius, Color.clear, 1.1f);
        Add(_shadow);

        if (SectorCount >= 2)
        {
            Add(_orbitOuter);
            Add(_orbitInner);
        }

        for (var index = 0; index < SectorCount; index++)
        {
            var direction = DirectionFor(index, SectorCount);
            var from = center + direction * (Mathf.Max(30f, InnerRadius - 6f) + 6f);
            var to = center + direction * (OrbitRadius - NodeRadius - 4f);
            var connector = CreateLine(from, to, CompassLeather, 2.2f);
            _connectors.Add(connector);
            Add(connector);
        }

        RefreshState();
    }

    private void RefreshState()
    {
        if (_orbitOuter != null)
            SetBorder(_orbitOuter, new Color(0.08f, 0.055f, 0.035f, CenterHovered ? 0.32f : 0.22f), 4.8f);

        if (_orbitInner != null)
            SetBorder(_orbitInner, CenterHovered ? AmberGlow : CompassEdge, 1.1f);

        for (var index = 0; index < _connectors.Count; index++)
        {
            var emphasized = index == HoveredIndex || index == FlashIndex;
            SetLine(_connectors[index], emphasized ? AmberGlow : CompassLeather, emphasized ? 3.2f : 2.2f);
        }
    }

    private static VisualElement CreateCircle(string className, Vector2 center, float radius, Color fill, float borderWidth)
    {
        var element = new VisualElement { pickingMode = PickingMode.Ignore };
        element.AddToClassList(className);
        element.style.position = Position.Absolute;
        element.style.left = center.x - radius;
        element.style.top = center.y - radius;
        element.style.width = radius * 2f;
        element.style.height = radius * 2f;
        element.style.backgroundColor = new StyleColor(fill);
        element.style.borderTopLeftRadius = radius;
        element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = radius;
        element.style.borderBottomRightRadius = radius;
        SetBorder(element, CompassEdge, borderWidth);
        return element;
    }

    private static VisualElement CreateLine(Vector2 from, Vector2 to, Color color, float width)
    {
        var element = new VisualElement { pickingMode = PickingMode.Ignore };
        var delta = to - from;
        var length = delta.magnitude;
        element.style.position = Position.Absolute;
        element.style.left = from.x;
        element.style.top = from.y - width * 0.5f;
        element.style.width = length;
        element.style.height = width;
        element.style.transformOrigin = new TransformOrigin(Length.Percent(0f), Length.Percent(50f), 0f);
        element.style.rotate = new Rotate(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        SetLine(element, color, width);
        return element;
    }

    private static void SetBorder(VisualElement element, Color color, float width)
    {
        element.style.borderTopColor = new StyleColor(color);
        element.style.borderRightColor = new StyleColor(color);
        element.style.borderBottomColor = new StyleColor(color);
        element.style.borderLeftColor = new StyleColor(color);
        element.style.borderTopWidth = width;
        element.style.borderRightWidth = width;
        element.style.borderBottomWidth = width;
        element.style.borderLeftWidth = width;
    }

    private static void SetLine(VisualElement element, Color color, float width)
    {
        element.style.backgroundColor = new StyleColor(color);
        element.style.height = width;
    }

    private static Vector2 DirectionFor(int index, int sectorCount)
    {
        var angle = CommandAngleFor(index, sectorCount) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static float CommandAngleFor(int index, int sectorCount)
    {
        if (sectorCount <= 1) return 0f;
        if (sectorCount == 2) return index == 0 ? 0f : 180f;
        if (sectorCount == 3)
        {
            return index switch
            {
                0 => -90f,
                1 => 30f,
                _ => 150f
            };
        }

        return sectorCount == 4 ? -135f + index * 90f : -90f + index * (360f / sectorCount);
    }
}