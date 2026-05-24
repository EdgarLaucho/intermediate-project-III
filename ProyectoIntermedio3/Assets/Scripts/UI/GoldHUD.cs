using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class GoldHUD : MonoBehaviour
{
    [Header("Feel")]
    [SerializeField] private float valueAnimationDuration = 0.38f;
    [SerializeField] private float pulseDuration = 0.28f;
    [SerializeField] private float deltaDuration = 0.74f;
    [SerializeField] private float deltaRise = 26f;
    [SerializeField] private int lowGoldThreshold = 30;

    private static readonly Color NormalAmountColor = new(1.00f, 0.87f, 0.34f, 1f);
    private static readonly Color GainColor = new(0.55f, 1.00f, 0.38f, 1f);
    private static readonly Color SpendColor = new(1.00f, 0.45f, 0.28f, 1f);
    private static readonly Color LowGoldColor = new(1.00f, 0.58f, 0.22f, 1f);
    private static readonly Color BaseBorderColor = new(1.00f, 0.76f, 0.25f, 0.68f);
    private static readonly Color TitleColor = new(1f, 0.875f, 0.56f, 0.74f);
    private static readonly Color IconBaseColor = new(0.94f, 0.68f, 0.16f);

    private readonly List<DeltaPopup> _deltaPopups = new();
    private VisualElement _goldPanel;
    private VisualElement _goldIcon;
    private VisualElement _deltaLayer;
    private Label _goldLabel;
    private Label _goldTitle;
    private UIDocument _uiDocument;
    private int _targetGold;
    private int _animationStartGold;
    private int _lastDelta;
    private float _displayedGold;
    private float _valueAnimationTime;
    private float _pulseTime = 999f;
    private bool _hasValue;
    private bool _uiReady;
    private bool _subscribed;
    private bool _reportedMissingDocument;
    private bool _reportedMissingGoldLabel;

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

    private bool TryInitializeUI()
    {
        if (_uiReady) return true;

        var document = CacheDocument();
        if (document == null)
        {
            if (!_reportedMissingDocument)
            {
                Debug.LogError("[GoldHUD] UIDocument not found on this GameObject.", this);
                _reportedMissingDocument = true;
            }

            return false;
        }

        var root = document.rootVisualElement;
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

        SetCenteredOrigin(_goldPanel);
        SetCenteredOrigin(_goldIcon);

        _uiReady = true;
        Subscribe();
        return true;
    }

    private UIDocument CacheDocument()
    {
        if (_uiDocument == null)
            _uiDocument = GetComponent<UIDocument>();

        return _uiDocument;
    }

    private void Subscribe()
    {
        if (_subscribed) return;

        EconomyEvents.OnGoldChanged += HandleGoldChanged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;

        EconomyEvents.OnGoldChanged -= HandleGoldChanged;
        _subscribed = false;
    }

    private static void SetCenteredOrigin(VisualElement element)
    {
        if (element != null)
            element.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);
    }

    private void HandleGoldChanged(int gold)
    {
        var previousGold = _hasValue ? _targetGold : gold;
        var delta = gold - previousGold;

        _animationStartGold = Mathf.RoundToInt(_displayedGold);
        _targetGold = gold;
        _valueAnimationTime = 0f;
        _lastDelta = delta;
        _pulseTime = 0f;
        _hasValue = true;

        if (delta != 0)
            SpawnDelta(delta);
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
        var duration = Mathf.Max(0.01f, valueAnimationDuration);
        var t = Mathf.Clamp01(_valueAnimationTime / duration);
        var ease = EaseOutCubic(t);

        _displayedGold = Mathf.Lerp(_animationStartGold, _targetGold, ease);
        SetDisplayedGold(Mathf.RoundToInt(_displayedGold));

        if (t >= 1f)
            _displayedGold = _targetGold;
    }

    private void UpdatePulse()
    {
        if (_pulseTime > pulseDuration)
        {
            SetScale(_goldPanel, Vector3.one);
            SetScale(_goldIcon, Vector3.one);
            ApplyEventColor(0f, 0);
            return;
        }

        _pulseTime += Time.unscaledDeltaTime;
        var t = Mathf.Clamp01(_pulseTime / Mathf.Max(0.01f, pulseDuration));
        var punch = Mathf.Sin(t * Mathf.PI);
        var panelScale = 1f + punch * (_lastDelta >= 0 ? 0.035f : 0.024f);
        var iconScale = 1f + punch * (_lastDelta >= 0 ? 0.13f : 0.08f);

        SetScale(_goldPanel, new Vector3(panelScale, panelScale, 1f));
        SetScale(_goldIcon, new Vector3(iconScale, iconScale, 1f));
        ApplyEventColor(punch, _lastDelta);
    }

    private void SetDisplayedGold(int gold)
    {
        if (_goldLabel != null)
            _goldLabel.text = gold.ToString();

        if (_goldTitle != null)
            _goldTitle.style.color = new StyleColor(gold <= lowGoldThreshold ? LowGoldColor : TitleColor);
    }

    private void ApplyEventColor(float amount, int delta)
    {
        var eventColor = delta > 0 ? GainColor : delta < 0 ? SpendColor : BaseBorderColor;
        var amountColor = _targetGold <= lowGoldThreshold ? LowGoldColor : NormalAmountColor;
        amountColor = Color.Lerp(amountColor, eventColor, Mathf.Clamp01(amount));

        if (_goldLabel != null)
            _goldLabel.style.color = new StyleColor(amountColor);

        SetPanelBorder(Color.Lerp(BaseBorderColor, eventColor, Mathf.Clamp01(amount)));

        if (_goldIcon != null)
            _goldIcon.style.backgroundColor = new StyleColor(Color.Lerp(IconBaseColor, eventColor, amount * 0.28f));
    }

    private void SetPanelBorder(Color color)
    {
        if (_goldPanel == null) return;

        var styleColor = new StyleColor(color);
        _goldPanel.style.borderTopColor = styleColor;
        _goldPanel.style.borderRightColor = styleColor;
        _goldPanel.style.borderBottomColor = styleColor;
        _goldPanel.style.borderLeftColor = styleColor;
    }

    private void SpawnDelta(int amount)
    {
        if (_deltaLayer == null) return;

        while (_deltaPopups.Count >= 4)
            RemovePopupAt(0);

        var label = new Label(amount > 0 ? $"+{amount}" : amount.ToString());
        label.AddToClassList("gold-delta");
        label.AddToClassList(amount > 0 ? "gold-delta-gain" : "gold-delta-spend");
        label.style.width = DeltaLabelWidth(label.text.Length);
        label.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0);
        label.pickingMode = PickingMode.Ignore;

        var stackIndex = _deltaPopups.Count;
        var startLeft = ResolveDeltaStartLeft(label.text.Length, stackIndex);
        var startTop = ResolveDeltaStartTop(stackIndex);
        label.style.left = startLeft;
        label.style.top = startTop;
        label.style.opacity = 0f;
        label.style.scale = new Scale(new Vector3(0.86f, 0.86f, 1f));

        _deltaLayer.Add(label);
        _deltaPopups.Add(new DeltaPopup(label, startLeft, startTop, (stackIndex % 2 == 0 ? 1f : -1f) * 4f));
    }

    private void UpdateDeltaPopups()
    {
        for (var index = _deltaPopups.Count - 1; index >= 0; index--)
        {
            var popup = _deltaPopups[index];
            popup.Age += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(popup.Age / Mathf.Max(0.01f, deltaDuration));

            if (popup.Label != null)
                AnimatePopup(popup, t);

            if (t >= 1f)
                RemovePopupAt(index);
        }
    }

    private void AnimatePopup(DeltaPopup popup, float t)
    {
        var ease = EaseOutCubic(t);
        var intro = Mathf.Clamp01(t / 0.14f);
        var exit = Mathf.Clamp01((t - 0.62f) / 0.38f);
        var pop = EaseOutCubic(intro);
        var scale = Mathf.Lerp(Mathf.Lerp(0.86f, 1.04f, pop), 0.96f, exit);

        popup.Label.style.left = popup.StartLeft + popup.DriftX * ease;
        popup.Label.style.top = popup.StartTop - deltaRise * ease - Mathf.Sin(t * Mathf.PI) * 3f;
        popup.Label.style.opacity = intro * (1f - exit);
        popup.Label.style.scale = new Scale(new Vector3(scale, scale, 1f));
    }

    private void RemovePopupAt(int index)
    {
        _deltaPopups[index].Label?.RemoveFromHierarchy();
        _deltaPopups.RemoveAt(index);
    }

    private float ResolveDeltaStartLeft(int textLength, int stackIndex)
    {
        var labelWidth = DeltaLabelWidth(textLength);
        if (_deltaLayer == null || _goldLabel == null || _goldLabel.worldBound.width <= 0f)
            return 126f - labelWidth * 0.5f + stackIndex * 2f;

        var labelRight = _deltaLayer.WorldToLocal(new Vector2(_goldLabel.worldBound.xMax, _goldLabel.worldBound.center.y));
        return Mathf.Clamp(labelRight.x - labelWidth * 0.72f + stackIndex * 2f, 86f, 136f);
    }

    private float ResolveDeltaStartTop(int stackIndex)
    {
        if (_deltaLayer == null || _goldLabel == null || _goldLabel.worldBound.height <= 0f)
            return 34f + stackIndex * 5f;

        var labelCenter = _deltaLayer.WorldToLocal(_goldLabel.worldBound.center);
        return Mathf.Clamp(labelCenter.y - 9f + stackIndex * 5f, 22f, 46f);
    }

    private static float DeltaLabelWidth(int textLength)
    {
        return Mathf.Clamp(32f + textLength * 7f, 54f, 78f);
    }

    private static void SetScale(VisualElement element, Vector3 scale)
    {
        if (element != null)
            element.style.scale = new Scale(scale);
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private class DeltaPopup
    {
        public readonly Label Label;
        public readonly float StartLeft;
        public readonly float StartTop;
        public readonly float DriftX;
        public float Age;

        public DeltaPopup(Label label, float startLeft, float startTop, float driftX)
        {
            Label = label;
            StartLeft = startLeft;
            StartTop = startTop;
            DriftX = driftX;
        }
    }
}
