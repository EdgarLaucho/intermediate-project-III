using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildFeedbackController : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private GridManager grid;

    [Header("Sprites")]
    [SerializeField] private Sprite fillSprite;
    [SerializeField] private Sprite ringSprite;
    [SerializeField] private Sprite burstSprite;

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
    private bool _subscribed;

    #endregion

    #region Types

    private enum FeedbackKind
    {
        Build,
        Upgrade,
        Demolish,
    }

    private class CellFlash
    {
        public GameObject Root;
        public SpriteRenderer Fill;
        public SpriteRenderer Ring;
        public SpriteRenderer Echo;
        public SpriteRenderer Burst;
        public Vector3 Center;
        public float CellSize;
        public float Age;
        public float Duration;
        public FeedbackKind Kind;
    }

    #endregion

    #region Lifecycle

    private void Awake()
    {
        if (grid == null)
            grid = FindAnyObjectByType<GridManager>(FindObjectsInactive.Exclude);
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void OnDestroy()
    {
        ClearFlashes();
    }

    private void Update()
    {
        for (int i = _cellFlashes.Count - 1; i >= 0; i--)
        {
            var flash = _cellFlashes[i];
            flash.Age += Time.deltaTime;

            if (flash.Age >= flash.Duration)
            {
                DestroyFlash(flash);
                _cellFlashes.RemoveAt(i);
                continue;
            }

            DrawCellFlash(flash);
        }
    }

    #endregion

    #region Events

    private void Subscribe()
    {
        if (_subscribed) return;
        ConstructionEvents.OnBuildingPlaced += HandleBuildingPlaced;
        ConstructionEvents.OnBuildingUpgraded += HandleBuildingUpgraded;
        ConstructionEvents.OnBuildingDemolished += HandleBuildingDemolished;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        ConstructionEvents.OnBuildingPlaced -= HandleBuildingPlaced;
        ConstructionEvents.OnBuildingUpgraded -= HandleBuildingUpgraded;
        ConstructionEvents.OnBuildingDemolished -= HandleBuildingDemolished;
        _subscribed = false;
    }

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

    #endregion

    #region Flash Setup

    private void AddCellFlash(Vector2Int coords, FeedbackKind kind, float duration)
    {
        if (grid == null || fillSprite == null || ringSprite == null)
            return;

        var root = new GameObject($"BuildFeedback {kind} {coords.x},{coords.y}");
        root.transform.SetParent(transform, false);

        var flash = new CellFlash
        {
            Root = root,
            Fill = CreateLayer(root.transform, "Fill", fillSprite, -10),
            Ring = CreateLayer(root.transform, "Ring", ringSprite, -9),
            Echo = CreateLayer(root.transform, "Echo", ringSprite, -8),
            Burst = burstSprite != null ? CreateLayer(root.transform, "Burst", burstSprite, -7) : null,
            Center = grid.GridToWorld(coords) + Vector3.up * yOffset,
            CellSize = grid.CellSize,
            Duration = Mathf.Max(0.01f, duration),
            Kind = kind,
        };

        _cellFlashes.Add(flash);
    }

    private static SpriteRenderer CreateLayer(Transform parent, string name, Sprite sprite, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void ClearFlashes()
    {
        for (int i = _cellFlashes.Count - 1; i >= 0; i--)
            DestroyFlash(_cellFlashes[i]);

        _cellFlashes.Clear();
    }

    private static void DestroyFlash(CellFlash flash)
    {
        if (flash?.Root == null) return;
        Destroy(flash.Root);
    }

    #endregion

    #region Building Animation

    private IEnumerator PlaySpawnPop(Transform target)
    {
        if (target == null) yield break;

        var baseScale = target.localScale;
        var basePosition = target.position;
        var baseRotation = target.rotation;
        var elapsed = 0f;

        while (elapsed < popDuration && target != null)
        {
            elapsed += Time.deltaTime;
            var t = Normalized(elapsed, popDuration);
            var scale = EvaluatePopScale(t);
            var lift = Mathf.Sin(t * Mathf.PI) * popLiftHeight;
            var wobble = Mathf.Sin(t * Mathf.PI * 2.4f) * popWobbleDegrees * (1f - t);

            target.localScale = baseScale * scale;
            target.position = basePosition + Vector3.up * lift;
            target.rotation = baseRotation * Quaternion.Euler(0f, wobble, 0f);
            yield return null;
        }

        ResetTransform(target, baseScale, basePosition, baseRotation);
    }

    private IEnumerator PlayUpgradePulse(Transform target)
    {
        if (target == null) yield break;

        var baseScale = target.localScale;
        var basePosition = target.position;
        var baseRotation = target.rotation;
        var elapsed = 0f;

        while (elapsed < upgradeFlashDuration && target != null)
        {
            elapsed += Time.deltaTime;
            var t = Normalized(elapsed, upgradeFlashDuration);
            var surge = Mathf.Sin(t * Mathf.PI);
            var twist = Mathf.Sin(t * Mathf.PI * 2f) * 5.5f * (1f - EaseOutCubic(t));

            target.localScale = baseScale * (1f + surge * (upgradePulseScale - 1f));
            target.position = basePosition + Vector3.up * (surge * upgradeLiftHeight);
            target.rotation = baseRotation * Quaternion.Euler(0f, twist, 0f);
            yield return null;
        }

        ResetTransform(target, baseScale, basePosition, baseRotation);
    }

    private float EvaluatePopScale(float t)
    {
        if (t < 0.58f)
            return Mathf.Lerp(popStartScale, popOvershootScale, EaseOutCubic(t / 0.58f));

        return Mathf.Lerp(popOvershootScale, 1f, EaseOutCubic((t - 0.58f) / 0.42f));
    }

    private static void ResetTransform(Transform target, Vector3 scale, Vector3 position, Quaternion rotation)
    {
        if (target == null) return;
        target.localScale = scale;
        target.position = position;
        target.rotation = rotation;
    }

    #endregion

    #region Cell Flash Drawing

    private void DrawCellFlash(CellFlash flash)
    {
        var t = Mathf.Clamp01(flash.Age / flash.Duration);

        switch (flash.Kind)
        {
            case FeedbackKind.Upgrade:
                DrawUpgradeFlash(flash, t);
                break;
            case FeedbackKind.Demolish:
                DrawDemolishFlash(flash, t);
                break;
            default:
                DrawBuildFlash(flash, t);
                break;
        }
    }

    private void DrawBuildFlash(CellFlash flash, float t)
    {
        var fade = 1f - t;
        var ease = EaseOutCubic(t);
        var spin = t * 96f;

        SetLayer(flash.Fill, flash.Center, 0f, 0f, flash.CellSize * Mathf.Lerp(0.62f, 1.04f, ease), WithAlpha(fillColor, fade * fade));
        SetLayer(flash.Ring, flash.Center, 0.006f, spin, flash.CellSize * Mathf.Lerp(0.72f, 1.28f, ease), WithAlpha(ringColor, fade));
        SetLayer(flash.Echo, flash.Center, 0.011f, -spin * 1.4f, flash.CellSize * Mathf.Lerp(0.54f, 1.46f, ease), WithAlpha(ringColor, fade * 0.42f));
        SetLayer(flash.Burst, flash.Center, 0f, 0f, 0f, Color.clear);
    }

    private void DrawUpgradeFlash(CellFlash flash, float t)
    {
        var fade = 1f - t;
        var ease = EaseOutCubic(t);
        var surge = Mathf.Sin(t * Mathf.PI);
        var spin = t * 180f;

        SetLayer(flash.Fill, flash.Center, 0f, t * 50f, flash.CellSize * Mathf.Lerp(0.74f, 1.10f, ease), WithAlpha(upgradeFillColor, fade * (0.72f + surge * 0.45f)));
        SetLayer(flash.Ring, flash.Center, 0.007f, spin, flash.CellSize * Mathf.Lerp(0.48f, 1.10f, ease), WithAlpha(upgradeRingColor, fade));
        SetLayer(flash.Echo, flash.Center, 0.013f, -spin * 0.8f, flash.CellSize * Mathf.Lerp(0.80f, 1.42f, ease), WithAlpha(upgradeRingColor, fade * 0.42f));
        SetLayer(flash.Burst, flash.Center, 0.018f, t * -120f, flash.CellSize * Mathf.Lerp(0.42f, 1.14f, ease), WithAlpha(upgradeRingColor, fade * 0.76f));
    }

    private void DrawDemolishFlash(CellFlash flash, float t)
    {
        var fade = 1f - t;
        var ease = EaseOutCubic(t);
        var snap = 1f - EaseOutCubic(Mathf.Clamp01(t / 0.62f));
        var spin = t * -220f;

        SetLayer(flash.Fill, flash.Center, 0f, t * -70f, flash.CellSize * Mathf.Lerp(1.08f, 0.34f, ease), WithAlpha(demolishFillColor, fade * fade));
        SetLayer(flash.Ring, flash.Center, 0.008f, spin, flash.CellSize * Mathf.Lerp(1.34f, 0.58f, ease), WithAlpha(demolishRingColor, fade));
        SetLayer(flash.Echo, flash.Center, 0.014f, -spin * 0.45f, flash.CellSize * Mathf.Lerp(0.50f, 1.62f, ease), WithAlpha(demolishRingColor, fade * 0.40f));
        SetLayer(flash.Burst, flash.Center, 0.020f, t * 260f, flash.CellSize * Mathf.Lerp(0.62f, 1.56f, ease), WithAlpha(demolishShardColor, fade * (0.55f + snap * 0.55f)));
    }

    private static void SetLayer(SpriteRenderer renderer, Vector3 center, float lift, float yaw, float scale, Color color)
    {
        if (renderer == null) return;

        renderer.color = color;
        renderer.enabled = color.a > 0f && scale > 0f;
        renderer.transform.position = center + Vector3.up * lift;
        renderer.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    #endregion

    #region Helpers

    private static Color WithAlpha(Color color, float alphaMultiplier)
    {
        color.a *= alphaMultiplier;
        return color;
    }

    private static float Normalized(float elapsed, float duration)
    {
        return duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    #endregion
}