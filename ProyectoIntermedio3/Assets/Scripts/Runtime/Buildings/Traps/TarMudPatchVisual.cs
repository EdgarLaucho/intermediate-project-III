using System.Collections.Generic;
using UnityEngine;

public sealed class TarMudPatchVisual : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float yOffset = 0.012f;
    [SerializeField] private float fadeInDuration = 0.12f;
    [SerializeField] private float fadeOutDuration = 0.45f;

    private readonly List<MaterialState> _materials = new();
    private readonly List<Material> _ownedMaterials = new();

    private float _createdAt;
    private float _duration;
    private bool _initialized;

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

    public void Initialize(int cellRadius, float duration)
    {
        var cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1f;
        var worldSize = Mathf.Max(cellSize, (cellRadius * 2 + 1) * cellSize);

        EnsureVisualRoot();
        visualRoot.localPosition = Vector3.up * yOffset;
        visualRoot.localScale = new Vector3(worldSize, 1f, worldSize);

        _duration = Mathf.Max(0.1f, duration);
        _createdAt = Time.time;
        _initialized = true;

        CacheMaterials();
        UpdateAlpha(0f);
    }

    private void Update()
    {
        if (!_initialized || GamePauseEvents.IsPaused) return;

        var age = Time.time - _createdAt;
        var fadeIn = fadeInDuration <= 0f ? 1f : Mathf.Clamp01(age / fadeInDuration);
        var remaining = _duration - age;
        var fadeOut = fadeOutDuration <= 0f ? 1f : Mathf.Clamp01(remaining / fadeOutDuration);
        UpdateAlpha(Mathf.Min(fadeIn, fadeOut));
    }

    private void OnDestroy()
    {
        for (var i = 0; i < _ownedMaterials.Count; i++)
            if (_ownedMaterials[i] != null) Destroy(_ownedMaterials[i]);
    }

    private void EnsureVisualRoot()
    {
        if (visualRoot == null)
            visualRoot = transform;
    }

    private void CacheMaterials()
    {
        _materials.Clear();
        _ownedMaterials.Clear();

        var renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            var renderer = renderers[rendererIndex];
            var sharedMaterials = renderer.sharedMaterials;
            var instanceMaterials = new Material[sharedMaterials.Length];

            for (var materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
            {
                var material = sharedMaterials[materialIndex];
                if (material == null) continue;

                var instance = new Material(material);
                instanceMaterials[materialIndex] = instance;
                _ownedMaterials.Add(instance);
                _materials.Add(new MaterialState(instance, ReadColor(instance)));
            }

            renderer.materials = instanceMaterials;
        }
    }

    private void UpdateAlpha(float alphaMultiplier)
    {
        var alpha = Mathf.Clamp01(alphaMultiplier);
        for (var i = 0; i < _materials.Count; i++)
        {
            var state = _materials[i];
            var color = state.BaseColor;
            color.a *= alpha;
            SetColor(state.Material, color);
        }
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
}