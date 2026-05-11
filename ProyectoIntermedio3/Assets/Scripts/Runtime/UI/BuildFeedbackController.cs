using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Handles visual feedback for construction events: spawn pop animations, upgrade surges,
// and demolish burst effects. All geometry is drawn at runtime via Graphics.DrawMesh.
[DisallowMultipleComponent]
public sealed class BuildFeedbackController : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private GridManager grid;

    [Header("Spawn Pop")]
    [SerializeField] private float popDuration = 0.24f;
    [SerializeField] private float popStartScale = 0.84f;
    [SerializeField] private float popOvershootScale = 1.08f;
    [SerializeField] private float popLiftHeight = 0.08f;
    [SerializeField] private float popWobbleDegrees = 4.5f;

    [Header("Cell Flash")]
    [SerializeField] private float cellFlashDuration = 0.34f;
    [SerializeField] private float upgradeFlashDuration = 0.42f;
    [SerializeField] private float demolishFlashDuration = 0.46f;
    [SerializeField] private float yOffset = 0.085f;
    [SerializeField] private Color fillColor = new(1.00f, 0.78f, 0.18f, 0.34f);
    [SerializeField] private Color ringColor = new(1.00f, 0.92f, 0.40f, 0.88f);

    [Header("Upgrade Surge")]
    [SerializeField] private Color upgradeFillColor = new(0.26f, 1.00f, 0.56f, 0.30f);
    [SerializeField] private Color upgradeRingColor = new(0.74f, 1.00f, 0.36f, 0.92f);
    [SerializeField] private float upgradePulseScale = 1.13f;
    [SerializeField] private float upgradeLiftHeight = 0.06f;

    [Header("Demolish Burst")]
    [SerializeField] private Color demolishFillColor = new(1.00f, 0.24f, 0.12f, 0.30f);
    [SerializeField] private Color demolishRingColor = new(1.00f, 0.46f, 0.20f, 0.92f);
    [SerializeField] private Color demolishShardColor = new(1.00f, 0.76f, 0.34f, 0.84f);

    #endregion

    #region Runtime State

    private readonly List<CellFlash> _cellFlashes = new();
    private Mesh _fillMesh;
    private Mesh _ringMesh;
    private Mesh _burstMesh;
    private Material _fillMaterial;
    private Material _ringMaterial;
    private Material _burstMaterial;
    private bool _subscribed;

    #endregion

    #region Inner Types

    private enum FeedbackKind
    {
        Build,
        Upgrade,
        Demolish,
    }

    private struct CellFlash
    {
        public Vector3 Center;
        public float CellSize;
        public float Age;
        public float Duration;
        public FeedbackKind Kind;
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _fillMesh = BuildFillMesh();
        _ringMesh = BuildRingMesh();
        _burstMesh = BuildBurstMesh();
        _fillMaterial = BuildMaterial(fillColor, "BuildFeedbackFill");
        _ringMaterial = BuildMaterial(ringColor, "BuildFeedbackRing");
        _burstMaterial = BuildMaterial(demolishShardColor, "BuildFeedbackBurst");
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (_fillMesh != null) Destroy(_fillMesh);
        if (_ringMesh != null) Destroy(_ringMesh);
        if (_burstMesh != null) Destroy(_burstMesh);
        if (_fillMaterial != null) Destroy(_fillMaterial);
        if (_ringMaterial != null) Destroy(_ringMaterial);
        if (_burstMaterial != null) Destroy(_burstMaterial);
    }

    private void Update()
    {
        // Iterate backwards so RemoveAt doesn't invalidate the remaining indices
        for (int flashIndex = _cellFlashes.Count - 1; flashIndex >= 0; flashIndex--)
        {
            CellFlash flash = _cellFlashes[flashIndex];
            flash.Age += Time.deltaTime;

            if (flash.Age >= flash.Duration)
            {
                _cellFlashes.RemoveAt(flashIndex);
                continue;
            }

            DrawCellFlash(flash);
            _cellFlashes[flashIndex] = flash;
        }
    }

    #endregion

    #region Event Subscription

    private void Subscribe()
    {
        if (_subscribed) return;
        ConstructionEvents.OnBuildingPlaced     += HandleBuildingPlaced;
        ConstructionEvents.OnBuildingUpgraded   += HandleBuildingUpgraded;
        ConstructionEvents.OnBuildingDemolished += HandleBuildingDemolished;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        ConstructionEvents.OnBuildingPlaced     -= HandleBuildingPlaced;
        ConstructionEvents.OnBuildingUpgraded   -= HandleBuildingUpgraded;
        ConstructionEvents.OnBuildingDemolished -= HandleBuildingDemolished;
        _subscribed = false;
    }

    #endregion

    #region Event Handlers

    private void HandleBuildingPlaced(BuildingActionArgs args)
    {
        if (args.Building != null)
            StartCoroutine(PlaySpawnPop(args.Building.transform));

        AddCellFlash(args.Coords, FeedbackKind.Build, cellFlashDuration);
    }

    private void HandleBuildingUpgraded(BuildingActionArgs args)
    {
        if (args.Building != null)
            StartCoroutine(PlayUpgradePulse(args.Building.transform));

        AddCellFlash(args.Coords, FeedbackKind.Upgrade, upgradeFlashDuration);
    }

    private void HandleBuildingDemolished(Vector2Int coords)
    {
        AddCellFlash(coords, FeedbackKind.Demolish, demolishFlashDuration);
    }

    private void AddCellFlash(Vector2Int coords, FeedbackKind kind, float duration)
    {
        if (grid == null) return;

        _cellFlashes.Add(new CellFlash
        {
            Center   = grid.GridToWorld(coords) + Vector3.up * yOffset,
            CellSize = grid.CellSize,
            Age      = 0f,
            Duration = Mathf.Max(0.01f, duration),
            Kind     = kind,
        });
    }

    #endregion

    #region Coroutines

    private IEnumerator PlaySpawnPop(Transform target)
    {
        if (target == null) yield break;

        Vector3 baseScale = target.localScale;
        Vector3 basePosition = target.position;
        Quaternion baseRotation = target.rotation;
        float elapsed = 0f;

        while (elapsed < popDuration && target != null)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = popDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / popDuration);
            float scale = EvaluatePopScale(normalizedTime);
            float lift = Mathf.Sin(normalizedTime * Mathf.PI) * popLiftHeight;
            float wobble = Mathf.Sin(normalizedTime * Mathf.PI * 2.4f) * popWobbleDegrees * (1f - normalizedTime);
            target.localScale = baseScale * scale;
            target.position = basePosition + Vector3.up * lift;
            target.rotation = baseRotation * Quaternion.Euler(0f, wobble, 0f);
            yield return null;
        }

        if (target != null)
        {
            target.localScale = baseScale;
            target.position = basePosition;
            target.rotation = baseRotation;
        }
    }

    // Two-phase ease: rise from startScale → overshootScale in the first 58%, then settle back to 1
    private float EvaluatePopScale(float normalizedTime)
    {
        if (normalizedTime < 0.58f)
        {
            float riseProgress = EaseOutCubic(normalizedTime / 0.58f);
            return Mathf.Lerp(popStartScale, popOvershootScale, riseProgress);
        }

        float settleProgress = EaseOutCubic((normalizedTime - 0.58f) / 0.42f);
        return Mathf.Lerp(popOvershootScale, 1f, settleProgress);
    }

    // Upgrade surge: sine-arc scale + vertical lift + decaying twist rotation
    private IEnumerator PlayUpgradePulse(Transform target)
    {
        if (target == null) yield break;

        Vector3 baseScale = target.localScale;
        Vector3 basePosition = target.position;
        Quaternion baseRotation = target.rotation;
        float elapsed = 0f;

        while (elapsed < upgradeFlashDuration && target != null)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = upgradeFlashDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / upgradeFlashDuration);
            // surge = sine arch (peaks at midpoint, zero at both ends); settle = eased 0→1 for twist decay
            float surge = Mathf.Sin(normalizedTime * Mathf.PI);
            float settle = EaseOutCubic(normalizedTime);
            float scale = 1f + surge * (upgradePulseScale - 1f);
            float lift = surge * upgradeLiftHeight;
            float twist = Mathf.Sin(normalizedTime * Mathf.PI * 2f) * 5.5f * (1f - settle);

            target.localScale = baseScale * scale;
            target.position = basePosition + Vector3.up * lift;
            target.rotation = baseRotation * Quaternion.Euler(0f, twist, 0f);
            yield return null;
        }

        if (target != null)
        {
            target.localScale = baseScale;
            target.position = basePosition;
            target.rotation = baseRotation;
        }
    }

    #endregion

    #region Cell Flash Drawing

    private void DrawCellFlash(CellFlash flash)
    {
        float normalizedTime = Mathf.Clamp01(flash.Age / flash.Duration);
        switch (flash.Kind)
        {
            case FeedbackKind.Upgrade:
                DrawUpgradeFlash(flash, normalizedTime);
                break;
            case FeedbackKind.Demolish:
                DrawDemolishFlash(flash, normalizedTime);
                break;
            default:
                DrawBuildFlash(flash, normalizedTime);
                break;
        }
    }

    private void DrawBuildFlash(CellFlash flash, float normalizedTime)
    {
        float fade = 1f - normalizedTime;
        float ease = EaseOutCubic(normalizedTime);

        if (_fillMesh != null && _fillMaterial != null)
        {
            Color color = fillColor;
            color.a *= fade * fade;
            SetMaterialColor(_fillMaterial, color);

            float scale = flash.CellSize * Mathf.Lerp(0.62f, 1.04f, ease);
            Matrix4x4 matrix = Matrix4x4.TRS(flash.Center, Quaternion.identity, new Vector3(scale, 1f, scale));
            Graphics.DrawMesh(_fillMesh, matrix, _fillMaterial, 0);
        }

        if (_ringMesh != null && _ringMaterial != null)
        {
            Color color = ringColor;
            color.a *= fade;
            SetMaterialColor(_ringMaterial, color);

            float scale = flash.CellSize * Mathf.Lerp(0.72f, 1.28f, ease);
            float spin = normalizedTime * 96f;
            Matrix4x4 matrix = Matrix4x4.TRS(flash.Center + Vector3.up * 0.006f, Quaternion.Euler(0f, spin, 0f), new Vector3(scale, 1f, scale));
            Graphics.DrawMesh(_ringMesh, matrix, _ringMaterial, 0);

            // Echo ring: larger, counter-rotating, more transparent — adds visual depth to the flash
            Color echoColor = ringColor;
            echoColor.a *= fade * 0.42f;
            SetMaterialColor(_ringMaterial, echoColor);
            float echoScale = flash.CellSize * Mathf.Lerp(0.54f, 1.46f, ease);
            Matrix4x4 echoMatrix = Matrix4x4.TRS(flash.Center + Vector3.up * 0.011f, Quaternion.Euler(0f, -spin * 1.4f, 0f), new Vector3(echoScale, 1f, echoScale));
            Graphics.DrawMesh(_ringMesh, echoMatrix, _ringMaterial, 0);
        }
    }

    private void DrawUpgradeFlash(CellFlash flash, float normalizedTime)
    {
        float fade = 1f - normalizedTime;
        float ease = EaseOutCubic(normalizedTime);
        float surge = Mathf.Sin(normalizedTime * Mathf.PI);

        if (_fillMesh != null && _fillMaterial != null)
        {
            Color color = upgradeFillColor;
            color.a *= fade * (0.72f + surge * 0.45f);
            SetMaterialColor(_fillMaterial, color);

            float scale = flash.CellSize * Mathf.Lerp(0.74f, 1.10f, ease);
            Matrix4x4 matrix = Matrix4x4.TRS(flash.Center, Quaternion.Euler(0f, normalizedTime * 50f, 0f), new Vector3(scale, 1f, scale));
            Graphics.DrawMesh(_fillMesh, matrix, _fillMaterial, 0);
        }

        if (_ringMesh != null && _ringMaterial != null)
        {
            Color color = upgradeRingColor;
            color.a *= fade;
            SetMaterialColor(_ringMaterial, color);

            float innerScale = flash.CellSize * Mathf.Lerp(0.48f, 1.10f, ease);
            float outerScale = flash.CellSize * Mathf.Lerp(0.80f, 1.42f, ease);
            float spin = normalizedTime * 180f;
            Graphics.DrawMesh(_ringMesh, Matrix4x4.TRS(flash.Center + Vector3.up * 0.007f, Quaternion.Euler(0f, spin, 0f), new Vector3(innerScale, 1f, innerScale)), _ringMaterial, 0);

            Color echoColor = upgradeRingColor;
            echoColor.a *= fade * 0.42f;
            SetMaterialColor(_ringMaterial, echoColor);
            Graphics.DrawMesh(_ringMesh, Matrix4x4.TRS(flash.Center + Vector3.up * 0.013f, Quaternion.Euler(0f, -spin * 0.8f, 0f), new Vector3(outerScale, 1f, outerScale)), _ringMaterial, 0);
        }

        if (_burstMesh != null && _burstMaterial != null)
        {
            Color sparks = upgradeRingColor;
            sparks.a *= fade * 0.76f;
            SetMaterialColor(_burstMaterial, sparks);
            float burstScale = flash.CellSize * Mathf.Lerp(0.42f, 1.14f, ease);
            Graphics.DrawMesh(_burstMesh, Matrix4x4.TRS(flash.Center + Vector3.up * 0.018f, Quaternion.Euler(0f, normalizedTime * -120f, 0f), new Vector3(burstScale, 1f, burstScale)), _burstMaterial, 0);
        }
    }

    private void DrawDemolishFlash(CellFlash flash, float normalizedTime)
    {
        float fade = 1f - normalizedTime;
        float ease = EaseOutCubic(normalizedTime);
        float snap = 1f - EaseOutCubic(Mathf.Clamp01(normalizedTime / 0.62f));

        if (_fillMesh != null && _fillMaterial != null)
        {
            Color color = demolishFillColor;
            color.a *= fade * fade;
            SetMaterialColor(_fillMaterial, color);

            float scale = flash.CellSize * Mathf.Lerp(1.08f, 0.34f, ease);
            Graphics.DrawMesh(_fillMesh, Matrix4x4.TRS(flash.Center, Quaternion.Euler(0f, normalizedTime * -70f, 0f), new Vector3(scale, 1f, scale)), _fillMaterial, 0);
        }

        if (_ringMesh != null && _ringMaterial != null)
        {
            Color color = demolishRingColor;
            color.a *= fade;
            SetMaterialColor(_ringMaterial, color);

            float implodeScale = flash.CellSize * Mathf.Lerp(1.34f, 0.58f, ease);
            float shockScale = flash.CellSize * Mathf.Lerp(0.50f, 1.62f, ease);
            float spin = normalizedTime * -220f;
            Graphics.DrawMesh(_ringMesh, Matrix4x4.TRS(flash.Center + Vector3.up * 0.008f, Quaternion.Euler(0f, spin, 0f), new Vector3(implodeScale, 1f, implodeScale)), _ringMaterial, 0);

            Color shockColor = demolishRingColor;
            shockColor.a *= fade * 0.40f;
            SetMaterialColor(_ringMaterial, shockColor);
            Graphics.DrawMesh(_ringMesh, Matrix4x4.TRS(flash.Center + Vector3.up * 0.014f, Quaternion.Euler(0f, -spin * 0.45f, 0f), new Vector3(shockScale, 1f, shockScale)), _ringMaterial, 0);
        }

        if (_burstMesh != null && _burstMaterial != null)
        {
            Color shards = demolishShardColor;
            shards.a *= fade * (0.55f + snap * 0.55f);
            SetMaterialColor(_burstMaterial, shards);
            float burstScale = flash.CellSize * Mathf.Lerp(0.62f, 1.56f, ease);
            Graphics.DrawMesh(_burstMesh, Matrix4x4.TRS(flash.Center + Vector3.up * 0.020f, Quaternion.Euler(0f, normalizedTime * 260f, 0f), new Vector3(burstScale, 1f, burstScale)), _burstMaterial, 0);
        }
    }

    #endregion

    #region Mesh Builders

    private static Mesh BuildFillMesh()
    {
        Mesh mesh = new() { name = "BuildFeedbackFillMesh" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f, -0.5f),
            new Vector3( 0.5f, 0f,  0.5f),
            new Vector3(-0.5f, 0f,  0.5f),
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        return mesh;
    }

    // Square ring made from four axis-aligned strips (one per side); each strip is a thin quad
    private static Mesh BuildRingMesh()
    {
        const float thickness = 0.065f;
        float inner = 0.5f - thickness;

        Vector3[] vertices = new Vector3[16];
        int[] triangles = new int[24];

        SetStrip(vertices, triangles, 0, 0,
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, -inner), new Vector3(-0.5f, 0f, -inner));
        SetStrip(vertices, triangles, 4, 6,
            new Vector3(-0.5f, 0f, inner), new Vector3(0.5f, 0f, inner),
            new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f));
        SetStrip(vertices, triangles, 8, 12,
            new Vector3(-0.5f, 0f, -inner), new Vector3(-inner, 0f, -inner),
            new Vector3(-inner, 0f, inner), new Vector3(-0.5f, 0f, inner));
        SetStrip(vertices, triangles, 12, 18,
            new Vector3(inner, 0f, -inner), new Vector3(0.5f, 0f, -inner),
            new Vector3(0.5f, 0f, inner), new Vector3(inner, 0f, inner));

        Mesh mesh = new() { name = "BuildFeedbackRingMesh" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh BuildBurstMesh()
    {
        const int spokes = 12;
        const float innerRadius = 0.12f;
        const float outerRadius = 0.50f;
        const float halfWidth = 0.014f;

        var vertices = new List<Vector3>(spokes * 4);
        var triangles = new List<int>(spokes * 6);

        for (int spokeIndex = 0; spokeIndex < spokes; spokeIndex++)
        {
            float angle = spokeIndex / (float)spokes * Mathf.PI * 2f;
            Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 tangent = new(-direction.z, 0f, direction.x);
            // Stagger every 3rd spoke's length (0.86 / 0.94 / 1.02) for a spiky, asymmetric silhouette
            float lengthJitter = 0.86f + (spokeIndex % 3) * 0.08f;
            AddRadialStrip(vertices, triangles, direction, tangent, innerRadius, outerRadius * lengthJitter, halfWidth);
        }

        Mesh mesh = new() { name = "BuildFeedbackBurstMesh" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    // Appends a thin quad strip along 'direction', spanning innerRadius→outerRadius, ±halfWidth wide
    private static void AddRadialStrip(List<Vector3> vertices, List<int> triangles, Vector3 direction, Vector3 tangent, float innerRadius, float outerRadius, float halfWidth)
    {
        int baseIndex = vertices.Count;
        vertices.Add(direction * innerRadius - tangent * halfWidth);
        vertices.Add(direction * outerRadius - tangent * halfWidth);
        vertices.Add(direction * outerRadius + tangent * halfWidth);
        vertices.Add(direction * innerRadius + tangent * halfWidth);
        triangles.Add(baseIndex); triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex); triangles.Add(baseIndex + 3); triangles.Add(baseIndex + 2);
    }

    // Writes a CCW quad (two tris) into shared arrays at the given index offsets
    private static void SetStrip(Vector3[] vertices, int[] triangles, int vertexBase, int triangleBase, Vector3 outerStart, Vector3 outerEnd, Vector3 innerEnd, Vector3 innerStart)
    {
        vertices[vertexBase] = outerStart;
        vertices[vertexBase + 1] = outerEnd;
        vertices[vertexBase + 2] = innerEnd;
        vertices[vertexBase + 3] = innerStart;

        triangles[triangleBase] = vertexBase;
        triangles[triangleBase + 1] = vertexBase + 2;
        triangles[triangleBase + 2] = vertexBase + 1;
        triangles[triangleBase + 3] = vertexBase;
        triangles[triangleBase + 4] = vertexBase + 3;
        triangles[triangleBase + 5] = vertexBase + 2;
    }

    #endregion

    #region Material Helpers

    private static Material BuildMaterial(Color initialColor, string materialName)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default");

        Material material = new(shader) { name = materialName };
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        SetMaterialColor(material, initialColor);
        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private static float EaseOutCubic(float normalizedTime)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        return 1f - Mathf.Pow(1f - normalizedTime, 3f);
    }

    #endregion
}