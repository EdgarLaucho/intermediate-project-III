using UnityEngine;
using UnityEngine.UIElements;

// Drawn radial command icon. This avoids font baseline drift and keeps the menu
// iconography crisp while the button styling remains in USS.
internal sealed class RadialMenuIcon : VisualElement
{
    #region Icon Types

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

    #endregion

    #region Runtime State

    private readonly Kind _kind;
    private bool _active = true;
    private bool _hovered;
    private bool _pressed;
    private bool _flashing;

    #endregion

    #region Constructor

    public RadialMenuIcon(Kind kind)
    {
        _kind = kind;
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Paint;
    }

    #endregion

    #region Public API

    public void SetState(bool active, bool hovered, bool pressed, bool flashing)
    {
        if (_active == active && _hovered == hovered && _pressed == pressed && _flashing == flashing)
            return;

        _active = active;
        _hovered = hovered;
        _pressed = pressed;
        _flashing = flashing;
        MarkDirtyRepaint();
    }

    #endregion

    #region Painting

    private void Paint(MeshGenerationContext ctx)
    {
        float size = Mathf.Min(layout.width, layout.height);
        if (size <= 0f) return;

        Painter2D painter = ctx.painter2D;
        Color fill = FillColor;
        Color stroke = StrokeColor;
        float scale = size / 56f;
        Vector2 offset = new((layout.width - 56f * scale) * 0.5f, (layout.height - 56f * scale) * 0.5f);

        // Icons are authored on a 56x56 virtual canvas, then scaled to the element size.
        using (new IconScope(painter, offset, scale))
        {
            if (_hovered || _flashing)
                DrawHalo(painter, fill);

            switch (_kind)
            {
                case Kind.Tower:
                    DrawTower(painter, fill, stroke);
                    break;
                case Kind.Trap:
                    DrawTrap(painter, fill, stroke);
                    break;
                case Kind.Wall:
                    DrawWall(painter, fill, stroke);
                    break;
                case Kind.Repair:
                    DrawRepair(painter, fill, stroke);
                    break;
                case Kind.Upgrade:
                    DrawUpgrade(painter, fill, stroke);
                    break;
                case Kind.Demolish:
                    DrawDemolish(painter, fill, stroke);
                    break;
                case Kind.Back:
                    DrawBack(painter, stroke);
                    break;
                case Kind.Cancel:
                    DrawCancel(painter, stroke);
                    break;
                default:
                    DrawBuild(painter, fill, stroke);
                    break;
            }
        }
    }

    #endregion

    #region State Colors

    private Color FillColor
    {
        get
        {
            if (!_active) return new Color(0.18f, 0.14f, 0.09f, 0.52f);
            if (_flashing) return new Color(0.97f, 0.54f, 0.08f, 0.98f);
            if (_hovered) return new Color(0.48f, 0.22f, 0.06f, 0.96f);
            return new Color(0.30f, 0.14f, 0.04f, 0.96f);
        }
    }

    private Color StrokeColor
    {
        get
        {
            if (_kind == Kind.Cancel) return CancelStrokeColor;
            if (!_active) return new Color(0.13f, 0.10f, 0.07f, 0.58f);
            if (_pressed) return new Color(0.07f, 0.03f, 0.01f, 1f);
            if (_flashing) return new Color(0.18f, 0.07f, 0.01f, 1f);
            if (_hovered) return new Color(0.11f, 0.045f, 0.01f, 1f);
            return new Color(0.20f, 0.09f, 0.025f, 0.98f);
        }
    }

    private Color CancelStrokeColor
    {
        get
        {
            // Cancel sits on the dark center button, so it needs a brighter palette
            // than the other icons that sit on tan command faces.
            if (!_active) return new Color(0.50f, 0.30f, 0.17f, 0.72f);
            if (_pressed) return new Color(1.00f, 0.42f, 0.10f, 1f);
            if (_flashing) return new Color(1.00f, 0.88f, 0.34f, 1f);
            if (_hovered) return new Color(1.00f, 0.68f, 0.20f, 1f);
            return new Color(0.96f, 0.38f, 0.11f, 0.98f);
        }
    }

    #endregion

    #region Icon Drawing

    private static void DrawHalo(Painter2D painter, Color color)
    {
        color.a *= 0.16f;
        painter.fillColor = color;
        painter.BeginPath();
        painter.Arc(P(28f, 28f), 23f, 0f, 360f);
        painter.ClosePath();
        painter.Fill();
    }

