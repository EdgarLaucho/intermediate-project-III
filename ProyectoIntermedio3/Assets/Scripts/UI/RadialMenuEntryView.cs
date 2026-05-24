using UnityEngine;
using UnityEngine.UIElements;

public class RadialMenuEntryView
{
    public const float NodeSize = 76f;

    private const float MinLabelPlateWidth = 88f;
    private const float MaxLabelPlateWidth = 138f;

    public static float NodeRadius => NodeSize * 0.5f;

    private readonly VisualElement _node;
    private readonly VisualElement _face;
    private readonly RadialMenuIcon _icon;
    private readonly VisualElement _labelPlate;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Vector2 _labelSize;

    public Vector2 BaseLabelCenter { get; }

    public RadialMenuEntryView(RadialMenuElement.SectorData sector, Vector2 labelCenter, Vector2 labelSize)
    {
        _node = new VisualElement { pickingMode = PickingMode.Ignore };
        _node.AddToClassList("radial-entry-node");
        _node.style.position = Position.Absolute;
        _node.style.width = NodeSize;
        _node.style.height = NodeSize;
        _node.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);

        _face = new VisualElement { pickingMode = PickingMode.Ignore };
        _face.AddToClassList("radial-entry-face");
        _node.Add(_face);

        _icon = new RadialMenuIcon(ResolveIconKind(sector.Label)) { pickingMode = PickingMode.Ignore };
        _icon.AddToClassList("radial-entry-icon");
        _face.Add(_icon);

        _labelPlate = new VisualElement { pickingMode = PickingMode.Ignore };
        _labelPlate.AddToClassList("radial-entry-label-plate");
        _labelPlate.style.position = Position.Absolute;
        _labelPlate.style.width = labelSize.x;
        _labelPlate.style.height = labelSize.y;
        _labelPlate.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);

        var displayLabel = CompactLabel(sector.Label);
        _title = new Label(displayLabel) { pickingMode = PickingMode.Ignore };
        _title.AddToClassList("radial-entry-title");
        _title.style.fontSize = LabelFontSize(displayLabel);
        _labelPlate.Add(_title);

        if (!string.IsNullOrEmpty(sector.SubLabel))
        {
            _subtitle = new Label(sector.SubLabel) { pickingMode = PickingMode.Ignore };
            _subtitle.AddToClassList("radial-entry-subtitle");
            _labelPlate.Add(_subtitle);
        }

        BaseLabelCenter = labelCenter;
        _labelSize = labelSize;
    }

    public void AddTo(VisualElement parent)
    {
        parent.Add(_node);
        parent.Add(_labelPlate);
    }

    public void SetState(bool active, bool hovered, bool pressed, bool flashing)
    {
        ApplyStateClasses(_node, active, hovered, pressed, flashing);
        ApplyStateClasses(_face, active, hovered, pressed, flashing);
        ApplyStateClasses(_icon, active, hovered, pressed, flashing);
        _icon.SetState(active, hovered, pressed, flashing);
        ApplyStateClasses(_labelPlate, active, hovered, pressed, flashing);
        ApplyStateClasses(_title, active, hovered, pressed, flashing);
        if (_subtitle != null)
            ApplyStateClasses(_subtitle, active, hovered, pressed, flashing);
    }

    public void SetLayout(Vector2 nodeCenter, Vector2 labelCenter, float nodeScale, float labelScale)
    {
        SetCenteredRectAndScale(_node, nodeCenter, new Vector2(NodeSize, NodeSize), nodeScale);
        SetCenteredRectAndScale(_labelPlate, labelCenter, _labelSize, labelScale);
    }

    public static float LabelHeight(RadialMenuElement.SectorData sector)
    {
        return string.IsNullOrEmpty(sector.SubLabel) ? 24f : 38f;
    }

    public static Vector2 LabelSize(RadialMenuElement.SectorData sector, int sectorCount)
    {
        return new Vector2(LabelWidth(sector, sectorCount), LabelHeight(sector));
    }

    public static Rect LabelRect(Vector2 labelCenter, RadialMenuElement.SectorData sector, int sectorCount)
    {
        var size = LabelSize(sector, sectorCount);
        return new Rect(labelCenter.x - size.x * 0.5f, labelCenter.y - size.y * 0.5f, size.x, size.y);
    }

    private static void ApplyStateClasses(VisualElement element, bool active, bool hovered, bool pressed, bool flashing)
    {
        element.EnableInClassList("is-disabled", !active);
        element.EnableInClassList("is-hovered", hovered);
        element.EnableInClassList("is-pressed", pressed);
        element.EnableInClassList("is-flashing", flashing);
    }

    private static void SetCenteredRectAndScale(VisualElement element, Vector2 center, Vector2 size, float scale)
    {
        element.style.left = center.x - size.x * 0.5f;
        element.style.top = center.y - size.y * 0.5f;
        element.style.scale = new Scale(new Vector3(scale, scale, 1f));
    }

    private static float LabelWidth(RadialMenuElement.SectorData sector, int sectorCount)
    {
        var displayLabel = CompactLabel(sector.Label);
        var longest = Mathf.Max(displayLabel.Length, sector.SubLabel?.Length ?? 0);
        var width = 62f + longest * 5.8f;
        var maxWidth = sectorCount > 6 ? 116f : MaxLabelPlateWidth;
        return Mathf.Clamp(width, MinLabelPlateWidth, maxWidth);
    }

    private static int LabelFontSize(string label)
    {
        var length = label?.Length ?? 0;
        if (length > 16) return 8;
        if (length > 12) return 9;
        return 10;
    }

    private static string CompactLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return string.Empty;
        return label.Length <= 18 ? label : label.Substring(0, 17) + "...";
    }

    private static RadialMenuIcon.Kind ResolveIconKind(string label)
    {
        var lower = (label ?? string.Empty).ToLowerInvariant();

        if (lower.Contains("tower") || lower.Contains("torre")) return RadialMenuIcon.Kind.Tower;
        if (lower.Contains("trap") || lower.Contains("trampa")) return RadialMenuIcon.Kind.Trap;
        if (lower.Contains("wall") || lower.Contains("muro")) return RadialMenuIcon.Kind.Wall;
        if (lower.Contains("repair") || lower.Contains("repar")) return RadialMenuIcon.Kind.Repair;
        if (lower.Contains("upgrade") || lower.Contains("mejor") || lower.Contains("max")) return RadialMenuIcon.Kind.Upgrade;
        if (lower.Contains("demolish") || lower.Contains("demol")) return RadialMenuIcon.Kind.Demolish;
        if (lower.Contains("back") || lower.Contains("volver")) return RadialMenuIcon.Kind.Back;
        if (lower.Contains("cancel")) return RadialMenuIcon.Kind.Cancel;
        return RadialMenuIcon.Kind.Build;
    }
}