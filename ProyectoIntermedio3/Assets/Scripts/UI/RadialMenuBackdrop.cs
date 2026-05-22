using UnityEngine;
using UnityEngine.UIElements;

// Lightweight Painter2D layer for the radial menu background.
// Buttons, labels, colors, and interaction states live in USS-backed VisualElements.
internal sealed class RadialMenuBackdrop : VisualElement
{
    private const float AmbientSweepSpeed = 38f;
    private const float AmbientPulseSpeed = 2.7f;

    private static readonly Color ShadowSoft = new(0.025f, 0.018f, 0.012f, 0.24f);
    private static readonly Color CompassLeather = new(0.105f, 0.072f, 0.047f, 0.68f);
    private static readonly Color CompassEdge = new(0.55f, 0.34f, 0.13f, 0.54f);
    private static readonly Color BrassBright = new(1.00f, 0.78f, 0.30f, 0.84f);
    private static readonly Color AmberGlow = new(1.00f, 0.58f, 0.18f, 0.20f);
    private static readonly Color SweepShadow = new(0.13f, 0.045f, 0.005f, 0.34f);

    public float OuterRadius;
    public float InnerRadius;
    public float OrbitRadius;
    public float NodeRadius;
    public int SectorCount;
    public int HoveredIndex = -1;
    public int FlashIndex = -1;
    public bool CenterHovered;

    public RadialMenuBackdrop()
    {
        AddToClassList("radial-backdrop");
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Paint;
    }

    private void Paint(MeshGenerationContext ctx)
    {
        Painter2D painter = ctx.painter2D;
        Vector2 center = new(layout.width * 0.5f, layout.height * 0.5f);

        DrawShadow(painter, center);
        DrawConnectorLines(painter, center);
        DrawOrbitGuide(painter, center);
        DrawAmbientSweep(painter, center);
    }

    private void DrawShadow(Painter2D painter, Vector2 center)
    {
        painter.fillColor = ShadowSoft;
        painter.BeginPath();
        painter.Arc(center + new Vector2(0f, 11f), OuterRadius + 25f, 0f, 360f);
        painter.ClosePath();
        painter.Fill();
    }

    private void DrawConnectorLines(Painter2D painter, Vector2 center)
    {
        if (SectorCount <= 0) return;

        for (int index = 0; index < SectorCount; index++)
        {
            Vector2 direction = DirectionFor(index, SectorCount);
            Vector2 from = center + direction * (Mathf.Max(30f, InnerRadius - 6f) + 6f);
            Vector2 to = center + direction * (OrbitRadius - NodeRadius - 4f);
            bool emphasized = index == HoveredIndex || index == FlashIndex;

            painter.strokeColor = emphasized ? AmberGlow : CompassLeather;
            painter.lineWidth = emphasized ? 3.2f : 2.2f;
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();

            painter.strokeColor = CompassEdge;
            painter.lineWidth = 0.8f;
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }
    }

    private void DrawOrbitGuide(Painter2D painter, Vector2 center)
    {
        if (SectorCount < 2) return;

        painter.strokeColor = new Color(0.08f, 0.055f, 0.035f, 0.22f);
        painter.lineWidth = 4.8f;
        painter.BeginPath();
        painter.Arc(center, OrbitRadius, 0f, 360f);
        painter.Stroke();

        painter.strokeColor = CompassEdge;
        painter.lineWidth = 1.1f;
        painter.BeginPath();
        painter.Arc(center, OrbitRadius, 0f, 360f);
        painter.Stroke();
    }

    private void DrawAmbientSweep(Painter2D painter, Vector2 center)
    {
        if (SectorCount <= 0) return;

        float time = Time.realtimeSinceStartup;
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * AmbientPulseSpeed);
        float sweepRadius = Mathf.Lerp(Mathf.Max(30f, InnerRadius - 6f) + 14f, OrbitRadius - 8f, 0.55f);
        float arcLength = 42f;
        Color sweepColor = CenterHovered
            ? new Color(1.00f, 0.80f, 0.28f, 0.50f)
            : new Color(1.00f, 0.58f, 0.14f, 0.36f);
        sweepColor.a += pulse * 0.12f;

        painter.strokeColor = SweepShadow;
        painter.lineWidth = 4.6f + pulse * 0.9f;
        for (int sweepIndex = 0; sweepIndex < 3; sweepIndex++)
        {
            float startAngle = time * AmbientSweepSpeed + sweepIndex * 120f;
            painter.BeginPath();
            painter.Arc(center, sweepRadius, startAngle, startAngle + arcLength, ArcDirection.Clockwise);
            painter.Stroke();
        }

        painter.strokeColor = sweepColor;
        painter.lineWidth = 2.4f + pulse * 0.8f;

        for (int sweepIndex = 0; sweepIndex < 3; sweepIndex++)
        {
            float startAngle = time * AmbientSweepSpeed + sweepIndex * 120f;
            painter.BeginPath();
            painter.Arc(center, sweepRadius, startAngle, startAngle + arcLength, ArcDirection.Clockwise);
            painter.Stroke();
        }
    }

    private static Vector2 DirectionFor(int index, int sectorCount)
    {
        float angle = CommandAngleFor(index, sectorCount) * Mathf.Deg2Rad;
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

        if (sectorCount == 4)
            return -135f + index * 90f;

        return -90f + index * (360f / sectorCount);
    }
}