    private static void DrawTower(Painter2D painter, Color fill, Color stroke)
    {
        DrawRect(painter, 18f, 27f, 20f, 17f, fill, stroke);
        DrawRect(painter, 16f, 21f, 6f, 8f, fill, stroke);
        DrawRect(painter, 25f, 18f, 6f, 11f, fill, stroke);
        DrawRect(painter, 34f, 21f, 6f, 8f, fill, stroke);
        DrawRect(painter, 24f, 35f, 8f, 9f, new Color(fill.r * 0.55f, fill.g * 0.55f, fill.b * 0.55f, fill.a), stroke);
        StrokeLine(painter, P(18f, 32f), P(38f, 32f), stroke, 2f);
    }

    private static void DrawTrap(Painter2D painter, Color fill, Color stroke)
    {
        for (int index = 0; index < 5; index++)
        {
            float x = 13f + index * 7.5f;
            DrawTriangle(painter, P(x, 40f), P(x + 3.8f, 19f), P(x + 7.4f, 40f), fill, stroke);
        }

        StrokeLine(painter, P(12f, 40f), P(45f, 40f), stroke, 3f);
    }

    private static void DrawWall(Painter2D painter, Color fill, Color stroke)
    {
        DrawRect(painter, 13f, 17f, 30f, 25f, fill, stroke);
        StrokeLine(painter, P(13f, 25f), P(43f, 25f), stroke, 1.8f);
        StrokeLine(painter, P(13f, 34f), P(43f, 34f), stroke, 1.8f);
        StrokeLine(painter, P(23f, 17f), P(23f, 25f), stroke, 1.8f);
        StrokeLine(painter, P(34f, 17f), P(34f, 25f), stroke, 1.8f);
        StrokeLine(painter, P(18f, 25f), P(18f, 34f), stroke, 1.8f);
        StrokeLine(painter, P(31f, 25f), P(31f, 34f), stroke, 1.8f);
        StrokeLine(painter, P(25f, 34f), P(25f, 42f), stroke, 1.8f);
        StrokeLine(painter, P(37f, 34f), P(37f, 42f), stroke, 1.8f);
    }

    private static void DrawRepair(Painter2D painter, Color fill, Color stroke)
    {
        StrokeLine(painter, P(20f, 39f), P(34f, 25f), stroke, 5f);
        StrokeLine(painter, P(21f, 38f), P(35f, 24f), fill, 2.4f);
        DrawPolygon(painter, fill, stroke, P(27f, 17f), P(42f, 24f), P(38f, 31f), P(23f, 24f));
        StrokeLine(painter, P(15f, 22f), P(15f, 33f), stroke, 2.2f);
        StrokeLine(painter, P(9.5f, 27.5f), P(20.5f, 27.5f), stroke, 2.2f);
    }

    private static void DrawUpgrade(Painter2D painter, Color fill, Color stroke)
    {
        DrawTriangle(painter, P(28f, 12f), P(41f, 28f), P(33f, 28f), fill, stroke);
        DrawTriangle(painter, P(28f, 12f), P(23f, 28f), P(15f, 28f), fill, stroke);
        DrawRect(painter, 24.5f, 26f, 7f, 17f, fill, stroke);
        StrokeLine(painter, P(17f, 40f), P(28f, 47f), stroke, 2.4f);
        StrokeLine(painter, P(39f, 40f), P(28f, 47f), stroke, 2.4f);
    }

    private static void DrawDemolish(Painter2D painter, Color fill, Color stroke)
    {
        StrokeLine(painter, P(17f, 17f), P(39f, 39f), stroke, 6f);
        StrokeLine(painter, P(39f, 17f), P(17f, 39f), stroke, 6f);
        StrokeLine(painter, P(18.5f, 18.5f), P(37.5f, 37.5f), fill, 2.8f);
        StrokeLine(painter, P(37.5f, 18.5f), P(18.5f, 37.5f), fill, 2.8f);
        DrawTriangle(painter, P(12f, 28f), P(18f, 25f), P(16f, 33f), fill, stroke);
        DrawTriangle(painter, P(42f, 24f), P(47f, 20f), P(47f, 29f), fill, stroke);
    }

