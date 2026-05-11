using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

// UI Toolkit radial command wheel. ConstructionPresenter builds the entry list and calls ShowEntries.
// This class only handles layout, spawning, visibility, and forwarding selection events back to the presenter.
public class RadialMenu : MonoBehaviour
{
    #region Inspector Fields

    [Header("Dependencies")]
    [SerializeField] private RadialAudio        radialAudio;

    [Header("Layout")]
    [SerializeField] private float outerRadius = 130f;
    [SerializeField] private float innerRadius = 44f;
    #endregion

    #region Runtime State & Events
    private VisualElement _radialRoot;
    private UIDocument _uiDocument;
    private readonly List<Entry> _activeEntries = new();
    private bool _reportedMissingDocument;
    private bool _reportedMissingRootElement;

    public event System.Action<Entry> OnEntrySelected;
    public event System.Action<Entry?> OnEntryHovered;
    public event System.Action OnClosed;

    public bool IsOpen => _radialRoot != null && _radialRoot.style.display == DisplayStyle.Flex;

    public readonly struct Entry
    {
        public readonly string Label;
        public readonly string SubLabel;
        public readonly bool Interactable;
        public readonly object Payload;

        public Entry(string label, string subLabel, bool interactable, object payload)
        {
            Label = label;
            SubLabel = subLabel;
            Interactable = interactable;
            Payload = payload;
        }
    }

    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        EnsureRoot();
        if (_radialRoot != null)
            _radialRoot.style.display = DisplayStyle.None;
    }
    #endregion

    #region Public API
    public void Initialize()
    {
        CacheDocument();
    }

    public void ShowEntries(IReadOnlyList<Entry> entries)
    {
        if (!EnsureRoot()) return;

        _activeEntries.Clear();
        if (entries != null)
            _activeEntries.AddRange(entries);

        ClearButtons();
        SpawnPassiveButtons(_activeEntries);
        Show();
    }

    public void HideImmediate()
    {
        Hide();
    }
    #endregion

    #region Button Spawning

    private void SpawnPassiveButtons(IList<Entry> entries)
    {
        if (_radialRoot?.panel == null) return;

        // Transparent full-screen backdrop catches pointer-down outside any sector to close the menu
        var backdrop = new VisualElement();
        backdrop.style.position = Position.Absolute;
        backdrop.style.left = 0;
        backdrop.style.top = 0;
        backdrop.style.right = 0;
        backdrop.style.bottom = 0;
        backdrop.pickingMode = PickingMode.Position;
        backdrop.RegisterCallback<PointerDownEvent>(_ => RequestClose());
        _radialRoot.Add(backdrop);

        var el = new RadialMenuElement
        {
            OuterRadius = outerRadius,
            InnerRadius = innerRadius,
        };

        var sectors = new List<RadialMenuElement.SectorData>(entries.Count);
        for (int index = 0; index < entries.Count; index++)
        {
            int capturedIndex = index;
            Entry entry = entries[index];
            sectors.Add(new RadialMenuElement.SectorData(entry.Label, entry.SubLabel, entry.Interactable,
                () => OnEntrySelected?.Invoke(_activeEntries[capturedIndex])));
        }

        el.SetSectors(sectors);
        el.OnCenterClicked += RequestClose;
        el.OnHoverChanged += HandlePassiveHoverChanged;
        if (radialAudio != null)
        {
            el.OnHoverChanged += idx => { if (idx >= 0) radialAudio.PlayHover(); };
            el.OnConfirm += _ => radialAudio.PlayClick();
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        mousePos.y = Screen.height - mousePos.y;
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_radialRoot.panel, mousePos);
        float halfSize = el.ElementHalfSize;
        Rect screen = _radialRoot.panel.visualTree.layout;
        Vector2 center = GetSmartMenuCenter(panelPos, halfSize, screen);

        el.style.left = center.x - halfSize;
        el.style.top = center.y - halfSize;

        _radialRoot.Add(el);
    }

    private static Vector2 GetSmartMenuCenter(Vector2 cursorPanelPosition, float halfSize, Rect panelRect)
    {
        const float safeMargin = 10f;

        // Compute the valid range so the menu stays fully within the panel
        float minX = halfSize + safeMargin;
        float minY = halfSize + safeMargin;
        float maxX = Mathf.Max(minX, panelRect.width - halfSize - safeMargin);
        float maxY = Mathf.Max(minY, panelRect.height - halfSize - safeMargin);

        if (panelRect.width <= halfSize * 2f + safeMargin * 2f)
            minX = maxX = panelRect.width * 0.5f;

        if (panelRect.height <= halfSize * 2f + safeMargin * 2f)
            minY = maxY = panelRect.height * 0.5f;

        return new Vector2(
            Mathf.Clamp(cursorPanelPosition.x, minX, maxX),
            Mathf.Clamp(cursorPanelPosition.y, minY, maxY));
    }

    private void ClearButtons() => _radialRoot?.Clear();

    private void HandlePassiveHoverChanged(int sectorIndex)
    {
        if (sectorIndex < 0 || sectorIndex >= _activeEntries.Count)
            OnEntryHovered?.Invoke(null);
        else
            OnEntryHovered?.Invoke(_activeEntries[sectorIndex]);
    }
    #endregion

    #region Visibility

    private void Show()
    {
        if (!EnsureRoot()) return;
        _radialRoot.style.display = DisplayStyle.Flex;
        if (radialAudio != null) radialAudio.PlayOpen();
    }

    private void Hide()
    {
        if (_radialRoot != null)
        {
            if (_radialRoot.style.display == DisplayStyle.Flex && radialAudio != null) radialAudio.PlayClose();
            _radialRoot.style.display = DisplayStyle.None;
        }
        ClearButtons();
        _activeEntries.Clear();
    }

    private bool EnsureRoot()
    {
        if (_radialRoot != null) return true;

        UIDocument doc = CacheDocument();
        if (doc == null)
        {
            if (!_reportedMissingDocument)
            {
                Debug.LogError("[RadialMenu] UIDocument not found on this GameObject.", this);
                _reportedMissingDocument = true;
            }
            return false;
        }

        VisualElement documentRoot = doc.rootVisualElement;
        if (documentRoot == null)
            return false;

        _radialRoot = documentRoot.Q("radial-root");
        if (_radialRoot == null)
        {
            if (!_reportedMissingRootElement)
            {
                Debug.LogError("[RadialMenu] 'radial-root' element not found in UXML.", this);
                _reportedMissingRootElement = true;
            }
            return false;
        }

        return true;
    }

    private UIDocument CacheDocument()
    {
        if (_uiDocument == null)
            _uiDocument = GetComponent<UIDocument>();

        return _uiDocument;
    }

    private void RequestClose()
    {
        Hide();
        OnClosed?.Invoke();
    }
    #endregion
}