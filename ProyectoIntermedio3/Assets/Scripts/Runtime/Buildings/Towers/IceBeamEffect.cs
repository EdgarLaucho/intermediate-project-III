using UnityEngine;

public sealed class IceBeamEffect : MonoBehaviour
{
    [SerializeField] private float duration = 0.18f;
    [SerializeField] private float startWidth = 0.12f;
    [SerializeField] private float endWidth = 0.035f;
    [SerializeField] private Color coreColor = new Color(0.76f, 0.96f, 1f, 0.95f);
    [SerializeField] private Color glowColor = new Color(0.22f, 0.72f, 1f, 0.36f);

    private LineRenderer coreLine;
    private LineRenderer glowLine;
    private float timer;
    private float randomSeed;
    private Vector3 startPosition;
    private Vector3 endPosition;
    private Vector3 sideOffset;
    private Vector3 upOffset;
    private Material coreMaterial;
    private Material glowMaterial;

    public static void Play(Vector3 start, Vector3 end)
    {
        var beamObject = new GameObject("__IceBeamEffect");
        var effect = beamObject.AddComponent<IceBeamEffect>();

        effect.Initialize(start, end);
    }

    private void Initialize(Vector3 start, Vector3 end)
    {
        startPosition = start;
        endPosition = end;
        randomSeed = Random.Range(0f, 1000f);

        var direction = endPosition - startPosition;
        var flatDirection = new Vector3(direction.x, 0f, direction.z);
        var side = flatDirection.sqrMagnitude > 0.0001f
            ? Vector3.Cross(Vector3.up, flatDirection.normalized)
            : Vector3.right;

        sideOffset = side * 0.035f;
        upOffset = Vector3.up * 0.05f;

        coreLine = CreateLine("Core", startWidth, endWidth, coreColor, 1);
        glowLine = CreateLine("Glow", startWidth * 2.6f, endWidth * 2f, glowColor, 0);
        UpdateLines(1f);
    }

    private void Update()
    {
        if (GamePauseEvents.IsPaused) return;

        timer += Time.deltaTime;
        var t = Mathf.Clamp01(timer / Mathf.Max(0.01f, duration));
        var alpha = 1f - Smooth01(t);
        UpdateLines(alpha);

        if (timer >= duration)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (coreMaterial != null) Destroy(coreMaterial);
        if (glowMaterial != null) Destroy(glowMaterial);
    }

    private LineRenderer CreateLine(string lineName, float widthStart, float widthEnd, Color color, int sortingOrder)
    {
        var lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);

        var line = lineObject.AddComponent<LineRenderer>();
        line.positionCount = 5;
        line.useWorldSpace = true;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.startWidth = widthStart;
        line.endWidth = widthEnd;
        line.numCapVertices = 4;
        line.numCornerVertices = 3;
        line.sortingOrder = sortingOrder;
        line.material = CreateMaterial(color, lineName == "Core");
        line.startColor = color;
        line.endColor = color;
        return line;
    }

    private Material CreateMaterial(Color color, bool isCore)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        var material = new Material(shader)
        {
            name = isCore ? "IceBeamCore" : "IceBeamGlow",
            hideFlags = HideFlags.HideAndDontSave
        };

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);

        if (isCore) coreMaterial = material;
        else glowMaterial = material;
        return material;
    }

    private void UpdateLines(float alpha)
    {
        var pulse = Mathf.Sin((Time.time + randomSeed) * 45f) * 0.5f + 0.5f;
        var jitter = sideOffset * (pulse - 0.5f);
        var points = new[]
        {
            startPosition,
            Vector3.Lerp(startPosition, endPosition, 0.28f) + upOffset + jitter,
            Vector3.Lerp(startPosition, endPosition, 0.52f) - jitter,
            Vector3.Lerp(startPosition, endPosition, 0.76f) + upOffset * 0.4f + jitter,
            endPosition
        };

        ApplyLine(coreLine, points, coreColor, alpha);
        ApplyLine(glowLine, points, glowColor, alpha);
    }

    private static void ApplyLine(LineRenderer line, Vector3[] points, Color color, float alpha)
    {
        if (line == null) return;

        line.SetPositions(points);
        var faded = new Color(color.r, color.g, color.b, color.a * alpha);
        line.startColor = faded;
        line.endColor = new Color(faded.r, faded.g, faded.b, 0f);
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}