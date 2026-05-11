using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

// Persistent on-screen gold display driven by UI Toolkit.
// Subscribes to EconomyEvents.OnGoldChanged — no direct dependency on EconomyManager.
// Animates the displayed value, pulses the panel on change, and spawns floating +/- delta popups.
public class GoldHUD : MonoBehaviour
{
    #region Inspector Fields

    [Header("Feel")]
    [SerializeField] private float valueAnimationDuration = 0.38f;
    [SerializeField] private float pulseDuration = 0.28f;
    [SerializeField] private float deltaDuration = 0.74f;
    [SerializeField] private float deltaRise = 26f;
    [SerializeField] private int lowGoldThreshold = 30;

    #endregion

    #region Runtime State

    private VisualElement _goldPanel;
    private VisualElement _goldIcon;
    private VisualElement _deltaLayer;
    private Label _goldLabel;
    private Label _goldTitle;
    private UIDocument _uiDocument;

    private readonly List<DeltaPopup> _deltaPopups = new();
    private int _targetGold;
    private int _animationStartGold;
    private float _displayedGold;
    private float _valueAnimationTime;
    private float _pulseTime = 999f;
    private int _lastDelta;
    private bool _hasValue;
    private bool _uiReady;
    private bool _subscribed;
    private bool _reportedMissingDocument;
    private bool _reportedMissingGoldLabel;

    #endregion

    #region Color Constants

    private static readonly Color NormalAmountColor = new(1.00f, 0.87f, 0.34f, 1f);
    private static readonly Color GainColor = new(0.55f, 1.00f, 0.38f, 1f);
    private static readonly Color SpendColor = new(1.00f, 0.45f, 0.28f, 1f);
    private static readonly Color LowGoldColor = new(1.00f, 0.58f, 0.22f, 1f);
    private static readonly Color BaseBorderColor = new(1.00f, 0.76f, 0.25f, 0.68f);

    #endregion

    #region Inner Types

    private sealed class DeltaPopup
    {
        public Label Label;
        public float Age;
        public float StartLeft;   // resolved pixel position in _deltaLayer local space
        public float StartTop;
        public float DriftX;      // horizontal drift direction per stack slot (+/-)
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        TryInitializeUI();
    }

    private void Update()
    {
        if (!_uiReady)
            TryInitializeUI();

        UpdateValueAnimation();
        UpdatePulse();
        UpdateDeltaPopups();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    #endregion

    #region Initialization

    private void TrySubscribe()
    {
        if (!_uiReady || _subscribed) return;
        EconomyEvents.OnGoldChanged += HandleGoldChanged;
        _subscribed = true;
    }

    private bool TryInitializeUI()
    {
        if (_uiReady) return true;

        // UIDocument lives on the same GameObject; cache it lazily so Awake order doesn't matter
        UIDocument doc = CacheDocument();
        if (doc == null)
        {
            if (!_reportedMissingDocument)
            {
                Debug.LogError("[GoldHUD] UIDocument not found on this GameObject.", this);
                _reportedMissingDocument = true;
            }
            return false;
        }

        VisualElement root = doc.rootVisualElement;
        if (root == null)
            return false;

        _goldPanel = root.Q<VisualElement>("gold-panel");
        _goldIcon = root.Q<VisualElement>("gold-icon");
        _deltaLayer = root.Q<VisualElement>("gold-delta-layer");
        _goldTitle = root.Q<Label>("gold-title");
        _goldLabel = root.Q<Label>("gold-label");
        if (_goldLabel == null)
        {
            if (!_reportedMissingGoldLabel)
            {
                Debug.LogError("[GoldHUD] 'gold-label' element not found in UXML.", this);
                _reportedMissingGoldLabel = true;
            }
            return false;
        }

        if (_deltaLayer != null)
            _deltaLayer.pickingMode = PickingMode.Ignore;

        // Anchor scale transforms to the visual centre so punch animations expand symmetrically
        if (_goldPanel != null)
            _goldPanel.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);
        if (_goldIcon != null)
            _goldIcon.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);

