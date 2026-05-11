using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Hybrid UI Toolkit radial menu: C# handles radial geometry, hit testing, and callbacks;
// USS handles most of the visual styling for command nodes, labels, and state changes.
public sealed class RadialMenuElement : VisualElement
{
    #region Config

    public float OuterRadius { get; set; } = 150f;
    public float InnerRadius { get; set; } = 54f;

    private const int CenterHit = -2;
    private const float OverflowBuffer = 28f;
    private const float CommandNodeSize = 76f;
    private const float HoverLift = 11f;
    private const float PressLift = 5f;
    private const float LabelLift = 11f;
    private const float LabelPlateGap = 15f;
    private const float MinLabelPlateWidth = 88f;
    private const float MaxLabelPlateWidth = 138f;
    private const float OpenAnimMs = 120f;
    private const float OpenStartScale = 0.72f;
    private const float PressMs = 65f;
    private const float ConfirmFlashMs = 110f;
    private const int AmbientRepaintMs = 33;

    private static float CommandNodeRadius => CommandNodeSize * 0.5f;

    public float ElementHalfSize => OuterRadius + OverflowBuffer;

    #endregion

    #region Events

    public event Action OnCenterClicked;
    public event Action<int> OnHoverChanged;
    public event Action<int> OnConfirm;

    #endregion

    #region Runtime State

    private readonly List<SectorData> _sectors = new();
    private readonly List<EntryView> _entries = new();
    private RadialMenuBackdrop _backdrop;
    private VisualElement _centerButton;
    private RadialMenuIcon _centerIcon;
    private int _hoveredIndex = -1;
    private int _pressedIndex = -1;
    private int _flashIndex = -1;
    private Vector2 _lastLocalPos;
    private bool _centerHovered;
    private bool _isOpening;
    private float _openStartTime;

    #endregion

    #region Constructor

