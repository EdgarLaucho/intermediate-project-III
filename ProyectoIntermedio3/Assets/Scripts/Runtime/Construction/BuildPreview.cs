using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;
using System.Collections.Generic;

// Renders the translucent "ghost" building and the cell highlight ring while the
// player is choosing where to place a building. It listens to ConstructionEvents
// so it stays decoupled from the input and state-management logic in
// ConstructionPresenter.
public class BuildPreview : MonoBehaviour
{
    #region Inspector Fields

    [Header("Materials")]
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    [Header("Cell Highlight")]
    [SerializeField] private GameObject cellHighlightPrefab;

    // "Feel" values control the subtle idle animation applied to the ghost every frame.
    [Header("Feel")]
    [SerializeField] private float ghostBobHeight = 0.045f;
    [SerializeField] private float ghostBobSpeed = 3.2f;
    [SerializeField] private float ghostBreathAmount = 0.025f;
    [SerializeField] private float ghostTiltDegrees = 1.4f;
    [SerializeField] private float invalidTiltDegrees = 3.6f;
    [SerializeField] private float highlightSpinSpeed = 28f;

    #endregion

    #region Constants

    private const float SnapDuration = 0.085f;  // Time to lerp ghost to a new cell.
    private const float InvalidShakeDuration = 0.18f;   // Duration of the rejection shake.
    private const float InvalidShakeDistance = 0.075f;  // Peak lateral offset during shake.

    #endregion

    #region Nested Types

    // Per-material cache built once in CacheGhostRenderers.
    // Storing HasBaseColor / HasColor avoids repeated HasProperty calls every frame.
    private sealed class GhostMaterialState
    {
        public Material Material;
        public Color BaseColor;
        public bool HasBaseColor;
        public bool HasColor;
    }

    #endregion

    #region Runtime State

    private BuildingData _data;
    private GameObject _ghost;
    private GameObject _highlight;
    private Tower _previewTower;
    private Vector2Int _lastCell;
    private bool _hasLastCell;
    private Vector3 _displayPosition;
    private Vector3 _snapFrom;
    private Vector3 _snapTo;
    private float _snapT = SnapDuration;  // Start at end so no lerp on first frame.
    private float _invalidT;              // Counts down from InvalidShakeDuration to 0.
    private Vector3 _ghostBaseScale = Vector3.one;
    private Quaternion _ghostBaseRotation = Quaternion.identity;
    private Vector3 _highlightBaseScale = Vector3.one;
    private Quaternion _highlightBaseRotation = Quaternion.identity;
    private BuildManager.PlacementState _lastPlacementState = BuildManager.PlacementState.MissingData;

    private readonly List<GhostMaterialState> _ghostMaterials = new();
    private readonly List<Renderer> _highlightRenderers = new();

    #endregion

    #region Lifecycle

    private void Awake()
    {
        EnsureRangeIndicatorExists();

        if (validMaterial == null) Debug.LogError("[BuildPreview] 'validMaterial' not assigned.");
        if (invalidMaterial == null) Debug.LogError("[BuildPreview] 'invalidMaterial' not assigned.");
    }

    // The range indicator may not be on this same GameObject; add it lazily so the
    // scene can be set up without a pre-placed TowerRangeIndicator component.
    private void EnsureRangeIndicatorExists()
    {
        if (FindFirstObjectByType<TowerRangeIndicator>() != null)
            return;

        gameObject.AddComponent<TowerRangeIndicator>();
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
        DestroyHighlight();
    }

    #endregion

    #region Public API

    // Spawns the ghost and highlight for the given building data.
    // Calls Hide() first so switching from one building to another cleans up correctly.
    public void Show(BuildingData data)
    {
        Hide();
        _data = data;

        if (data?.prefab == null) return;

        _ghost = Instantiate(data.prefab);
        _ghost.name = "__BuildPreview_Ghost";
        _ghostBaseScale = _ghost.transform.localScale;
        _ghostBaseRotation = _ghost.transform.rotation;
        DisableColliders(_ghost);
        CacheGhostRenderers(_ghost);
        _ghost.TryGetComponent<Tower>(out _previewTower);

        // Initialize the preview tower so TowerRangeIndicator can read its AttackRange.
        _previewTower?.Initialize(data);

        if (cellHighlightPrefab != null)
        {
            _highlight = Instantiate(cellHighlightPrefab);
            _highlight.name = "__BuildPreview_Highlight";
            _highlightBaseScale = _highlight.transform.localScale;
            _highlightBaseRotation = _highlight.transform.rotation;
            _highlightRenderers.Clear();
            _highlightRenderers.AddRange(_highlight.GetComponentsInChildren<Renderer>());
        }

        _hasLastCell = false;
        _snapT = SnapDuration;
        _invalidT = 0f;
    }

