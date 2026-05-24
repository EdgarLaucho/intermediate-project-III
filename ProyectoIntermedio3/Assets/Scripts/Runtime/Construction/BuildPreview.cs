using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class BuildPreview : MonoBehaviour
{
    #region Inspector Fields

    [Header("Materials")]
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    [Header("Feel")]
    [SerializeField] private float ghostBobHeight = 0.045f;
    [SerializeField] private float ghostBobSpeed = 3.2f;
    [SerializeField] private float ghostBreathAmount = 0.025f;
    [SerializeField] private float ghostTiltDegrees = 1.4f;
    [SerializeField] private float invalidTiltDegrees = 3.6f;

    #endregion

    #region Constants

    private const float SnapDuration = 0.085f;

    #endregion

    #region Nested Types

    private class GhostRendererState
    {
        public Renderer Renderer;
        public int MaterialCount;
    }

    #endregion

    #region Runtime State

    private BuildingData _data;
    private GameObject _ghost;
    private Tower _previewTower;
    private Vector2Int _lastCell;
    private bool _hasLastCell;
    private Vector3 _displayPosition;
    private Vector3 _snapFrom;
    private Vector3 _snapTo;
    private float _snapT = SnapDuration;
    private Vector3 _ghostBaseScale = Vector3.one;
    private Quaternion _ghostBaseRotation = Quaternion.identity;
    private Quaternion _ghostPrefabRotation = Quaternion.identity;
    private BuildManager.PlacementState _lastPlacementState = BuildManager.PlacementState.Blocked;

    private readonly List<GhostRendererState> _ghostRenderers = new();
    private Material _validGhostMaterial;
    private Material _invalidGhostMaterial;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        EnsureRangeIndicatorExists();
        EnsurePaintPreviewExists();
        EnsureGhostMaterials();

        if (validMaterial == null) Debug.LogError("[BuildPreview] 'validMaterial' not assigned.");
        if (invalidMaterial == null) Debug.LogError("[BuildPreview] 'invalidMaterial' not assigned.");
    }

    private void EnsureRangeIndicatorExists()
    {
        if (FindAnyObjectByType<TowerRangeIndicator>() != null)
            return;

        gameObject.AddComponent<TowerRangeIndicator>();
    }

    private void EnsurePaintPreviewExists()
    {
        if (FindAnyObjectByType<PaintPlacementPreviewRenderer>() != null)
            return;

        gameObject.AddComponent<PaintPlacementPreviewRenderer>();
    }

    private void OnEnable()
    {
        ConstructionEvents.OnPlacementStarted += Show;
        ConstructionEvents.OnPlacementUpdated += OnPlacementUpdated;
        ConstructionEvents.OnPlacementEnded += Hide;
    }

    private void OnDisable()
    {
        ConstructionEvents.OnPlacementStarted -= Show;
        ConstructionEvents.OnPlacementUpdated -= OnPlacementUpdated;
        ConstructionEvents.OnPlacementEnded -= Hide;
    }

    private void OnPlacementUpdated(PlacementUpdatedArgs args)
    {
        UpdatePreview(args.Coords, args.WorldPos, args.Validation);
    }

    private void OnDestroy()
    {
        DestroyGhost();
        DestroyGhostMaterials();
    }

    #endregion

    #region Public API

    public void Show(BuildingData data)
    {
        Hide();
        _data = data;

        if (data?.prefab == null) return;

        _ghost = Instantiate(data.prefab);
        _ghost.name = "__BuildPreview_Ghost_" + data.prefab.name;
        _ghostBaseScale = _ghost.transform.localScale;
        _ghostPrefabRotation = _ghost.transform.rotation;
        _ghostBaseRotation = _ghostPrefabRotation;
        DisableColliders(_ghost);
        DisableGhostGameplay(_ghost);
        CacheGhostRenderers(_ghost);
        _previewTower = null;
        _ghost.TryGetComponent<Tower>(out _previewTower);

        _previewTower?.Initialize(data);
        DisableGhostLevelIndicators(_ghost);


        _hasLastCell = false;
        _snapT = SnapDuration;
    }

    public void Hide()
    {
        DestroyGhost();
        ConstructionEvents.TowerFocused(null);
        _data = null;
        _previewTower = null;
        _ghostRenderers.Clear();
        _hasLastCell = false;
    }

    public void UpdatePreview(Vector2Int coords, Vector3 snappedWorldPosition, BuildManager.PlacementValidation validation)
    {
        if (_data == null) return;

        bool changedCell = !_hasLastCell || _lastCell != coords;
        bool changedState = validation.State != _lastPlacementState;

        if (changedCell)
        {
            _snapFrom = _hasLastCell ? _displayPosition : snappedWorldPosition;
            _snapTo = snappedWorldPosition;
            _snapT = 0f;
            _lastCell = coords;
            _hasLastCell = true;

            GameObject neededPrefab = BuildingRotationHelper.ResolvePrefab(coords, _data);
            if (_ghost == null || _ghost.name != "__BuildPreview_Ghost_" + neededPrefab.name)
                RebuildGhost(neededPrefab);

            _ghostBaseRotation = BuildingRotationHelper.ComputePlacementRotationForCell(coords, _data) * _ghostPrefabRotation;
        }

        if (changedCell || changedState)
        {
            _lastPlacementState = validation.State;
            ApplyGhostVisual(validation);
        }

        _snapT = Mathf.Min(SnapDuration, _snapT + Time.deltaTime);
        float snapEase = EaseOutCubic(SnapDuration <= 0f ? 1f : _snapT / SnapDuration);
        _displayPosition = Vector3.Lerp(_snapFrom, _snapTo, snapEase);

        Vector3 finalPosition = _displayPosition;
        float time = Time.unscaledTime;
        float bob = Mathf.Sin(time * ghostBobSpeed) * ghostBobHeight;
        float breath = 1f + Mathf.Sin(time * ghostBobSpeed * 0.72f) * ghostBreathAmount;
        float tiltAmount = validation.IsValid ? ghostTiltDegrees : invalidTiltDegrees;
        float tiltX = Mathf.Sin(time * ghostBobSpeed * 1.17f) * tiltAmount;
        float tiltZ = Mathf.Cos(time * ghostBobSpeed * 0.93f) * tiltAmount;

        if (_ghost != null)
        {
            _ghost.transform.position = finalPosition + Vector3.up * (validation.IsValid ? bob : bob * 0.35f);
            _ghost.transform.rotation = _ghostBaseRotation * Quaternion.Euler(tiltX, 0f, tiltZ);
            _ghost.transform.localScale = _ghostBaseScale * breath;
        }



        if (_previewTower != null)
            ConstructionEvents.TowerFocused(_previewTower);
        else
            ConstructionEvents.TowerFocused(null);
    }

    #endregion

    #region Ghost Setup Helpers

    private static void DisableColliders(GameObject go)
    {
        foreach (var col in go.GetComponentsInChildren<Collider>())
            col.enabled = false;

        foreach (var obstacle in go.GetComponentsInChildren<NavMeshObstacle>())
            obstacle.enabled = false;
    }

    private static void DisableGhostGameplay(GameObject go)
    {
        foreach (var shooter in go.GetComponentsInChildren<TowerShooter>(true))
            shooter.enabled = false;

        foreach (var catapultAnimator in go.GetComponentsInChildren<CannonCatapultAnimator>(true))
            catapultAnimator.enabled = false;

        foreach (var trigger in go.GetComponentsInChildren<TrapTrigger>(true))
            trigger.enabled = false;

        foreach (var healthBar in go.GetComponentsInChildren<BuildingHealthBar>(true))
            healthBar.enabled = false;

        foreach (var uiDocument in go.GetComponentsInChildren<UIDocument>(true))
            uiDocument.enabled = false;
    }

    private static void DisableGhostLevelIndicators(GameObject go)
    {
        foreach (var indicator in go.GetComponentsInChildren<BuildingLevelIndicator>(true))
            indicator.enabled = false;
    }

    private void CacheGhostRenderers(GameObject go)
    {
        _ghostRenderers.Clear();

        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            int materialCount = renderer.sharedMaterials.Length;
            if (materialCount == 0)
                continue;

            _ghostRenderers.Add(new GhostRendererState
            {
                Renderer = renderer,
                MaterialCount = materialCount
            });
        }
    }

    #endregion

    #region Visual Tinting

    private void ApplyGhostVisual(BuildManager.PlacementValidation validation)
    {
        Material material = validation.IsValid ? _validGhostMaterial : _invalidGhostMaterial;
        SetMaterialColor(material, GhostTintFor(validation.State));
        ConfigureSurface(material, validation.IsValid);

        foreach (var state in _ghostRenderers)
        {
            if (state.Renderer == null) continue;
            Material[] mats = new Material[state.MaterialCount];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = material;
            state.Renderer.sharedMaterials = mats;
        }
    }

    private Color GhostTintFor(BuildManager.PlacementState state)
    {
        if (state == BuildManager.PlacementState.Valid)
            return new Color(0.72f, 0.72f, 0.72f, 1f);

        if (state == BuildManager.PlacementState.InsufficientGold)
            return new Color(1.00f, 0.78f, 0.16f, 0.65f);

        if (state == BuildManager.PlacementState.InvalidPhase)
            return new Color(0.55f, 0.58f, 0.66f, 0.55f);

        return ReadMaterialColor(invalidMaterial, new Color(1f, 0.16f, 0.12f, 0.65f));
    }

    #endregion

    #region Material Utilities

    private void EnsureGhostMaterials()
    {
        if (_validGhostMaterial == null)
            _validGhostMaterial = CreateGhostMaterial("BuildPreview_Valid", new Color(0.72f, 0.72f, 0.72f, 1f), true);

        if (_invalidGhostMaterial == null)
            _invalidGhostMaterial = CreateGhostMaterial("BuildPreview_Invalid", new Color(1f, 0.16f, 0.12f, 0.68f), false);
    }

    private static Material CreateGhostMaterial(string name, Color color, bool opaque)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Standard");

        Material mat = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
        mat.name = name;
        SetMaterialColor(mat, color);
        ConfigureSurface(mat, opaque);
        return mat;
    }

    private static void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    }

    private void DestroyGhostMaterials()
    {
        DestroyRuntimeMaterial(_validGhostMaterial);
        DestroyRuntimeMaterial(_invalidGhostMaterial);
        _validGhostMaterial = null;
        _invalidGhostMaterial = null;
    }

    private static void DestroyRuntimeMaterial(Material material)
    {
        if (material == null) return;
        if (Application.isPlaying)
            Destroy(material);
        else
            DestroyImmediate(material);
    }

    private static void ConfigureSurface(Material mat, bool opaque)
    {
        if (mat == null) return;

        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", opaque ? 0f : 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", opaque ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", opaque ? (float)BlendMode.Zero : (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", opaque ? 1f : 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
        if (opaque)
        {
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = (int)RenderQueue.Geometry;
            return;
        }

        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    private static Color ReadMaterialColor(Material mat, Color defaultColor)
    {
        if (mat == null) return defaultColor;
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
        return defaultColor;
    }

    #endregion

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private void DestroyGhost()
    {
        if (_ghost != null) { Destroy(_ghost); _ghost = null; }
    }

    private void RebuildGhost(GameObject prefab)
    {
        DestroyGhost();
        _ghost = Instantiate(prefab);
        _ghost.name = "__BuildPreview_Ghost_" + prefab.name;
        _ghostBaseScale = _ghost.transform.localScale;
        _ghostPrefabRotation = _ghost.transform.rotation;
        _ghostBaseRotation = _ghostPrefabRotation;
        DisableColliders(_ghost);
        DisableGhostGameplay(_ghost);
        CacheGhostRenderers(_ghost);
        _previewTower = null;
        _ghost.TryGetComponent<Tower>(out _previewTower);
        _previewTower?.Initialize(_data);
        DisableGhostLevelIndicators(_ghost);
        ApplyGhostVisual(new BuildManager.PlacementValidation(
            _lastPlacementState, null, string.Empty));
    }
}
