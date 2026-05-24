using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingBase))]
public class BuildingLevelIndicator : MonoBehaviour
{
    private const string RootName = "BuildingLevelIndicator";
    private const string PipSpriteResourcePath = "LevelPip";
    private const int BackSortingOrder = 5000;
    private const int FillSortingOrder = 5001;

    [Header("Placement")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector3 localOffset = new(0f, 1.6f, 0f);
    [SerializeField] private float pipSize = 0.135f;
    [SerializeField] private float pipSpacingMultiplier = 1.08f;
    [SerializeField, Min(1)] private int maxVisiblePips = 5;

    [Header("Visuals")]
    [SerializeField] private Color pipColor = new(1f, 0.9f, 0.26f, 1f);
    [SerializeField] private Color backdropColor = new(0.025f, 0.02f, 0.012f, 0.82f);
    [SerializeField] private float backdropScale = 1.42f;

    private readonly List<PipView> pips = new();
    private BuildingBase building;
    private Transform indicatorRoot;
    private Camera cachedCamera;
    private bool subscribed;
    private int renderedLevel = -1;

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
        RefreshPips(true);
        UpdateVisibility();
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
        RefreshPips(true);
        UpdateVisibility();
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
        if (!UpdateVisibility())
            return;

        RefreshPips(false);
        PositionIndicator();
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
        RefreshPips(true);
    }

    private bool UpdateVisibility()
    {
        var visible = enabled && building != null && building.Data != null && building.IsAlive;
        SetIndicatorVisible(visible);
        return visible;
    }

    private void EnsureRoot()
    {
        if (indicatorRoot != null) return;

        var existingRoot = transform.Find(RootName);
        if (existingRoot != null)
        {
            indicatorRoot = existingRoot;
            return;
        }

        var rootObject = new GameObject(RootName) { layer = gameObject.layer };
        indicatorRoot = rootObject.transform;
        indicatorRoot.SetParent(transform, false);
    }

    private void RefreshPips(bool force)
    {
        if (building == null || building.Data == null)
            return;

        var maxLevel = Mathf.Max(1, building.Data.maxLevel + 1);
        var level = Mathf.Clamp(building.CurrentLevel + 1, 1, Mathf.Min(maxLevel, maxVisiblePips));

        if (!force && level == renderedLevel)
            return;

        renderedLevel = level;
        EnsurePipCount(level);
        LayoutPips(level);
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
        var pipRoot = new GameObject($"Pip_{index + 1}") { layer = gameObject.layer }.transform;
        pipRoot.SetParent(indicatorRoot, false);

        var backdrop = CreateRenderer("Backdrop", pipRoot, backdropColor, BackSortingOrder);
        backdrop.transform.localScale = Vector3.one * backdropScale;

        var fill = CreateRenderer("Fill", pipRoot, pipColor, FillSortingOrder);
        fill.transform.localScale = Vector3.one;

        return new PipView(pipRoot, backdrop, fill);
    }

    private SpriteRenderer CreateRenderer(string rendererName, Transform parent, Color color, int sortingOrder)
    {
        var rendererObject = new GameObject(rendererName) { layer = gameObject.layer };
        rendererObject.transform.SetParent(parent, false);

        var renderer = rendererObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetPipSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private void LayoutPips(int visibleCount)
    {
        var spacing = pipSize * pipSpacingMultiplier;
        var startOffset = -spacing * (visibleCount - 1) * 0.5f;

        for (int index = 0; index < visibleCount; index++)
        {
            var pip = pips[index];
            pip.Root.SetLocalPositionAndRotation(new Vector3(startOffset + spacing * index, 0f, 0f), Quaternion.identity);
            pip.Root.localScale = Vector3.one * pipSize;
            pip.Backdrop.color = backdropColor;
            pip.Fill.color = pipColor;
        }
    }

    private void PositionIndicator()
    {
        EnsureRoot();
        indicatorRoot.localPosition = localOffset;
        indicatorRoot.localScale = Vector3.one;

        Camera camera = ResolveCamera();
        if (camera != null)
            indicatorRoot.rotation = camera.transform.rotation;
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null) return targetCamera;
        if (cachedCamera != null) return cachedCamera;

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
        if (pipSprite != null)
            return pipSprite;

        pipSprite = Resources.Load<Sprite>(PipSpriteResourcePath);
        if (pipSprite == null)
            Debug.LogError($"[BuildingLevelIndicator] Missing sprite at Resources/{PipSpriteResourcePath}.");

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