    public void Hide()
    {
        DestroyGhost();
        DestroyHighlight();
        ConstructionEvents.TowerFocused(null);
        _data = null;
        _previewTower = null;
        _ghostMaterials.Clear();
        _highlightRenderers.Clear();
        _hasLastCell = false;
    }

    // Called every frame while placement is active. Moves the ghost to the snapped
    // world position, applies idle animation, and refreshes visual tints on state changes.
    public void UpdatePreview(Vector2Int coords, Vector3 snappedWorldPosition, BuildManager.PlacementValidation validation)
    {
        if (_data == null) return;

        bool changedCell = !_hasLastCell || _lastCell != coords;
        bool changedState = validation.State != _lastPlacementState;

        if (changedCell)
        {
            // Record the current display position as the lerp origin so the ghost
            // slides smoothly rather than teleporting when the cursor crosses a cell.
            _snapFrom = _hasLastCell ? _displayPosition : snappedWorldPosition;
            _snapTo = snappedWorldPosition;
            _snapT = 0f;
            _lastCell = coords;
            _hasLastCell = true;
        }

        if (changedCell || changedState)
        {
            _lastPlacementState = validation.State;
            ApplyGhostVisual(validation);
            ApplyHighlightColor(validation);
        }

        // Advance the snap lerp and evaluate the eased position.
        _snapT = Mathf.Min(SnapDuration, _snapT + Time.deltaTime);
        float snapEase = EaseOutCubic(SnapDuration <= 0f ? 1f : _snapT / SnapDuration);
        _displayPosition = Vector3.Lerp(_snapFrom, _snapTo, snapEase);

        // Rejection shake: a damped sine that fades out over InvalidShakeDuration.
        Vector3 shake = Vector3.zero;
        if (_invalidT > 0f)
        {
            _invalidT = Mathf.Max(0f, _invalidT - Time.deltaTime);
            float t = 1f - (_invalidT / InvalidShakeDuration);
            float fade = 1f - t;
            shake = Vector3.right * (Mathf.Sin(t * Mathf.PI * 8f) * InvalidShakeDistance * fade);
        }

        Vector3 finalPosition = _displayPosition + shake;
        float time = Time.unscaledTime;
        float bob = Mathf.Sin(time * ghostBobSpeed) * ghostBobHeight;
        float breath = 1f + Mathf.Sin(time * ghostBobSpeed * 0.72f) * ghostBreathAmount;
        float tiltAmount = validation.IsValid ? ghostTiltDegrees : invalidTiltDegrees;
        float tiltX = Mathf.Sin(time * ghostBobSpeed * 1.17f) * tiltAmount;
        float tiltZ = Mathf.Cos(time * ghostBobSpeed * 0.93f) * tiltAmount;

        if (_ghost != null)
        {
            // Dampen the bob when invalid so the ghost looks more "grounded" / stuck.
            _ghost.transform.position = finalPosition + Vector3.up * (validation.IsValid ? bob : bob * 0.35f);
            _ghost.transform.rotation = _ghostBaseRotation * Quaternion.Euler(tiltX, 0f, tiltZ);
            _ghost.transform.localScale = _ghostBaseScale * breath;
        }

        if (_highlight != null)
        {
            float highlightPulse = 1f + Mathf.Sin(time * ghostBobSpeed * 1.4f) * 0.035f;
            float spinDirection = validation.IsValid ? 1f : -1.45f;  // Reverses when invalid.
            _highlight.transform.position = finalPosition + Vector3.up * 0.012f;
            _highlight.transform.rotation = _highlightBaseRotation * Quaternion.Euler(0f, time * highlightSpinSpeed * spinDirection, 0f);
            _highlight.transform.localScale = _highlightBaseScale * highlightPulse;
        }

        if (_previewTower != null)
            ConstructionEvents.TowerFocused(_previewTower);
        else
            ConstructionEvents.TowerFocused(null);
    }

    // Kicks off the rejection shake animation. Called by ConstructionPresenter
    // when the player clicks on an invalid cell.
    public void PlayInvalidFeedback()
    {
        _invalidT = InvalidShakeDuration;
    }

    #endregion

    #region Ghost Setup Helpers

