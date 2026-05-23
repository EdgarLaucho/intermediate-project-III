using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingBase))]
public sealed class BuildingLevelIndicator : MonoBehaviour
{
    private const string RootName = "__BuildingLevelIndicator";
    private const int BackSortingOrder = 5000;
    private const int FillSortingOrder = 5001;

    [Header("Placement")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float verticalPadding = 0.12f;
    [SerializeField] private bool useUniformPipSize = true;
    [SerializeField] private float uniformPipSize = 0.135f;
    [SerializeField] private float pipSizeFromFootprint = 0.11f;
    [SerializeField] private float minPipSize = 0.08f;
    [SerializeField] private float maxPipSize = 0.16f;
    [SerializeField] private float pipSpacingMultiplier = 1.08f;
    [SerializeField] private float screenVerticalOffset = 14f;
    [SerializeField] private float cameraForwardPadding = 0.05f;
    [SerializeField] private bool usePivotAsHorizontalAnchor = true;
    [SerializeField, Min(1)] private int maxVisiblePips = 5;

    [Header("Camera Scaling")]
    [SerializeField] private bool scaleWithDistance = true;
    [SerializeField] private float referenceDistance = 12f;
    [SerializeField] private float minScale = 0.85f;
    [SerializeField] private float maxScale = 1.2f;

    [Header("Visuals")]
    [SerializeField] private Color pipColor = new Color(1f, 0.9f, 0.26f, 1f);
    [SerializeField] private Color backdropColor = new Color(0.025f, 0.02f, 0.012f, 0.82f);
    [SerializeField] private float backdropScale = 1.42f;

    private readonly List<PipView> pips = new();
    private BuildingBase building;
    private Transform indicatorRoot;
    private Camera cachedCamera;
    private Bounds cachedBounds;
    private bool hasCachedBounds;
    private bool boundsDirty = true;
    private bool subscribed;
    private int renderedLevel = -1;
    private int renderedMaxLevel = -1;
    private float currentPipSize = 0.12f;
    private float nextCameraSearchTime;

    private static Sprite pipSprite;

    public void Initialize(BuildingBase source)
    {
        if (building != source)
        {
            Unsubscribe();
            building = source;
            Subscribe();
        }

        EnsureRoot();
        boundsDirty = true;
        RefreshPipsIfNeeded(true);
        SetIndicatorVisible(enabled && building != null && building.Data != null);
    }

    private void Awake()
    {
        building = GetComponent<BuildingBase>();
        EnsureRoot();
    }

    private void OnEnable()
    {
        if (building == null)
            building = GetComponent<BuildingBase>();

        Subscribe();
        boundsDirty = true;
        RefreshPipsIfNeeded(true);
    }

    private void OnDisable()
    {
        Unsubscribe();
        SetIndicatorVisible(false);
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void LateUpdate()
    {
        if (building == null || building.Data == null || !building.IsAlive)
        {
            SetIndicatorVisible(false);
            return;
        }

        EnsureRoot();
        RefreshPipsIfNeeded(false);

        if (boundsDirty || !hasCachedBounds)
            RecalculateBounds();

        PositionForTopDownCamera();
        SetIndicatorVisible(true);
    }

    private void Subscribe()
    {
        if (subscribed || building == null) return;
        building.OnUpgraded += HandleBuildingUpgraded;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || building == null) return;
        building.OnUpgraded -= HandleBuildingUpgraded;
        subscribed = false;
    }

    private void HandleBuildingUpgraded(BuildingBase upgradedBuilding, UpgradeLevelData upgradeData)
    {
        boundsDirty = true;
        RefreshPipsIfNeeded(true);
    }

    private void EnsureRoot()
    {
        if (indicatorRoot != null) return;

        Transform existingRoot = transform.Find(RootName);
        if (existingRoot != null)
        {
            indicatorRoot = existingRoot;
            return;
        }

        GameObject rootObject = new GameObject(RootName);
        rootObject.layer = gameObject.layer;
        indicatorRoot = rootObject.transform;
        indicatorRoot.SetParent(transform, false);
    }

    private void RefreshPipsIfNeeded(bool force)
    {
        if (building == null || building.Data == null)
            return;

        int currentLevel = Mathf.Max(1, building.CurrentLevel + 1);
        int designedMaxLevel = Mathf.Max(1, building.Data.maxLevel + 1);
        int visibleLevel = Mathf.Clamp(currentLevel, 1, Mathf.Min(designedMaxLevel, maxVisiblePips));

        if (!force && visibleLevel == renderedLevel && designedMaxLevel == renderedMaxLevel)
            return;

        renderedLevel = visibleLevel;
        renderedMaxLevel = designedMaxLevel;
        EnsurePipCount(visibleLevel);
        LayoutPips();
    }

    private void EnsurePipCount(int count)
    {
        EnsureRoot();

        while (pips.Count < count)
            pips.Add(CreatePip(pips.Count));

        for (int index = 0; index < pips.Count; index++)
            pips[index].Root.gameObject.SetActive(index < count);
    }

    private PipView CreatePip(int index)
    {
        Transform pipRoot = new GameObject($"Pip_{index + 1}").transform;
        pipRoot.SetParent(indicatorRoot, false);
        pipRoot.gameObject.layer = gameObject.layer;

        SpriteRenderer backdrop = CreateRenderer("Backdrop", pipRoot, backdropColor, BackSortingOrder);
        backdrop.transform.localScale = Vector3.one * backdropScale;

        SpriteRenderer fill = CreateRenderer("Fill", pipRoot, pipColor, FillSortingOrder);
        fill.transform.localScale = Vector3.one;

        return new PipView(pipRoot, backdrop, fill);
    }

    private SpriteRenderer CreateRenderer(string rendererName, Transform parent, Color color, int sortingOrder)
    {
        GameObject rendererObject = new GameObject(rendererName);
        rendererObject.layer = gameObject.layer;
        Transform rendererTransform = rendererObject.transform;
        rendererTransform.SetParent(parent, false);

        SpriteRenderer renderer = rendererObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetPipSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private void LayoutPips()
    {
        int visibleCount = Mathf.Max(0, renderedLevel);
        if (visibleCount == 0) return;

        float spacing = currentPipSize * pipSpacingMultiplier;
        float startOffset = -spacing * (visibleCount - 1) * 0.5f;

        for (int index = 0; index < visibleCount; index++)
        {
            PipView pip = pips[index];
            pip.Root.localPosition = new Vector3(startOffset + spacing * index, 0f, 0f);
            pip.Root.localRotation = Quaternion.identity;
            pip.Root.localScale = Vector3.one * currentPipSize;
            pip.Backdrop.color = backdropColor;
            pip.Fill.color = pipColor;
        }
    }

    private void RecalculateBounds()
    {
        Bounds bounds = new Bounds(transform.position, Vector3.one);
        bool hasBounds = false;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!ShouldUseRenderer(renderer)) continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                if (!ShouldUseCollider(collider)) continue;
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }

        cachedBounds = bounds;
        hasCachedBounds = true;
        boundsDirty = false;

        float footprint = Mathf.Max(cachedBounds.size.x, cachedBounds.size.z);
        currentPipSize = useUniformPipSize
            ? uniformPipSize
            : Mathf.Clamp(footprint * pipSizeFromFootprint, minPipSize, maxPipSize);
        LayoutPips();
    }

