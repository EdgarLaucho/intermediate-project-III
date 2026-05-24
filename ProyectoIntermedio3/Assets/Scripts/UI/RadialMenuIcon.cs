using UnityEngine;
using UnityEngine.UIElements;

public class RadialMenuIcon : VisualElement
{
    public enum Kind
    {
        Build,
        Tower,
        Trap,
        Wall,
        Repair,
        Upgrade,
        Demolish,
        Back,
        Cancel
    }

    private readonly Image _image;
    private bool _active = true;
    private bool _hovered;
    private bool _pressed;
    private bool _flashing;

    public RadialMenuIcon(Kind kind)
    {
        pickingMode = PickingMode.Ignore;

        _image = new Image
        {
            pickingMode = PickingMode.Ignore,
            scaleMode = ScaleMode.ScaleToFit,
            sprite = LoadSprite(kind),
        };
        _image.style.flexGrow = 1f;
        _image.style.width = Length.Percent(100f);
        _image.style.height = Length.Percent(100f);
        Add(_image);

        RefreshColor();
    }

    public void SetState(bool active, bool hovered, bool pressed, bool flashing)
    {
        if (_active == active && _hovered == hovered && _pressed == pressed && _flashing == flashing)
            return;

        _active = active;
        _hovered = hovered;
        _pressed = pressed;
        _flashing = flashing;
        RefreshColor();
    }

    private void RefreshColor()
    {
        _image.tintColor = IconColor;
    }

    private Color IconColor
    {
        get
        {
            if (!_active) return new Color(0.30f, 0.22f, 0.14f, 0.58f);
            if (_pressed) return new Color(0.08f, 0.03f, 0.01f, 1f);
            if (_flashing) return new Color(1.00f, 0.78f, 0.22f, 1f);
            if (_hovered) return new Color(0.18f, 0.07f, 0.01f, 1f);
            return new Color(0.22f, 0.09f, 0.025f, 0.98f);
        }
    }

    private static Sprite LoadSprite(Kind kind)
    {
        return Resources.Load<Sprite>($"RadialIcons/{ResourceName(kind)}");
    }

    private static string ResourceName(Kind kind)
    {
        return kind switch
        {
            Kind.Tower => "tower",
            Kind.Trap => "trap",
            Kind.Wall => "wall",
            Kind.Repair => "repair",
            Kind.Upgrade => "upgrade",
            Kind.Demolish => "demolish",
            Kind.Back => "back",
            Kind.Cancel => "cancel",
            _ => "build",
        };
    }
}