    // Colliders and NavMesh obstacles are disabled so the ghost doesn't interact
    // with the physics world or carve the NavMesh while the player is deciding.
    private static void DisableColliders(GameObject go)
    {
        foreach (var col in go.GetComponentsInChildren<Collider>())
            col.enabled = false;

        foreach (var obstacle in go.GetComponentsInChildren<NavMeshObstacle>())
            obstacle.enabled = false;
    }

    // Walks every renderer in the ghost hierarchy, forces transparent rendering,
    // and caches property availability to avoid repeated HasProperty calls later.
    private void CacheGhostRenderers(GameObject go)
    {
        _ghostMaterials.Clear();

        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Material[] mats = renderer.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                PrepareTransparentMaterial(mats[i]);
                _ghostMaterials.Add(new GhostMaterialState
                {
                    Material = mats[i],
                    HasBaseColor = mats[i].HasProperty("_BaseColor"),
                    HasColor = mats[i].HasProperty("_Color"),
                    BaseColor = ReadMaterialColor(mats[i], Color.white),
                });
            }
            renderer.materials = mats;
        }
    }

    #endregion

    #region Visual Tinting

    // Blends each material's original colour toward the state tint so the ghost
    // retains some of the building's own colouring rather than going fully solid.
    private void ApplyGhostVisual(BuildManager.PlacementValidation validation)
    {
        Color tint = GhostTintFor(validation.State);
        foreach (var state in _ghostMaterials)
        {
            if (state.Material == null) continue;
            Color color = Color.Lerp(state.BaseColor, tint, 0.45f);
            color.a = validation.IsValid ? 0.50f : 0.42f;
            if (state.HasBaseColor) state.Material.SetColor("_BaseColor", color);
            if (state.HasColor) state.Material.SetColor("_Color", color);
        }
    }

    private void ApplyHighlightColor(BuildManager.PlacementValidation validation)
    {
        Color target = HighlightColorFor(validation.State);

        foreach (var renderer in _highlightRenderers)
        {
            if (renderer == null) continue;
            foreach (var mat in renderer.materials)
            {
                if (mat == null) continue;
                SetMaterialColorIfSupported(mat, target);
            }
        }
    }

    // Ghost tint falls back to the assigned material's colour so designers can
    // override the exact green/red via the Inspector without touching code.
    private Color GhostTintFor(BuildManager.PlacementState state)
    {
        if (state == BuildManager.PlacementState.Valid)
            return ReadMaterialColor(validMaterial, new Color(0.25f, 1f, 0.35f, 0.5f));

        if (state == BuildManager.PlacementState.InsufficientGold)
            return new Color(1.00f, 0.78f, 0.16f, 0.45f);

        if (state == BuildManager.PlacementState.InvalidPhase)
            return new Color(0.55f, 0.58f, 0.66f, 0.42f);

        return ReadMaterialColor(invalidMaterial, new Color(1f, 0.16f, 0.12f, 0.42f));
    }

    private static Color HighlightColorFor(BuildManager.PlacementState state)
    {
        return state switch
        {
            BuildManager.PlacementState.Valid => new Color(0.25f, 1.00f, 0.35f, 0.42f),
            BuildManager.PlacementState.Occupied => new Color(1.00f, 0.18f, 0.13f, 0.45f),
            BuildManager.PlacementState.InsufficientGold => new Color(1.00f, 0.78f, 0.16f, 0.48f),
            BuildManager.PlacementState.InvalidPhase => new Color(0.55f, 0.58f, 0.66f, 0.40f),
            _ => new Color(0.95f, 0.08f, 0.08f, 0.42f),
        };
    }

    #endregion

    #region Material Utilities

    // Forces URP/HDRP materials into transparent mode at runtime so they render
    // correctly as a ghost even if the original material is fully opaque.
    private static void PrepareTransparentMaterial(Material mat)
    {
        if (mat == null) return;
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    // Reads _BaseColor first (URP), falls back to _Color (Built-in), then returns defaultColor.
    private static Color ReadMaterialColor(Material mat, Color defaultColor)
    {
        if (mat == null) return defaultColor;
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
        return defaultColor;
    }

    private static void SetMaterialColorIfSupported(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    }

    #endregion

    #region Math Helpers

    // EaseOutCubic produces a smooth deceleration for the cell-to-cell snap lerp.
    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    #endregion

    #region Cleanup

    private void DestroyGhost()
    {
        if (_ghost != null) { Destroy(_ghost); _ghost = null; }
    }

    private void DestroyHighlight()
    {
        if (_highlight != null) { Destroy(_highlight); _highlight = null; }
    }

    #endregion
}