        _uiReady = true;
        TrySubscribe();
        return true;
    }

    // Lazy-cache the UIDocument so the component can be queried before Awake runs on it
    private UIDocument CacheDocument()
    {
        if (_uiDocument == null)
            _uiDocument = GetComponent<UIDocument>();

        return _uiDocument;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        EconomyEvents.OnGoldChanged -= HandleGoldChanged;
        _subscribed = false;
    }

    #endregion

    #region Gold Display

    // ── Display ──────────────────────────────────────────────────────────────

    private void HandleGoldChanged(int gold)
    {
        int previousGold = _hasValue ? _targetGold : gold;
        int delta = gold - previousGold;

        // Restart the count-up from wherever the display currently sits (smooth chaining)
        _animationStartGold = Mathf.RoundToInt(_displayedGold);
        _targetGold = gold;
        _valueAnimationTime = 0f;
        _lastDelta = delta;
        _pulseTime = 0f;
        _hasValue = true;

        if (delta != 0)
            SpawnDelta(delta);
    }

    private void SetImmediate(int gold)
    {
        _targetGold = gold;
        _animationStartGold = gold;
        _displayedGold = gold;
        _hasValue = true;
        SetDisplayedGold(gold);
        ApplyEventColor(0f, 0);
    }

    private void UpdateValueAnimation()
    {
        if (!_hasValue) return;

        if (Mathf.Approximately(_displayedGold, _targetGold))
        {
            SetDisplayedGold(_targetGold);
            return;
        }

        _valueAnimationTime += Time.unscaledDeltaTime;
        float duration = Mathf.Max(0.01f, valueAnimationDuration);
        float t = Mathf.Clamp01(_valueAnimationTime / duration);
        // EaseOutCubic: fast start → slow finish for a natural counter roll-up
        float ease = 1f - Mathf.Pow(1f - t, 3f);

        _displayedGold = Mathf.Lerp(_animationStartGold, _targetGold, ease);
        SetDisplayedGold(Mathf.RoundToInt(_displayedGold));

        if (t >= 1f)
            _displayedGold = _targetGold;
    }

    private void UpdatePulse()
    {
        if (_pulseTime > pulseDuration)
        {
            // Pulse expired — reset scale and return label color to steady-state
            if (_goldPanel != null) _goldPanel.style.scale = new Scale(Vector3.one);
            if (_goldIcon != null) _goldIcon.style.scale = new Scale(Vector3.one);
            ApplyEventColor(0f, 0);
            return;
        }

        _pulseTime += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_pulseTime / Mathf.Max(0.01f, pulseDuration));
        // Sine arch: peaks at t=0.5, returns to zero at both ends — gives a clean punch then settle
        float punch = Mathf.Sin(t * Mathf.PI);
        float panelScale = 1f + punch * (_lastDelta >= 0 ? 0.035f : 0.024f);
        float iconScale = 1f + punch * (_lastDelta >= 0 ? 0.13f : 0.08f);

        if (_goldPanel != null) _goldPanel.style.scale = new Scale(new Vector3(panelScale, panelScale, 1f));
        if (_goldIcon != null) _goldIcon.style.scale = new Scale(new Vector3(iconScale, iconScale, 1f));
        ApplyEventColor(punch, _lastDelta);
    }

    private void SetDisplayedGold(int gold)
    {
        if (_goldLabel != null)
            _goldLabel.text = gold.ToString();

        if (_goldTitle != null)
            _goldTitle.style.color = new StyleColor(gold <= lowGoldThreshold ? LowGoldColor : new Color(1f, 0.875f, 0.56f, 0.74f));
    }

    private void ApplyEventColor(float amount, int delta)
    {
        // Blend between the neutral border color and the event color (green gain / red spend)
        Color eventColor = delta > 0 ? GainColor : delta < 0 ? SpendColor : BaseBorderColor;
        Color amountColor = _targetGold <= lowGoldThreshold ? LowGoldColor : NormalAmountColor;
        amountColor = Color.Lerp(amountColor, eventColor, Mathf.Clamp01(amount));

        if (_goldLabel != null)
            _goldLabel.style.color = new StyleColor(amountColor);

        Color border = Color.Lerp(BaseBorderColor, eventColor, Mathf.Clamp01(amount));
        SetPanelBorder(border);

        if (_goldIcon != null)
            _goldIcon.style.backgroundColor = new StyleColor(Color.Lerp(new Color(0.94f, 0.68f, 0.16f), eventColor, amount * 0.28f));
    }

    private void SetPanelBorder(Color color)
    {
        if (_goldPanel == null) return;
        _goldPanel.style.borderTopColor = new StyleColor(color);
        _goldPanel.style.borderRightColor = new StyleColor(color);
        _goldPanel.style.borderBottomColor = new StyleColor(color);
        _goldPanel.style.borderLeftColor = new StyleColor(color);
    }

    private void SpawnDelta(int amount)
    {
        if (_deltaLayer == null) return;

        // Cap the active popup count; evict the oldest before adding a new one
        while (_deltaPopups.Count >= 4)
        {
            _deltaPopups[0].Label?.RemoveFromHierarchy();
            _deltaPopups.RemoveAt(0);
        }

        Label label = new(amount > 0 ? $"+{amount}" : amount.ToString());
        label.AddToClassList("gold-delta");
        label.AddToClassList(amount > 0 ? "gold-delta-gain" : "gold-delta-spend");
        label.style.width = DeltaLabelWidth(label.text.Length);
        label.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);
        label.pickingMode = PickingMode.Ignore;

        int stackIndex = _deltaPopups.Count;
        float startLeft = ResolveDeltaStartLeft(label.text.Length, stackIndex);
        float startTop = ResolveDeltaStartTop(stackIndex);
        label.style.left = startLeft;
        label.style.top = startTop;
        label.style.opacity = 0f;
        label.style.scale = new Scale(new Vector3(0.86f, 0.86f, 1f));

        _deltaLayer.Add(label);

        // Alternate left/right drift so stacked popups don't overlap exactly
        _deltaPopups.Add(new DeltaPopup
        {
            Label = label,
            Age = 0f,
            StartLeft = startLeft,
            StartTop = startTop,
            DriftX = (stackIndex % 2 == 0 ? 1f : -1f) * 4f,
        });
    }

    private void UpdateDeltaPopups()
    {
        for (int index = _deltaPopups.Count - 1; index >= 0; index--)
        {
            DeltaPopup popup = _deltaPopups[index];
            popup.Age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(popup.Age / Mathf.Max(0.01f, deltaDuration));
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            if (popup.Label != null)
            {
                // intro: fast ease-in over first 14% of lifetime
                // exit:  fade out over the last 38% of lifetime
                float intro = Mathf.Clamp01(t / 0.14f);
                float exit = Mathf.Clamp01((t - 0.62f) / 0.38f);
                float pop = 1f - Mathf.Pow(1f - intro, 3f);

                popup.Label.style.left = popup.StartLeft + popup.DriftX * ease;
                popup.Label.style.top = popup.StartTop - deltaRise * ease - Mathf.Sin(t * Mathf.PI) * 3f;
                popup.Label.style.opacity = intro * (1f - exit);

                float scale = Mathf.Lerp(0.86f, 1.04f, pop);
                scale = Mathf.Lerp(scale, 0.96f, exit);
                popup.Label.style.scale = new Scale(new Vector3(scale, scale, 1f));
            }

            if (t >= 1f)
            {
                popup.Label?.RemoveFromHierarchy();
                _deltaPopups.RemoveAt(index);
            }
            else
            {
                _deltaPopups[index] = popup;
            }
        }
    }

    #endregion

    #region Layout Helpers

    // Resolves the horizontal start position of a delta popup relative to _deltaLayer,
    // aligning it near the right edge of the gold label when its world bounds are available
    private float ResolveDeltaStartLeft(int textLength, int stackIndex)
    {
        float labelWidth = DeltaLabelWidth(textLength);
        if (_deltaLayer == null || _goldLabel == null || _goldLabel.worldBound.width <= 0f)
            return 126f - labelWidth * 0.5f + stackIndex * 2f;

        Vector2 labelRight = _deltaLayer.WorldToLocal(new Vector2(_goldLabel.worldBound.xMax, _goldLabel.worldBound.center.y));
        return Mathf.Clamp(labelRight.x - labelWidth * 0.72f + stackIndex * 2f, 86f, 136f);
    }

    // Resolves the vertical start position, centred on the gold label when its bounds are known
    private float ResolveDeltaStartTop(int stackIndex)
    {
        if (_deltaLayer == null || _goldLabel == null || _goldLabel.worldBound.height <= 0f)
            return 34f + stackIndex * 5f;

        Vector2 labelCenter = _deltaLayer.WorldToLocal(_goldLabel.worldBound.center);
        return Mathf.Clamp(labelCenter.y - 9f + stackIndex * 5f, 22f, 46f);
    }

    // Width grows with character count so "+1000" and "-99" both fit without clipping
    private static float DeltaLabelWidth(int textLength)
    {
        return Mathf.Clamp(32f + textLength * 7f, 54f, 78f);
    }

    #endregion
}