    private static void DrawBack(Painter2D painter, Color stroke)
    {
        StrokeLine(painter, P(38f, 18f), P(20f, 28f), stroke, 5f);
        StrokeLine(painter, P(20f, 28f), P(38f, 38f), stroke, 5f);
        StrokeLine(painter, P(23f, 28f), P(45f, 28f), stroke, 4f);
    }

    private static void DrawCancel(Painter2D painter, Color stroke)
    {
        Color shadow = new(0.08f, 0.025f, 0.005f, 0.76f);
        Color highlight = Color.Lerp(stroke, Color.white, 0.22f);

        StrokeLine(painter, P(17f, 17f), P(39f, 39f), shadow, 8f);
        StrokeLine(painter, P(39f, 17f), P(17f, 39f), shadow, 8f);
        StrokeLine(painter, P(18f, 18f), P(38f, 38f), stroke, 5f);
        StrokeLine(painter, P(38f, 18f), P(18f, 38f), stroke, 5f);
        StrokeLine(painter, P(19.5f, 19.5f), P(36.5f, 36.5f), highlight, 2f);
        StrokeLine(painter, P(36.5f, 19.5f), P(19.5f, 36.5f), highlight, 2f);
    }

    private static void DrawBuild(Painter2D painter, Color fill, Color stroke)
    {
        DrawRect(painter, 24f, 14f, 8f, 28f, fill, stroke);
        DrawRect(painter, 14f, 24f, 28f, 8f, fill, stroke);
        DrawDiamond(painter, P(28f, 28f), 7f, new Color(1f, 0.72f, 0.16f, fill.a), stroke);
    }

    #endregion

    #region Drawing Primitives

    private static void DrawRect(Painter2D painter, float x, float y, float width, float height, Color fill, Color stroke)
    {
        DrawPolygon(painter, fill, stroke, P(x, y), P(x + width, y), P(x + width, y + height), P(x, y + height));
    }

    private static void DrawDiamond(Painter2D painter, Vector2 center, float radius, Color fill, Color stroke)
    {
        DrawPolygon(painter, fill, stroke, P(center.x, center.y - radius), P(center.x + radius, center.y), P(center.x, center.y + radius), P(center.x - radius, center.y));
    }

    private static void DrawTriangle(Painter2D painter, Vector2 a, Vector2 b, Vector2 c, Color fill, Color stroke)
    {
        DrawPolygon(painter, fill, stroke, a, b, c);
    }

    private static void DrawPolygon(Painter2D painter, Color fill, Color stroke, params Vector2[] points)
    {
        if (points == null || points.Length < 3) return;

        painter.fillColor = fill;
        BeginPolygon(painter, points);
        painter.Fill();

        painter.strokeColor = stroke;
        painter.lineWidth = 1.7f;
        BeginPolygon(painter, points);
        painter.Stroke();
    }

    private static void BeginPolygon(Painter2D painter, params Vector2[] points)
    {
        painter.BeginPath();
        painter.MoveTo(points[0]);
        for (int index = 1; index < points.Length; index++)
            painter.LineTo(points[index]);
        painter.ClosePath();
    }

    private static void StrokeLine(Painter2D painter, Vector2 start, Vector2 end, Color color, float width)
    {
        painter.strokeColor = color;
        painter.lineWidth = width;
        painter.BeginPath();
        painter.MoveTo(start);
        painter.LineTo(end);
        painter.Stroke();
    }

    #endregion

    #region Coordinate Scope

    private static Vector2 P(float x, float y)
    {
        return IconScope.TransformPoint(new Vector2(x, y));
    }

    // Temporarily maps virtual 56x56 icon coordinates into the current element rect.
    private readonly struct IconScope : System.IDisposable
    {
        private static Vector2 _offset;
        private static float _scale;

        private readonly Vector2 _previousOffset;
        private readonly float _previousScale;

        public IconScope(Painter2D painter, Vector2 offset, float scale)
        {
            _previousOffset = _offset;
            _previousScale = _scale == 0f ? 1f : _scale;
            _offset = offset;
            _scale = scale;
        }

        public static Vector2 TransformPoint(Vector2 point)
        {
            float scale = _scale == 0f ? 1f : _scale;
            return _offset + point * scale;
        }

        public void Dispose()
        {
            _offset = _previousOffset;
            _scale = _previousScale;
        }
    }

    #endregion
}