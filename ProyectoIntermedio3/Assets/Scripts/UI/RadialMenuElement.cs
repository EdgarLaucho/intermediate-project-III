using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class RadialMenuElement : VisualElement
{
    public float OuterRadius { get; set; } = 150f;
    public float InnerRadius { get; set; } = 54f;

    private const int CenterHit = -2;
    private const float OverflowBuffer = 28f;
    private const float HoverLift = 11f;
    private const float PressLift = 5f;
    private const float LabelLift = 11f;
    private const float LabelPlateGap = 15f;
    private const float OpenAnimMs = 120f;
    private const float OpenStartScale = 0.72f;
    private const float PressMs = 65f;
    private const float ConfirmFlashMs = 110f;

    public float ElementHalfSize => OuterRadius + OverflowBuffer;

    public event Action OnCenterClicked;
    public event Action<int> OnHoverChanged;
    public event Action<int> OnConfirm;

    private readonly List<SectorData> _sectors = new();
    private readonly List<RadialMenuEntryView> _entries = new();
    private RadialMenuBackdrop _backdrop;
    private RadialMenuCenterView _centerView;
    private int _hoveredIndex = -1;
    private int _pressedIndex = -1;
    private int _flashIndex = -1;
    private Vector2 _lastLocalPos;
    private bool _centerHovered;
    private bool _isOpening;
    private float _openStartTime;

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

    public void SetSectors(List<SectorData> sectors)
    {
        _sectors.Clear();
        if (sectors != null)
            _sectors.AddRange(sectors);

        ResetInteractionState();

        var size = ElementHalfSize * 2f;
        style.width = size;
        style.height = size;

        Clear();
        _entries.Clear();

        BuildBackdrop(size);
        BuildEntries();
        BuildCenterView();
        RefreshVisualState();
        PlayOpenAnimation();
    }

    private void ResetInteractionState()
    {
        _hoveredIndex = -1;
        _pressedIndex = -1;
        _flashIndex = -1;
        _centerHovered = false;
    }

    public override bool ContainsPoint(Vector2 localPoint)
    {
        return HitTest(localPoint) != -1;
    }

    private void BuildBackdrop(float size)
    {
        _backdrop = new RadialMenuBackdrop
        {
            OuterRadius = OuterRadius,
            InnerRadius = InnerRadius,
            OrbitRadius = CommandOrbitRadius,
            NodeRadius = RadialMenuEntryView.NodeRadius,
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
        var center = Center;
        var sectorCount = _sectors.Count;

        for (var index = 0; index < sectorCount; index++)
        {
            var sector = _sectors[index];
            var nodeCenter = CommandNodeCenter(center, index, sectorCount, false, false, false);
            var labelCenter = LabelCenterForNode(nodeCenter, index, sectorCount, sector);
            var labelSize = RadialMenuEntryView.LabelSize(sector, sectorCount);

            var view = new RadialMenuEntryView(sector, labelCenter, labelSize);
            _entries.Add(view);
            view.AddTo(this);
        }
    }

    private void BuildCenterView()
    {
        _centerView = new RadialMenuCenterView(Center, CenterSealRadius);
        _centerView.AddTo(this);
    }

    private void RefreshVisualState()
    {
        var sectorCount = Mathf.Min(_sectors.Count, _entries.Count);
        for (var index = 0; index < sectorCount; index++)
        {
            var sector = _sectors[index];
            var view = _entries[index];
            var active = sector.Interactable;
            var hovered = active && index == _hoveredIndex;
            var pressed = active && index == _pressedIndex;
            var flashing = index == _flashIndex;
            var emphasized = hovered || flashing;

            view.SetState(active, hovered, pressed, flashing);

            var direction = CommandDirection(index, sectorCount);
            var nodeCenter = CommandNodeCenter(Center, index, sectorCount, hovered || flashing, pressed, flashing);
            var labelCenter = view.BaseLabelCenter + direction * (emphasized ? LabelLift : 0f);
            var nodeScale = flashing ? 1.08f : hovered ? 1.04f : 1f;
            var labelScale = emphasized ? 1.035f : 1f;

            view.SetLayout(nodeCenter, labelCenter, nodeScale, labelScale);
        }

        _centerView?.SetHovered(_centerHovered);

        if (_backdrop != null)
        {
            _backdrop.HoveredIndex = _hoveredIndex;
            _backdrop.FlashIndex = _flashIndex;
            _backdrop.CenterHovered = _centerHovered;
        }
    }

    private void PlayOpenAnimation()
    {
        _isOpening = true;
        _openStartTime = Time.realtimeSinceStartup;
        style.opacity = 0f;
        style.scale = new Scale(new Vector3(OpenStartScale, OpenStartScale, 1f));

        schedule.Execute(() =>
        {
            var elapsedMs = (Time.realtimeSinceStartup - _openStartTime) * 1000f;
            var t = Mathf.Clamp01(elapsedMs / OpenAnimMs);
            var ease = EaseOutCubic(t);
            var scale = Mathf.Lerp(OpenStartScale, 1f, ease);
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
        var flashEndTime = Time.realtimeSinceStartupAsDouble * 1000.0 + ConfirmFlashMs;
        RefreshVisualState();

        schedule.Execute(() =>
        {
            if (Time.realtimeSinceStartupAsDouble * 1000.0 >= flashEndTime)
                _flashIndex = -1;

            RefreshVisualState();
        }).Every(16).Until(() => _flashIndex < 0);
    }

    private void OnPointerMove(PointerMoveEvent e)
    {
        _lastLocalPos = e.localPosition;
        var hit = HitTest(e.localPosition);
        var nowCenter = hit == CenterHit;

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

        var hit = HitTest(e.localPosition);
        if (hit == CenterHit)
            OnCenterClicked?.Invoke();
        else if (hit >= 0 && _sectors[hit].Interactable)
            PressThenConfirm(hit);

        e.StopPropagation();
    }

    private void OnWheel(WheelEvent e)
    {
        var sectorCount = _sectors.Count;
        if (sectorCount == 0) return;

        var direction = e.delta.y > 0 ? 1 : -1;
        var start = _hoveredIndex >= 0 ? _hoveredIndex : NearestInteractableTo(_lastLocalPos);
        for (var step = 1; step <= sectorCount; step++)
        {
            var index = ((start + direction * step) % sectorCount + sectorCount) % sectorCount;
            if (!_sectors[index].Interactable) continue;

            SetHover(index);
            e.StopPropagation();
            return;
        }
    }

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
        var action = _sectors[index].OnClick;
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

    private Vector2 Center => new(ElementHalfSize, ElementHalfSize);

    private float CenterSealRadius => Mathf.Max(30f, InnerRadius - 6f);

    private int HitTest(Vector2 local)
    {
        var sectorCount = _sectors.Count;
        if ((local - Center).magnitude <= CenterSealRadius + 7f)
            return CenterHit;

        for (var index = 0; index < sectorCount; index++)
        {
            var highlighted = index == _hoveredIndex && _sectors[index].Interactable;
            var pressed = index == _pressedIndex && _sectors[index].Interactable;
            var flashing = index == _flashIndex;
            var nodeCenter = CommandNodeCenter(Center, index, sectorCount, highlighted || flashing, pressed, flashing);
            var labelCenter = LabelCenterForNode(nodeCenter, index, sectorCount, _sectors[index]);
            var labelPlate = RadialMenuEntryView.LabelRect(labelCenter, _sectors[index], sectorCount);

            if ((local - nodeCenter).magnitude <= RadialMenuEntryView.NodeRadius + 10f || labelPlate.Contains(local))
                return index;
        }

        return -1;
    }

    private int NearestInteractableTo(Vector2 local)
    {
        var delta = local - Center;
        var angle = NormalizeAngle(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        var sectorCount = _sectors.Count;
        var bestIndex = 0;
        var bestDelta = float.MaxValue;

        for (var index = 0; index < sectorCount; index++)
        {
            if (!_sectors[index].Interactable) continue;

            var midAngle = NormalizeAngle(CommandAngle(index, sectorCount));
            var difference = Mathf.Abs(Mathf.DeltaAngle(angle, midAngle));
            if (difference >= bestDelta) continue;

            bestDelta = difference;
            bestIndex = index;
        }

        return bestIndex;
    }

    private float CommandOrbitRadius => Mathf.Max(CenterSealRadius + RadialMenuEntryView.NodeRadius + 26f, OuterRadius - RadialMenuEntryView.NodeRadius - 8f);

    private static Vector2 LabelCenterForNode(Vector2 nodeCenter, int index, int sectorCount, SectorData sector)
    {
        var direction = CommandDirection(index, sectorCount);
        var offset = RadialMenuEntryView.NodeRadius + LabelPlateGap + RadialMenuEntryView.LabelHeight(sector) * 0.5f;
        return direction.y < -0.45f
            ? nodeCenter + new Vector2(0f, -offset)
            : nodeCenter + new Vector2(0f, offset);
    }

    private Vector2 CommandNodeCenter(Vector2 center, int index, int sectorCount, bool highlighted, bool pressed, bool flashing)
    {
        var direction = CommandDirection(index, sectorCount);
        var lift = flashing ? HoverLift + 3f : highlighted ? HoverLift : pressed ? PressLift : 0f;
        return center + direction * (CommandOrbitRadius + lift);
    }

    private static Vector2 CommandDirection(int index, int sectorCount)
    {
        var angle = CommandAngle(index, sectorCount) * Mathf.Deg2Rad;
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

    private static float EaseOutCubic(float normalizedTime)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        return 1f - Mathf.Pow(1f - normalizedTime, 3f);
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
}