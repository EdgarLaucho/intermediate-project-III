using UnityEngine;
using UnityEngine.UIElements;

// World-space health bar for buildings, driven by a UI Toolkit UIDocument.
// Binds to IDamageable on the parent, reacts to OnHealthChanged, and optionally
// faces the camera and scales with viewing distance for consistent readability.
public class BuildingHealthBar : MonoBehaviour
{
    #region Inspector Fields
    [Header("Visibility")]
    [SerializeField] private bool hideWhenFullHealth = true;
    [SerializeField] private float fullHealthLingerTime = 0.65f;

    [Header("Camera Facing")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool keepUpright = false;
    [SerializeField] private bool scaleWithDistance = true;
    [SerializeField] private float referenceDistance = 10f;
    [SerializeField] private float minScale = 0.85f;
    [SerializeField] private float maxScale = 1.05f;

    #endregion

    #region Runtime State

    private IDamageable _building;
    private VisualElement _root;
    private VisualElement _fill;
    private UIDocument _uiDocument;
    private Camera _cachedCamera;
    private Vector3 _baseScale;
    private float _showUntilTime;
    private bool _uiReady;
    private bool _reportedMissingDocument;
    private bool _reportedMissingRoot;
    private bool _reportedMissingFill;
    private int _lastHealth = int.MinValue;
    private int _lastMaxHealth = int.MinValue;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        _baseScale = transform.localScale;
        _building = GetComponentInParent<IDamageable>();
        if (_building == null)
        {
            Debug.LogError("[BuildingHealthBar] No IDamageable found in parent hierarchy.");
            return;
        }

        _building.OnHealthChanged += HandleHealthChanged;
        TryInitializeUI();
    }

    private void Update()
    {
        if (!_uiReady)
            TryInitializeUI();

        RefreshIfHealthChanged();
        RefreshVisibility();
    }

    private void LateUpdate()
    {
        if (!_uiReady || !IsVisible()) return;

        FaceTargetCamera();
        ApplyDistanceScale();
    }

    private void OnDestroy()
    {
        if (_building != null) _building.OnHealthChanged -= HandleHealthChanged;
    }

    #endregion

    #region Display

    private void HandleHealthChanged()
    {
        // Extend the show window so the bar lingers briefly even when health returns to full
        _showUntilTime = Time.unscaledTime + Mathf.Max(0f, fullHealthLingerTime);
        Refresh();
    }

    private void Refresh()
    {
        if (_building == null || !_uiReady) return;

        float ratio = _building.MaxHealth > 0
            ? Mathf.Clamp01((float)_building.CurrentHealth / _building.MaxHealth)
            : 0f;

        _lastHealth = _building.CurrentHealth;
        _lastMaxHealth = _building.MaxHealth;

        if (_fill != null)
        {
            _fill.style.width = Length.Percent(ratio * 100f);
            _fill.style.backgroundColor = new StyleColor(ColorForRatio(ratio));
        }

        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (_building == null || !_uiReady || _root == null) return;

        bool isDamaged = _building.CurrentHealth < _building.MaxHealth;
        bool shouldShow = _building.IsAlive && (!hideWhenFullHealth || isDamaged || Time.unscaledTime < _showUntilTime);
        _root.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private bool IsVisible()
    {
        return _root != null && _root.style.display != DisplayStyle.None;
    }

    private bool TryInitializeUI()
    {
        if (_uiReady) return true;

        // Lazy-initialize: UIDocument component may not be ready until the first frame
        UIDocument doc = CacheDocument();
        if (doc == null)
        {
            if (!_reportedMissingDocument)
            {
                Debug.LogError("[BuildingHealthBar] UIDocument not found on this GameObject.", this);
                _reportedMissingDocument = true;
            }
            return false;
        }

        VisualElement root = doc.rootVisualElement;
        if (root == null)
            return false;

        _root = root.Q("health-bar-root");
        if (_root == null)
        {
            if (!_reportedMissingRoot)
            {
                Debug.LogError("[BuildingHealthBar] 'health-bar-root' element not found in UXML.", this);
                _reportedMissingRoot = true;
            }
            return false;
        }

        _fill = root.Q("health-bar-fill");
        if (_fill == null)
        {
            if (!_reportedMissingFill)
            {
                Debug.LogError("[BuildingHealthBar] 'health-bar-fill' element not found in UXML.", this);
                _reportedMissingFill = true;
            }
            return false;
        }

        _uiReady = true;
        Refresh();
        return true;
    }

    #endregion

    #region Camera & Scale
    private void FaceTargetCamera()    {
        if (!faceCamera) return;

        Camera cameraToUse = ResolveCamera();
        if (cameraToUse == null) return;

        Vector3 forward = transform.position - cameraToUse.transform.position;
        if (forward.sqrMagnitude <= 0.0001f) return;

        Vector3 normalizedForward = forward.normalized;
        Vector3 up = keepUpright ? Vector3.up : cameraToUse.transform.up;
        // Avoid gimbal lock when forward is nearly parallel to the up vector
        if (Mathf.Abs(Vector3.Dot(normalizedForward, up)) > 0.98f)
            up = cameraToUse.transform.up;

        transform.rotation = Quaternion.LookRotation(normalizedForward, up);
    }

    private void RefreshIfHealthChanged()
    {
        if (_building == null || !_uiReady) return;
        if (_building.CurrentHealth == _lastHealth && _building.MaxHealth == _lastMaxHealth) return;

        Refresh();
    }

    private void ApplyDistanceScale()
    {
        if (!scaleWithDistance) return;

        Camera cameraToUse = ResolveCamera();
        if (cameraToUse == null) return;

        float distance = Vector3.Distance(cameraToUse.transform.position, transform.position);
        float reference = Mathf.Max(0.1f, referenceDistance);
        float scale = Mathf.Clamp(distance / reference, minScale, maxScale);
        transform.localScale = _baseScale * scale;
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null) return targetCamera;
        if (_cachedCamera == null) _cachedCamera = Camera.main;
        return _cachedCamera;
    }

    #endregion

    #region Helpers

    // Green → yellow → red based on health ratio thresholds
    private static Color ColorForRatio(float ratio)
    {
        if (ratio > 0.55f) return new Color(0.18f, 0.95f, 0.34f);
        if (ratio > 0.28f) return new Color(1.0f, 0.76f, 0.16f);
        return new Color(1.0f, 0.2f, 0.16f);
    }

    private UIDocument CacheDocument()
    {
        if (_uiDocument == null)
            _uiDocument = GetComponent<UIDocument>();

        return _uiDocument;
    }

    #endregion
}