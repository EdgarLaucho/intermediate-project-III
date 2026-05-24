using UnityEngine;
using UnityEngine.UIElements;

public class RadialMenuCenterView
{
    private readonly VisualElement _button;
    private readonly RadialMenuIcon _icon;

    public RadialMenuCenterView(Vector2 center, float radius)
    {
        var size = radius * 2f;
        _button = new VisualElement { pickingMode = PickingMode.Ignore };
        _button.AddToClassList("radial-center-button");
        _button.style.position = Position.Absolute;
        _button.style.width = size;
        _button.style.height = size;
        _button.style.left = center.x - size * 0.5f;
        _button.style.top = center.y - size * 0.5f;
        _button.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);

        _icon = new RadialMenuIcon(RadialMenuIcon.Kind.Cancel) { pickingMode = PickingMode.Ignore };
        _icon.AddToClassList("radial-center-icon");
        _button.Add(_icon);
    }

    public void AddTo(VisualElement parent)
    {
        parent.Add(_button);
    }

    public void SetHovered(bool hovered)
    {
        _button.EnableInClassList("is-hovered", hovered);
        var scale = hovered ? 1.06f : 1f;
        _button.style.scale = new Scale(new Vector3(scale, scale, 1f));
        _icon.SetState(true, hovered, false, false);
    }
}