    private bool ShouldUseRenderer(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            return false;

        if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            return false;

        if (indicatorRoot != null && renderer.transform.IsChildOf(indicatorRoot))
            return false;

        if (renderer.GetComponentInParent<BuildingHealthBar>() != null)
            return false;

        return true;
    }

    private bool ShouldUseCollider(Collider collider)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            return false;

        if (collider.isTrigger)
            return false;

        if (indicatorRoot != null && collider.transform.IsChildOf(indicatorRoot))
            return false;

        if (collider.GetComponentInParent<BuildingHealthBar>() != null)
            return false;

        return true;
    }

    private void PositionForTopDownCamera()
    {
        Camera camera = ResolveCamera();
        Vector3 horizontalAnchor = usePivotAsHorizontalAnchor ? transform.position : cachedBounds.center;
        Vector3 position = new Vector3(
            horizontalAnchor.x,
            cachedBounds.max.y + verticalPadding + currentPipSize * 0.5f,
            horizontalAnchor.z);

        if (camera != null)
        {
            indicatorRoot.rotation = camera.transform.rotation;

            Vector3 anchor = new Vector3(horizontalAnchor.x, cachedBounds.max.y, horizontalAnchor.z);
            Vector3 screenPosition = camera.WorldToScreenPoint(anchor);
            if (screenPosition.z > 0.01f)
            {
                screenPosition.y += screenVerticalOffset;
                position = camera.ScreenToWorldPoint(screenPosition);
                position -= camera.transform.forward * cameraForwardPadding;
            }
        }

        indicatorRoot.position = position;

        float distanceScale = 1f;
        if (scaleWithDistance && camera != null)
        {
            float distance = Vector3.Distance(camera.transform.position, position);
            distanceScale = Mathf.Clamp(distance / Mathf.Max(0.01f, referenceDistance), minScale, maxScale);
        }

        Vector3 parentScale = transform.lossyScale;
        indicatorRoot.localScale = new Vector3(
            distanceScale / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
            distanceScale / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)),
            distanceScale / Mathf.Max(0.001f, Mathf.Abs(parentScale.z)));
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null) return targetCamera;
        if (cachedCamera != null) return cachedCamera;

        if (Time.unscaledTime < nextCameraSearchTime)
            return null;

        nextCameraSearchTime = Time.unscaledTime + 0.5f;
        cachedCamera = Camera.main;
        return cachedCamera;
    }

    private void SetIndicatorVisible(bool visible)
    {
        if (indicatorRoot != null && indicatorRoot.gameObject.activeSelf != visible)
            indicatorRoot.gameObject.SetActive(visible);
    }

    private static Sprite GetPipSprite()
    {
        if (pipSprite != null) return pipSprite;

        const int size = 64;
        const float radius = 28f;
        const float feather = 3f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "GeneratedLevelPip",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius + feather - distance) / feather);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        pipSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        pipSprite.name = "GeneratedLevelPipSprite";
        pipSprite.hideFlags = HideFlags.HideAndDontSave;
        return pipSprite;
    }

    private readonly struct PipView
    {
        public readonly Transform Root;
        public readonly SpriteRenderer Backdrop;
        public readonly SpriteRenderer Fill;

        public PipView(Transform root, SpriteRenderer backdrop, SpriteRenderer fill)
        {
            Root = root;
            Backdrop = backdrop;
            Fill = fill;
        }
    }
}