    public RadialMenuElement()
    {
        AddToClassList("radial-menu-element");
        RegisterCallback<PointerMoveEvent>(OnPointerMove);
        RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<WheelEvent>(OnWheel);
        style.position = Position.Absolute;
        style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);
        pickingMode = PickingMode.Position;
        focusable = true;
    }

    #endregion

    #region Public API

    public void SetSectors(List<SectorData> sectors)
    {
        _sectors.Clear();
        if (sectors != null)
            _sectors.AddRange(sectors);

        _hoveredIndex = -1;
        _pressedIndex = -1;
        _flashIndex = -1;
        _centerHovered = false;

        float size = ElementHalfSize * 2f;
        style.width = size;
        style.height = size;

        Clear();
        _entries.Clear();

        BuildBackdrop(size);
        BuildEntries();
        BuildCenterButton();
        RefreshVisualState();
        PlayOpenAnimation();
        StartAmbientRepaint();
    }

    public override bool ContainsPoint(Vector2 localPoint)
    {
        return HitTest(localPoint) != -1;
    }

    #endregion

    #region View Construction

    private void BuildBackdrop(float size)
    {
        _backdrop = new RadialMenuBackdrop
        {
            OuterRadius = OuterRadius,
            InnerRadius = InnerRadius,
            OrbitRadius = CommandOrbitRadius,
            NodeRadius = CommandNodeRadius,
            SectorCount = _sectors.Count,
        };

        _backdrop.style.position = Position.Absolute;
        _backdrop.style.left = 0f;
        _backdrop.style.top = 0f;
        _backdrop.style.width = size;
        _backdrop.style.height = size;
        Add(_backdrop);
    }

    private void BuildEntries()
    {
        Vector2 center = Center;
        int sectorCount = _sectors.Count;

        for (int index = 0; index < sectorCount; index++)
        {
            SectorData sector = _sectors[index];
            Vector2 nodeCenter = CommandNodeCenter(center, index, sectorCount, false, false, false);
            Vector2 labelCenter = LabelCenterForNode(nodeCenter, index, sectorCount, sector);
            Vector2 labelSize = new(LabelPlateWidth(sector, sectorCount), LabelPlateHeight(sector));

            EntryView view = CreateEntryView(sector, nodeCenter, labelCenter, labelSize);
            _entries.Add(view);
            Add(view.Node);
            Add(view.LabelPlate);
        }
    }

    private static EntryView CreateEntryView(SectorData sector, Vector2 nodeCenter, Vector2 labelCenter, Vector2 labelSize)
    {
        var node = new VisualElement { pickingMode = PickingMode.Ignore };
        node.AddToClassList("radial-entry-node");
        node.style.position = Position.Absolute;
        node.style.width = CommandNodeSize;
        node.style.height = CommandNodeSize;
        node.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);

        var face = new VisualElement { pickingMode = PickingMode.Ignore };
        face.AddToClassList("radial-entry-face");
        node.Add(face);

        var icon = new RadialMenuIcon(ResolveIconKind(sector.Label)) { pickingMode = PickingMode.Ignore };
        icon.AddToClassList("radial-entry-icon");
        face.Add(icon);

        var labelPlate = new VisualElement { pickingMode = PickingMode.Ignore };
        labelPlate.AddToClassList("radial-entry-label-plate");
        labelPlate.style.position = Position.Absolute;
        labelPlate.style.width = labelSize.x;
        labelPlate.style.height = labelSize.y;
        labelPlate.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);

        string displayLabel = CompactLabel(sector.Label);
        var title = new Label(displayLabel) { pickingMode = PickingMode.Ignore };
        title.AddToClassList("radial-entry-title");
        title.style.fontSize = LabelFontSize(displayLabel);
        labelPlate.Add(title);

        Label subtitle = null;
        if (!string.IsNullOrEmpty(sector.SubLabel))
        {
            subtitle = new Label(sector.SubLabel) { pickingMode = PickingMode.Ignore };
            subtitle.AddToClassList("radial-entry-subtitle");
            labelPlate.Add(subtitle);
        }

        return new EntryView
        {
            Node = node,
            Face = face,
            Icon = icon,
            LabelPlate = labelPlate,
            Title = title,
            Subtitle = subtitle,
            BaseNodeCenter = nodeCenter,
            BaseLabelCenter = labelCenter,
            LabelSize = labelSize,
        };
    }

    private void BuildCenterButton()
    {
        float size = CenterSealRadius * 2f;
        _centerButton = new VisualElement { pickingMode = PickingMode.Ignore };
        _centerButton.AddToClassList("radial-center-button");
        _centerButton.style.position = Position.Absolute;
        _centerButton.style.width = size;
        _centerButton.style.height = size;
        _centerButton.style.left = Center.x - size * 0.5f;
        _centerButton.style.top = Center.y - size * 0.5f;
        _centerButton.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);

        _centerIcon = new RadialMenuIcon(RadialMenuIcon.Kind.Cancel) { pickingMode = PickingMode.Ignore };
        _centerIcon.AddToClassList("radial-center-icon");
        _centerButton.Add(_centerIcon);
        Add(_centerButton);
    }

    #endregion

    #region Visual State

    private void RefreshVisualState()
    {
        int sectorCount = Mathf.Min(_sectors.Count, _entries.Count);
        for (int index = 0; index < sectorCount; index++)
        {
            SectorData sector = _sectors[index];
            EntryView view = _entries[index];
            bool active = sector.Interactable;
            bool hovered = active && index == _hoveredIndex;
            bool pressed = active && index == _pressedIndex;
            bool flashing = index == _flashIndex;
            bool emphasized = hovered || flashing;

            SetStateClasses(view, active, hovered, pressed, flashing);

            Vector2 direction = CommandDirection(index, sectorCount);
            Vector2 nodeCenter = CommandNodeCenter(Center, index, sectorCount, hovered || flashing, pressed, flashing);
            Vector2 labelCenter = view.BaseLabelCenter + direction * (emphasized ? LabelLift : 0f);
            float nodeScale = flashing ? 1.08f : hovered ? 1.04f : 1f;
            float labelScale = emphasized ? 1.035f : 1f;

            SetCenteredRect(view.Node, nodeCenter, new Vector2(CommandNodeSize, CommandNodeSize));
            view.Node.style.scale = new Scale(new Vector3(nodeScale, nodeScale, 1f));

            SetCenteredRect(view.LabelPlate, labelCenter, view.LabelSize);
            view.LabelPlate.style.scale = new Scale(new Vector3(labelScale, labelScale, 1f));
        }

        if (_centerButton != null)
        {
            _centerButton.EnableInClassList("is-hovered", _centerHovered);
            float centerScale = _centerHovered ? 1.06f : 1f;
            _centerButton.style.scale = new Scale(new Vector3(centerScale, centerScale, 1f));
            _centerIcon?.SetState(true, _centerHovered, false, false);
        }

        if (_backdrop != null)
        {
            _backdrop.HoveredIndex = _hoveredIndex;
            _backdrop.FlashIndex = _flashIndex;
            _backdrop.CenterHovered = _centerHovered;
            _backdrop.MarkDirtyRepaint();
        }
    }

    private static void SetStateClasses(EntryView view, bool active, bool hovered, bool pressed, bool flashing)
    {
        ApplyStateClasses(view.Node, active, hovered, pressed, flashing);
        ApplyStateClasses(view.Face, active, hovered, pressed, flashing);
        ApplyStateClasses(view.Icon, active, hovered, pressed, flashing);
        view.Icon.SetState(active, hovered, pressed, flashing);
        ApplyStateClasses(view.LabelPlate, active, hovered, pressed, flashing);
        ApplyStateClasses(view.Title, active, hovered, pressed, flashing);
        if (view.Subtitle != null)
            ApplyStateClasses(view.Subtitle, active, hovered, pressed, flashing);
    }

    private static void ApplyStateClasses(VisualElement element, bool active, bool hovered, bool pressed, bool flashing)
    {
        element.EnableInClassList("is-disabled", !active);
        element.EnableInClassList("is-hovered", hovered);
        element.EnableInClassList("is-pressed", pressed);
        element.EnableInClassList("is-flashing", flashing);
    }

    private static void SetCenteredRect(VisualElement element, Vector2 center, Vector2 size)
    {
        element.style.left = center.x - size.x * 0.5f;
        element.style.top = center.y - size.y * 0.5f;
    }

    #endregion

    #region Animations

    private void PlayOpenAnimation()
    {
        _isOpening = true;
        _openStartTime = Time.realtimeSinceStartup;
        style.opacity = 0f;
        style.scale = new Scale(new Vector3(OpenStartScale, OpenStartScale, 1f));

        schedule.Execute(() =>
        {
            float elapsedMs = (Time.realtimeSinceStartup - _openStartTime) * 1000f;
            float t = Mathf.Clamp01(elapsedMs / OpenAnimMs);
            float ease = EaseOutCubic(t);
            float scale = Mathf.Lerp(OpenStartScale, 1f, ease);
            style.opacity = ease;
            style.scale = new Scale(new Vector3(scale, scale, 1f));

            if (t >= 1f)
            {
                _isOpening = false;
                style.opacity = 1f;
                style.scale = new Scale(Vector3.one);
            }
        }).Every(16).Until(() => !_isOpening);
    }

    private void StartFlash(int index)
    {
        _flashIndex = index;
        double flashEndTime = Time.realtimeSinceStartupAsDouble * 1000.0 + ConfirmFlashMs;
        RefreshVisualState();

        schedule.Execute(() =>
        {
            if (Time.realtimeSinceStartupAsDouble * 1000.0 >= flashEndTime)
                _flashIndex = -1;

            RefreshVisualState();
        }).Every(16).Until(() => _flashIndex < 0);
    }

    private void StartAmbientRepaint()
    {
        schedule.Execute(() => _backdrop?.MarkDirtyRepaint()).Every(AmbientRepaintMs).Until(() => panel == null);
    }

    #endregion

    #region Pointer Events

    private void OnPointerMove(PointerMoveEvent e)
    {
        _lastLocalPos = e.localPosition;
        int hit = HitTest(e.localPosition);
        bool nowCenter = hit == CenterHit;

        if (nowCenter != _centerHovered)
        {
            _centerHovered = nowCenter;
            RefreshVisualState();
        }

        SetHover(hit >= 0 ? hit : -1);
    }

    private void OnPointerLeave(PointerLeaveEvent e)
    {
        if (_centerHovered)
            _centerHovered = false;

        SetHover(-1);
        RefreshVisualState();
    }

    private void OnPointerDown(PointerDownEvent e)
    {
        if (e.button != 0) return;

        int hit = HitTest(e.localPosition);
        if (hit == CenterHit)
            OnCenterClicked?.Invoke();
        else if (hit >= 0 && _sectors[hit].Interactable)
            PressThenConfirm(hit);

        e.StopPropagation();
    }

    private void OnWheel(WheelEvent e)
    {
        int sectorCount = _sectors.Count;
        if (sectorCount == 0) return;

        int direction = e.delta.y > 0 ? 1 : -1;
        int start = _hoveredIndex >= 0 ? _hoveredIndex : NearestInteractableTo(_lastLocalPos);
        for (int step = 1; step <= sectorCount; step++)
        {
            int index = ((start + direction * step) % sectorCount + sectorCount) % sectorCount;
            if (!_sectors[index].Interactable) continue;

            SetHover(index);
            e.StopPropagation();
            return;
        }
    }

    #endregion

    #region State Helpers

    private void SetHover(int index)
    {
        if (index == _hoveredIndex) return;

        _hoveredIndex = index;
        OnHoverChanged?.Invoke(index);
        RefreshVisualState();
    }

    private void Confirm(int index)
    {
        OnConfirm?.Invoke(index);
        Action action = _sectors[index].OnClick;
        StartFlash(index);
        schedule.Execute(() => action?.Invoke()).StartingIn((long)ConfirmFlashMs);
    }

    private void PressThenConfirm(int index)
    {
        _pressedIndex = index;
        RefreshVisualState();

        schedule.Execute(() =>
        {
            if (_pressedIndex == index)
                _pressedIndex = -1;

            RefreshVisualState();
            Confirm(index);
        }).StartingIn((long)PressMs);
    }

    #endregion

    #region Hit Testing

    private Vector2 Center => new(ElementHalfSize, ElementHalfSize);

    private float CenterSealRadius => Mathf.Max(30f, InnerRadius - 6f);

    private int HitTest(Vector2 local)
    {
        int sectorCount = _sectors.Count;
        if ((local - Center).magnitude <= CenterSealRadius + 7f)
            return CenterHit;

        for (int index = 0; index < sectorCount; index++)
        {
            bool highlighted = index == _hoveredIndex && _sectors[index].Interactable;
            bool pressed = index == _pressedIndex && _sectors[index].Interactable;
            bool flashing = index == _flashIndex;
            Vector2 nodeCenter = CommandNodeCenter(Center, index, sectorCount, highlighted || flashing, pressed, flashing);
            Vector2 labelCenter = LabelCenterForNode(nodeCenter, index, sectorCount, _sectors[index]);
            Rect labelPlate = LabelPlateRect(labelCenter, _sectors[index], sectorCount);

            if ((local - nodeCenter).magnitude <= CommandNodeRadius + 10f || labelPlate.Contains(local))
                return index;
        }

        return -1;
    }

    private int NearestInteractableTo(Vector2 local)
    {
        Vector2 delta = local - Center;
        float angle = NormalizeAngle(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        int sectorCount = _sectors.Count;
        int bestIndex = 0;
        float bestDelta = float.MaxValue;

        for (int index = 0; index < sectorCount; index++)
        {
            if (!_sectors[index].Interactable) continue;

            float midAngle = NormalizeAngle(CommandAngle(index, sectorCount));
            float difference = Mathf.Abs(Mathf.DeltaAngle(angle, midAngle));
            if (difference >= bestDelta) continue;

            bestDelta = difference;
            bestIndex = index;
        }

        return bestIndex;
    }

    #endregion

    #region Geometry & Text Helpers

    private float CommandOrbitRadius => Mathf.Max(CenterSealRadius + CommandNodeRadius + 26f, OuterRadius - CommandNodeRadius - 8f);

    private static Vector2 LabelCenterForNode(Vector2 nodeCenter, int index, int sectorCount, SectorData sector)
    {
        Vector2 direction = CommandDirection(index, sectorCount);
        float offset = CommandNodeRadius + LabelPlateGap + LabelPlateHeight(sector) * 0.5f;
        return direction.y < -0.45f
            ? nodeCenter + new Vector2(0f, -offset)
            : nodeCenter + new Vector2(0f, offset);
    }

    private static float LabelPlateWidth(SectorData sector, int sectorCount)
    {
        string displayLabel = CompactLabel(sector.Label);
        int longest = Mathf.Max(displayLabel.Length, sector.SubLabel?.Length ?? 0);
        float width = 62f + longest * 5.8f;
        float maxWidth = sectorCount > 6 ? 116f : MaxLabelPlateWidth;
        return Mathf.Clamp(width, MinLabelPlateWidth, maxWidth);
    }

    private static float LabelPlateHeight(SectorData sector)
    {
        return string.IsNullOrEmpty(sector.SubLabel) ? 24f : 38f;
    }

    private static int LabelFontSize(string label)
    {
        int length = label?.Length ?? 0;
        if (length > 16) return 8;
        if (length > 12) return 9;
        return 10;
    }

    private static string CompactLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return string.Empty;
        return label.Length <= 18 ? label : label.Substring(0, 17) + "...";
    }

    private static Rect LabelPlateRect(Vector2 labelCenter, SectorData sector, int sectorCount)
    {
        float width = LabelPlateWidth(sector, sectorCount);
        float height = LabelPlateHeight(sector);
        return new Rect(labelCenter.x - width * 0.5f, labelCenter.y - height * 0.5f, width, height);
    }

    private Vector2 CommandNodeCenter(Vector2 center, int index, int sectorCount, bool highlighted, bool pressed, bool flashing)
    {
        Vector2 direction = CommandDirection(index, sectorCount);
        float lift = flashing ? HoverLift + 3f : highlighted ? HoverLift : pressed ? PressLift : 0f;
        return center + direction * (CommandOrbitRadius + lift);
    }

    private static Vector2 CommandDirection(int index, int sectorCount)
    {
        float angle = CommandAngle(index, sectorCount) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static float CommandAngle(int index, int sectorCount)
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

        if (sectorCount == 4)
            return -135f + index * 90f;

        return -90f + index * (360f / sectorCount);
    }

    private static float NormalizeAngle(float angle)
    {
        return ((angle % 360f) + 360f) % 360f;
    }

    private static RadialMenuIcon.Kind ResolveIconKind(string label)
    {
        string lower = (label ?? string.Empty).ToLowerInvariant();

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

    private static float EaseOutCubic(float normalizedTime)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        return 1f - Mathf.Pow(1f - normalizedTime, 3f);
    }

    #endregion

    #region Nested Types

    private sealed class EntryView
    {
        public VisualElement Node;
        public VisualElement Face;
        public RadialMenuIcon Icon;
        public VisualElement LabelPlate;
        public Label Title;
        public Label Subtitle;
        public Vector2 BaseNodeCenter;
        public Vector2 BaseLabelCenter;
        public Vector2 LabelSize;
    }

    public readonly struct SectorData
    {
        public readonly string Label;
        public readonly string SubLabel;
        public readonly bool Interactable;
        public readonly Action OnClick;

        public SectorData(string label, string subLabel, bool interactable, Action onClick)
        {
            Label = label;
            SubLabel = subLabel;
            Interactable = interactable;
            OnClick = onClick;
        }
    }

    #endregion
}