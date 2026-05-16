using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


// Procedural tar/mud decal used when TarTrap has no authored VFX prefab.
// It builds a cluster of flat irregular blobs, wet highlights and small bubbles,
// all from generated meshes and transparent materials: no textures required.
public sealed class TarMudPatchVisual : MonoBehaviour
{
    #region Inspector Fields

    // Ground is at the snapped cell position and GridRenderer sits at +0.02.
    // This keeps the mud between both layers: above the floor, below the grid.
    [SerializeField] private float yOffset = 0.012f;
    [SerializeField] private float fadeInDuration = 0.12f;
    [SerializeField] private float fadeOutDuration = 0.45f;

    #endregion

    #region Runtime State

    private readonly List<Mesh> _meshes = new();
    private readonly List<MaterialState> _materials = new();

    private float _createdAt;
    private float _duration;
    private float _cellSize;
    private int _cellRadius;
    private bool _initialized;

    #endregion

    #region Nested Types

    private readonly struct MaterialState
    {
        public readonly Material Material;
        public readonly Color BaseColor;

        public MaterialState(Material material, Color baseColor)
        {
            Material = material;
            BaseColor = baseColor;
        }
    }

    #endregion

    #region Public API

    public void Initialize(int cellRadius, float duration)
    {
        _cellRadius = Mathf.Max(0, cellRadius);
        _duration = Mathf.Max(0.1f, duration);
        _cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        _createdAt = Time.time;
        _initialized = true;

        BuildVisual();
        UpdateAlpha(0f);
    }

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (!_initialized) return;

