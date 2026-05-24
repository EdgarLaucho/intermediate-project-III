using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class BuildPreview : MonoBehaviour
{
    [Header("Materials")]
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    [Header("Feel")]
    [SerializeField] private float ghostBobHeight = 0.045f;
    [SerializeField] private float ghostBobSpeed = 3.2f;
    [SerializeField] private float ghostBreathAmount = 0.025f;
    [SerializeField] private float ghostTiltDegrees = 1.4f;
    [SerializeField] private float invalidTiltDegrees = 3.6f;

    private const float SnapDuration = 0.085f;
    private const string GhostPrefix = "__BuildPreview_Ghost_";

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly List<GhostRendererState> _ghostRenderers = new();
    private readonly MaterialPropertyBlock _ghostTintBlock = new();

    private BuildingData _data;
    private GameObject _currentPrefab;
    private GameObject _ghost;
    private Tower _previewTower;
    private Vector2Int _lastCell;
    private Vector3 _displayPosition;
    private Vector3 _snapFrom;
    private Vector3 _snapTo;
    private Vector3 _ghostBaseScale = Vector3.one;
    private Quaternion _ghostBaseRotation = Quaternion.identity;
    private Quaternion _ghostPrefabRotation = Quaternion.identity;
    private BuildManager.PlacementState _lastPlacementState = BuildManager.PlacementState.Blocked;
    private float _snapT = SnapDuration;
    private bool _hasLastCell;

    private void Awake()
    {
        EnsureVisualHelpersExist();

        if (validMaterial == null)
            Debug.LogError("[BuildPreview] 'validMaterial' not assigned.", this);

        if (invalidMaterial == null)
            Debug.LogError("[BuildPreview] 'invalidMaterial' not assigned.", this);
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

    private void OnDestroy()
    {
        DestroyGhost();
    }

    public void Show(BuildingData data)
    {
        Hide();
        _data = data;

        if (data?.prefab == null) return;

        CreateGhost(data.prefab);
        _hasLastCell = false;
        _snapT = SnapDuration;
    }

    public void Hide()
    {
        DestroyGhost();
        ConstructionEvents.TowerFocused(null);
        _data = null;
        _currentPrefab = null;
        _previewTower = null;
        _ghostRenderers.Clear();
        _hasLastCell = false;
    }

    public void UpdatePreview(Vector2Int coords, Vector3 snappedWorldPosition, BuildManager.PlacementValidation validation)
    {
        if (_data == null) return;

        var changedCell = !_hasLastCell || _lastCell != coords;
        var changedState = validation.State != _lastPlacementState;

        if (changedCell)
            MoveToCell(coords, snappedWorldPosition);

        if (changedCell || changedState)
        {
            _lastPlacementState = validation.State;
            ApplyGhostVisual(validation.State, validation.IsValid);
        }

        UpdateGhostTransform(validation.IsValid);
        ConstructionEvents.TowerFocused(_previewTower);
    }

    private void OnPlacementUpdated(PlacementUpdatedArgs args)
    {
        UpdatePreview(args.Coords, args.WorldPos, args.Validation);
    }

    private void MoveToCell(Vector2Int coords, Vector3 snappedWorldPosition)
    {
        _snapFrom = _hasLastCell ? _displayPosition : snappedWorldPosition;
        _snapTo = snappedWorldPosition;
        _snapT = 0f;
        _lastCell = coords;
        _hasLastCell = true;

        var neededPrefab = BuildingRotationHelper.ResolvePrefab(coords, _data);
        if (_ghost == null || _currentPrefab != neededPrefab)
            CreateGhost(neededPrefab);

        _ghostBaseRotation = BuildingRotationHelper.ComputePlacementRotationForCell(coords, _data) * _ghostPrefabRotation;
    }

    private void UpdateGhostTransform(bool isValidPlacement)
    {
        if (_ghost == null) return;

        _snapT = Mathf.Min(SnapDuration, _snapT + Time.deltaTime);
        var snapEase = EaseOutCubic(SnapDuration <= 0f ? 1f : _snapT / SnapDuration);
        _displayPosition = Vector3.Lerp(_snapFrom, _snapTo, snapEase);

        var time = Time.unscaledTime;
        var bob = Mathf.Sin(time * ghostBobSpeed) * ghostBobHeight;
        var breath = 1f + Mathf.Sin(time * ghostBobSpeed * 0.72f) * ghostBreathAmount;
        var tiltAmount = isValidPlacement ? ghostTiltDegrees : invalidTiltDegrees;
        var tiltX = Mathf.Sin(time * ghostBobSpeed * 1.17f) * tiltAmount;
        var tiltZ = Mathf.Cos(time * ghostBobSpeed * 0.93f) * tiltAmount;
        var bobScale = isValidPlacement ? 1f : 0.35f;

        _ghost.transform.position = _displayPosition + Vector3.up * (bob * bobScale);
        _ghost.transform.rotation = _ghostBaseRotation * Quaternion.Euler(tiltX, 0f, tiltZ);
        _ghost.transform.localScale = _ghostBaseScale * breath;
    }

    private void CreateGhost(GameObject prefab)
    {
        DestroyGhost();

        _currentPrefab = prefab;
        _ghost = Instantiate(prefab);
        _ghost.name = GhostPrefix + prefab.name;
        _ghostBaseScale = _ghost.transform.localScale;
        _ghostPrefabRotation = _ghost.transform.rotation;
        _ghostBaseRotation = _ghostPrefabRotation;

        DisableColliders(_ghost);
        DisableGhostGameplay(_ghost);
        DisableGhostLevelIndicators(_ghost);
        CacheGhostRenderers(_ghost);

        _previewTower = null;
        _ghost.TryGetComponent(out _previewTower);
        _previewTower?.Initialize(_data);

        ApplyGhostVisual(_lastPlacementState, _lastPlacementState == BuildManager.PlacementState.Valid);
    }

    private void DestroyGhost()
    {
        if (_ghost == null) return;

        Destroy(_ghost);
        _ghost = null;
        _currentPrefab = null;
    }

    private static void DisableColliders(GameObject target)
    {
        foreach (var collider in target.GetComponentsInChildren<Collider>())
            collider.enabled = false;

        foreach (var obstacle in target.GetComponentsInChildren<NavMeshObstacle>())
            obstacle.enabled = false;
    }

    private static void DisableGhostGameplay(GameObject target)
    {
        foreach (var shooter in target.GetComponentsInChildren<TowerShooter>(true))
            shooter.enabled = false;

        foreach (var catapultAnimator in target.GetComponentsInChildren<CannonCatapultAnimator>(true))
            catapultAnimator.enabled = false;

        foreach (var trigger in target.GetComponentsInChildren<TrapTrigger>(true))
            trigger.enabled = false;

        foreach (var healthBar in target.GetComponentsInChildren<BuildingHealthBar>(true))
            healthBar.enabled = false;

        foreach (var document in target.GetComponentsInChildren<UIDocument>(true))
            document.enabled = false;
    }

    private static void DisableGhostLevelIndicators(GameObject target)
    {
        foreach (var indicator in target.GetComponentsInChildren<BuildingLevelIndicator>(true))
            indicator.enabled = false;
    }

    private void CacheGhostRenderers(GameObject target)
    {
        _ghostRenderers.Clear();

        foreach (var renderer in target.GetComponentsInChildren<Renderer>())
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var materialCount = renderer.sharedMaterials.Length;
            if (materialCount <= 0) continue;

            _ghostRenderers.Add(new GhostRendererState(renderer, materialCount));
        }
    }

    private void ApplyGhostVisual(BuildManager.PlacementState state, bool isValidPlacement)
    {
        var material = isValidPlacement ? validMaterial : invalidMaterial;
        var tint = GhostTintFor(state);

        for (var index = 0; index < _ghostRenderers.Count; index++)
        {
            var rendererState = _ghostRenderers[index];
            if (rendererState.Renderer == null || material == null) continue;

            rendererState.Renderer.sharedMaterials = FilledMaterials(material, rendererState.MaterialCount);
            ApplyGhostTint(rendererState.Renderer, tint);
        }
    }

    private static Material[] FilledMaterials(Material material, int count)
    {
        var materials = new Material[count];
        for (var index = 0; index < materials.Length; index++)
            materials[index] = material;

        return materials;
    }

    private void ApplyGhostTint(Renderer renderer, Color tint)
    {
        _ghostTintBlock.Clear();
        _ghostTintBlock.SetColor(BaseColorId, tint);
        _ghostTintBlock.SetColor(ColorId, tint);
        renderer.SetPropertyBlock(_ghostTintBlock);
    }

    private Color GhostTintFor(BuildManager.PlacementState state)
    {
        return state switch
        {
            BuildManager.PlacementState.Valid => new Color(0.72f, 0.72f, 0.72f, 1f),
            BuildManager.PlacementState.InsufficientGold => new Color(1.00f, 0.78f, 0.16f, 0.65f),
            BuildManager.PlacementState.InvalidPhase => new Color(0.55f, 0.58f, 0.66f, 0.55f),
            _ => ReadMaterialColor(invalidMaterial, new Color(1f, 0.16f, 0.12f, 0.65f)),
        };
    }

    private static Color ReadMaterialColor(Material material, Color fallback)
    {
        if (material == null) return fallback;
        if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);
        if (material.HasProperty(ColorId)) return material.GetColor(ColorId);
        return fallback;
    }

    private void EnsureVisualHelpersExist()
    {
        if (FindAnyObjectByType<TowerRangeIndicator>() == null)
            gameObject.AddComponent<TowerRangeIndicator>();

        if (FindAnyObjectByType<PaintPlacementPreviewRenderer>() == null)
            gameObject.AddComponent<PaintPlacementPreviewRenderer>();
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private readonly struct GhostRendererState
    {
        public readonly Renderer Renderer;
        public readonly int MaterialCount;

        public GhostRendererState(Renderer renderer, int materialCount)
        {
            Renderer = renderer;
            MaterialCount = materialCount;
        }
    }
}
