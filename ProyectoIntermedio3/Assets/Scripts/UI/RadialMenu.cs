using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class RadialMenu : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RadialAudio radialAudio;

    [Header("Layout")]
    [SerializeField] private float outerRadius = 130f;
    [SerializeField] private float innerRadius = 44f;

    private VisualElement _radialRoot;
    private UIDocument _uiDocument;
    private readonly List<Entry> _activeEntries = new();
    private bool _reportedMissingDocument;
    private bool _reportedMissingRootElement;

    public event System.Action<Entry> OnEntrySelected;
    public event System.Action<Entry?> OnEntryHovered;
    public event System.Action OnClosed;

    public bool IsOpen => _radialRoot != null && _radialRoot.style.display == DisplayStyle.Flex;

    private void Start()
    {
        EnsureRoot();
        if (_radialRoot != null)
            _radialRoot.style.display = DisplayStyle.None;
    }

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
        CreateButtons(_activeEntries);
        Show();
    }

    public void HideImmediate()
    {
        Hide();
    }

    private void CreateButtons(IList<Entry> entries)
    {
        if (_radialRoot?.panel == null) return;

        var backdrop = new VisualElement();
        backdrop.style.position = Position.Absolute;
        backdrop.style.left = 0;
        backdrop.style.top = 0;
        backdrop.style.right = 0;
        backdrop.style.bottom = 0;
        backdrop.pickingMode = PickingMode.Position;
        backdrop.RegisterCallback<PointerDownEvent>(_ => RequestClose());
        _radialRoot.Add(backdrop);

        var element = new RadialMenuElement
        {
            OuterRadius = outerRadius,
            InnerRadius = innerRadius,
        };

        var sectors = new List<RadialMenuElement.SectorData>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            var capturedIndex = index;
            var entry = entries[index];
            sectors.Add(new RadialMenuElement.SectorData(entry.Label, entry.SubLabel, entry.Interactable,
                () => OnEntrySelected?.Invoke(_activeEntries[capturedIndex])));
        }

        element.SetSectors(sectors);
        element.OnCenterClicked += RequestClose;
        element.OnHoverChanged += HandleHoverChanged;
        if (radialAudio != null)
        {
            element.OnHoverChanged += index => { if (index >= 0) radialAudio.PlayHover(); };
            element.OnConfirm += _ => radialAudio.PlayClick();
        }

        var mousePosition = Mouse.current.position.ReadValue();
        mousePosition.y = Screen.height - mousePosition.y;
        var panelPosition = RuntimePanelUtils.ScreenToPanel(_radialRoot.panel, mousePosition);
        var halfSize = element.ElementHalfSize;
        var screen = _radialRoot.panel.visualTree.layout;
        var center = GetMenuCenter(panelPosition, halfSize, screen);

        element.style.left = center.x - halfSize;
        element.style.top = center.y - halfSize;
        _radialRoot.Add(element);
    }

    private static Vector2 GetMenuCenter(Vector2 cursorPanelPosition, float halfSize, Rect panelRect)
    {
        const float safeMargin = 10f;

        var minX = halfSize + safeMargin;
        var minY = halfSize + safeMargin;
        var maxX = Mathf.Max(minX, panelRect.width - halfSize - safeMargin);
        var maxY = Mathf.Max(minY, panelRect.height - halfSize - safeMargin);

        if (panelRect.width <= halfSize * 2f + safeMargin * 2f)
            minX = maxX = panelRect.width * 0.5f;

        if (panelRect.height <= halfSize * 2f + safeMargin * 2f)
            minY = maxY = panelRect.height * 0.5f;

        return new Vector2(
            Mathf.Clamp(cursorPanelPosition.x, minX, maxX),
            Mathf.Clamp(cursorPanelPosition.y, minY, maxY));
    }

    private void ClearButtons()
    {
        _radialRoot?.Clear();
    }

    private void HandleHoverChanged(int sectorIndex)
    {
        OnEntryHovered?.Invoke(sectorIndex < 0 || sectorIndex >= _activeEntries.Count ? null : _activeEntries[sectorIndex]);
    }

    private void Show()
    {
        if (!EnsureRoot()) return;

        _radialRoot.style.display = DisplayStyle.Flex;
        if (radialAudio != null)
            radialAudio.PlayOpen();
    }

    private void Hide()
    {
        if (_radialRoot != null)
        {
            if (_radialRoot.style.display == DisplayStyle.Flex && radialAudio != null)
                radialAudio.PlayClose();

            _radialRoot.style.display = DisplayStyle.None;
        }

        ClearButtons();
        _activeEntries.Clear();
    }

    private bool EnsureRoot()
    {
        if (_radialRoot != null) return true;

        var document = CacheDocument();
        if (document == null)
        {
            if (!_reportedMissingDocument)
            {
                Debug.LogError("[RadialMenu] UIDocument not found on this GameObject.", this);
                _reportedMissingDocument = true;
            }

            return false;
        }

        var documentRoot = document.rootVisualElement;
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
}