        float age = Time.time - _createdAt;
        float fadeIn = fadeInDuration <= 0f ? 1f : Mathf.Clamp01(age / fadeInDuration);
        float remaining = _duration - age;
        float fadeOut = fadeOutDuration <= 0f ? 1f : Mathf.Clamp01(remaining / fadeOutDuration);
        UpdateAlpha(Mathf.Min(fadeIn, fadeOut));
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _meshes.Count; i++)
            if (_meshes[i] != null) Destroy(_meshes[i]);

        for (int i = 0; i < _materials.Count; i++)
            if (_materials[i].Material != null) Destroy(_materials[i].Material);
    }

    #endregion

    #region Visual Generation

    private void BuildVisual()
    {
        int seed = BuildSeed(transform.position, _cellRadius);
        Random.State previousRandomState = Random.state;
        Random.InitState(seed);

        float totalSize = (_cellRadius * 2 + 1) * _cellSize;
        float mainRadius = totalSize * Random.Range(0.36f, 0.43f);

        Material mudMaterial = BuildMaterial("TarMud_Mass", new Color(0.085f, 0.055f, 0.035f, 0.78f));
        Material rimMaterial = BuildMaterial("TarMud_Edge", new Color(0.030f, 0.020f, 0.015f, 0.62f));
        Material wetMaterial = BuildMaterial("TarMud_WetSheen", new Color(0.18f, 0.14f, 0.095f, 0.26f));
        Material bubbleMaterial = BuildMaterial("TarMud_Bubbles", new Color(0.27f, 0.19f, 0.12f, 0.40f));

        _materials.Add(new MaterialState(mudMaterial, ReadColor(mudMaterial)));
        _materials.Add(new MaterialState(rimMaterial, ReadColor(rimMaterial)));
        _materials.Add(new MaterialState(wetMaterial, ReadColor(wetMaterial)));
        _materials.Add(new MaterialState(bubbleMaterial, ReadColor(bubbleMaterial)));

        AddBlob("MainMud", Vector3.zero, mainRadius * 1.10f, mainRadius * 0.92f, 44, 0.28f, mudMaterial, 0f);
        AddBlob("OuterRim", Vector3.up * 0.001f, mainRadius * 1.17f, mainRadius * 0.98f, 44, 0.32f, rimMaterial, 0f);

        int satelliteCount = Mathf.Clamp(4 + _cellRadius * 3, 4, 11);
        for (int i = 0; i < satelliteCount; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float distance = Random.Range(mainRadius * 0.25f, mainRadius * 0.98f);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0.002f + i * 0.0002f, Mathf.Sin(angle) * distance);
            float rx = mainRadius * Random.Range(0.18f, 0.38f);
            float rz = mainRadius * Random.Range(0.12f, 0.31f);
            float rotation = Random.Range(0f, 360f);
            AddBlob($"MudLobe_{i}", offset, rx, rz, 24, 0.35f, mudMaterial, rotation);
        }

        int sheenCount = Mathf.Clamp(3 + _cellRadius, 3, 7);
        for (int i = 0; i < sheenCount; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float distance = Random.Range(0f, mainRadius * 0.55f);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0.006f + i * 0.0002f, Mathf.Sin(angle) * distance);
            AddBlob($"WetSheen_{i}", offset, mainRadius * Random.Range(0.08f, 0.18f), mainRadius * Random.Range(0.025f, 0.070f), 18, 0.20f, wetMaterial, Random.Range(0f, 360f));
        }

        int bubbleCount = Mathf.Clamp(5 + _cellRadius * 4, 5, 17);
        for (int i = 0; i < bubbleCount; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float distance = Random.Range(0f, mainRadius * 0.78f);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0.008f + i * 0.00015f, Mathf.Sin(angle) * distance);
            float radius = _cellSize * Random.Range(0.035f, 0.075f);
            AddBlob($"MudBubble_{i}", offset, radius, radius * Random.Range(0.75f, 1.25f), 14, 0.08f, bubbleMaterial, Random.Range(0f, 360f));
        }

        Random.state = previousRandomState;
    }

    private void AddBlob(string name, Vector3 localOffset, float radiusX, float radiusZ, int segments, float roughness, Material material, float rotationDegrees)
    {
        Mesh mesh = BuildBlobMesh(name, radiusX, radiusZ, segments, roughness, rotationDegrees);
        _meshes.Add(mesh);

        GameObject child = new GameObject(name);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = new Vector3(localOffset.x, yOffset + localOffset.y, localOffset.z);

        MeshFilter filter = child.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = material;
    }

    private static Mesh BuildBlobMesh(string name, float radiusX, float radiusZ, int segments, float roughness, float rotationDegrees)
    {
        segments = Mathf.Max(8, segments);
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        float rotation = rotationDegrees * Mathf.Deg2Rad;
        float noiseSeed = Random.Range(0f, 1000f);

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments;
            float angle = t * Mathf.PI * 2f;
            float n1 = Mathf.PerlinNoise(noiseSeed + Mathf.Cos(angle) * 1.7f, noiseSeed + Mathf.Sin(angle) * 1.7f);
            float n2 = Mathf.PerlinNoise(noiseSeed + 13.3f + Mathf.Cos(angle * 2.1f), noiseSeed + 7.9f + Mathf.Sin(angle * 2.1f));
            float irregular = 1f + ((n1 - 0.5f) * roughness) + ((n2 - 0.5f) * roughness * 0.45f);

            float x = Mathf.Cos(angle) * radiusX * irregular;
            float z = Mathf.Sin(angle) * radiusZ * irregular;
            float rotatedX = x * Mathf.Cos(rotation) - z * Mathf.Sin(rotation);
            float rotatedZ = x * Mathf.Sin(rotation) + z * Mathf.Cos(rotation);
            vertices[i + 1] = new Vector3(rotatedX, 0f, rotatedZ);
        }

        for (int i = 0; i < segments; i++)
        {
            int next = i == segments - 1 ? 1 : i + 2;
            int index = i * 3;
            triangles[index] = 0;
            triangles[index + 1] = next;
            triangles[index + 2] = i + 1;
        }

        Mesh mesh = new Mesh { name = name };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    #endregion

    #region Material Helpers

    private void UpdateAlpha(float alphaMultiplier)
    {
        for (int i = 0; i < _materials.Count; i++)
        {
            MaterialState state = _materials[i];
            Color color = state.BaseColor;
            color.a *= Mathf.Clamp01(alphaMultiplier);
            SetColor(state.Material, color);
        }
    }

    private static Material BuildMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default");

        Material material = new Material(shader) { name = name };
        material.SetOverrideTag("RenderType", "Transparent");
        SetFloatIfSupported(material, "_Surface", 1f);
        SetFloatIfSupported(material, "_Blend", 0f);
        SetFloatIfSupported(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfSupported(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfSupported(material, "_ZWrite", 0f);
        SetFloatIfSupported(material, "_Cull", (float)CullMode.Off);
        SetFloatIfSupported(material, "_AlphaClip", 0f);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent - 25;
        SetColor(material, color);
        return material;
    }

    private static Color ReadColor(Material material)
    {
        if (material != null && material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
        if (material != null && material.HasProperty("_Color")) return material.GetColor("_Color");
        return Color.white;
    }

    private static void SetColor(Material material, Color color)
    {
        if (material == null) return;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private static void SetFloatIfSupported(Material material, string property, float value)
    {
        if (material != null && material.HasProperty(property))
            material.SetFloat(property, value);
    }

    private static int BuildSeed(Vector3 position, int radius)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Mathf.RoundToInt(position.x * 100f);
            hash = hash * 31 + Mathf.RoundToInt(position.z * 100f);
            hash = hash * 31 + radius;
            return hash;
        }
    }

    #endregion
}