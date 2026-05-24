using Game;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("References")]
    private GridManager grid;
    [SerializeField] private Camera gameplayCamera;

    [Header("Configuration")]
    [SerializeField] private LayerMask groundLayer;

    private GameInput _input;
    private Camera _cam;
    private bool _primaryStartedOnWorld;
    private bool _primaryStartedOverUi;
    private bool _hasLastWorldPos;
    private Vector3 _lastWorldPos;
    private static readonly List<RaycastResult> UiRaycastResults = new();

    #region Unity Lifecycle

    private void Awake()
    {
        _input = new GameInput();

        _cam = gameplayCamera != null
            ? gameplayCamera
            : Camera.main;

        grid = FindAnyObjectByType<GridManager>(FindObjectsInactive.Exclude);
    }

    private void OnEnable()
    {
        _input.Enable();

        RegisterGameplayInputs();
    }

    private void OnDisable()
    {
        UnregisterGameplayInputs();

        _input.Disable();
    }

    private void Update()
    {
        HandlePointerMovement();
        HandleTertiaryClick();
        HandlePrimaryHeld();
    }

    #endregion

    #region Input Registration

    private void RegisterGameplayInputs()
    {
        _input.Gameplay.MoveCamera.performed += OnMoveCamera;
        _input.Gameplay.MoveCamera.canceled += OnMoveCamera;

        _input.Gameplay.ToggleCameraFollow.performed += OnToggleCameraFollow;

        _input.Gameplay.PrimaryClick.performed += OnPrimaryClick;
        _input.Gameplay.PrimaryClick.canceled += OnPrimaryRelease;

        _input.Gameplay.SecondaryClick.performed += OnSecondaryClick;

        _input.Gameplay.Cancel.performed += OnCancel;
    }

    private void UnregisterGameplayInputs()
    {
        _input.Gameplay.MoveCamera.performed -= OnMoveCamera;
        _input.Gameplay.MoveCamera.canceled -= OnMoveCamera;

        _input.Gameplay.ToggleCameraFollow.performed -= OnToggleCameraFollow;

        _input.Gameplay.PrimaryClick.performed -= OnPrimaryClick;
        _input.Gameplay.PrimaryClick.canceled -= OnPrimaryRelease;

        _input.Gameplay.SecondaryClick.performed -= OnSecondaryClick;

        _input.Gameplay.Cancel.performed -= OnCancel;
    }

    #endregion

    #region Camera Input

    private void OnMoveCamera(InputAction.CallbackContext ctx)
    {
        Vector2 moveDirection = ctx.ReadValue<Vector2>();

        InputEvents.MoveCamera(moveDirection);
    }

    private void OnToggleCameraFollow(InputAction.CallbackContext ctx)
    {
        InputEvents.ToggleCameraFollow();
    }

    #endregion

    #region Mouse Buttons

    private void OnPrimaryClick(InputAction.CallbackContext ctx)
    {
        _primaryStartedOnWorld = false;
        _primaryStartedOverUi = IsPointerOverUi();

        if (_primaryStartedOverUi)
            return;

        if (TryGetGroundHit(out Vector3 worldPos))
        {
            _primaryStartedOnWorld = true;
            InputEvents.PrimaryPressed(worldPos);
        }
    }

    private void OnPrimaryRelease(InputAction.CallbackContext ctx)
    {
        if (_primaryStartedOverUi)
        {
            ResetPrimaryPressState();
            return;
        }

        if (_primaryStartedOnWorld)
            InputEvents.PrimaryReleased(GetCurrentOrLastWorldPos());

        ResetPrimaryPressState();
    }

    private void OnSecondaryClick(InputAction.CallbackContext ctx)
    {
        if (IsPointerOverUi())
            return;

        if (TryGetGroundHit(out Vector3 worldPos))
        {
            InputEvents.SecondaryPressed(worldPos);
        }
    }

    #endregion

    private void HandleTertiaryClick()
    {
        if (Mouse.current == null || !Mouse.current.middleButton.wasPressedThisFrame)
            return;

        if (IsPointerOverUi())
            return;

        if (TryGetGroundHit(out Vector3 worldPos))
            InputEvents.TertiaryPressed(worldPos);
    }

    #region General Input

    private void OnCancel(InputAction.CallbackContext ctx)
    {
        InputEvents.CancelPressed();
    }

    #endregion

    #region Pointer Logic

    private void HandlePointerMovement()
    {
        if (TryGetGroundHit(out Vector3 worldPos))
        {
            _hasLastWorldPos = true;
            _lastWorldPos = worldPos;

            Vector2Int gridCoords = grid != null
                ? grid.WorldToGrid(worldPos)
                : Vector2Int.zero;

            InputEvents.WorldPointerMoved(worldPos, gridCoords);
        }
        else
        {
            _hasLastWorldPos = false;
            InputEvents.WorldPointerLost();
        }
    }

    private void HandlePrimaryHeld()
    {
        if (!_primaryStartedOnWorld || Mouse.current == null || !Mouse.current.leftButton.isPressed)
            return;

        if (TryGetGroundHit(out Vector3 worldPos))
            InputEvents.PrimaryHeld(worldPos);
    }

    private Vector3 GetCurrentOrLastWorldPos()
    {
        if (TryGetGroundHit(out Vector3 worldPos))
            return worldPos;

        return _hasLastWorldPos ? _lastWorldPos : Vector3.zero;
    }

    private void ResetPrimaryPressState()
    {
        _primaryStartedOnWorld = false;
        _primaryStartedOverUi = false;
    }

    private bool TryGetGroundHit(out Vector3 worldPos)
    {
        if (_cam == null || Mouse.current == null)
        {
            worldPos = Vector3.zero;
            return false;
        }

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        float gridPlaneY = grid != null
            ? grid.GridToWorld(Vector2Int.zero).y
            : 0f;

        if (Mathf.Abs(ray.direction.y) > 0.0001f)
        {
            float t = (gridPlaneY - ray.origin.y) / ray.direction.y;

            if (t > 0f)
            {
                worldPos = ray.origin + ray.direction * t;
                return true;
            }
        }

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayer))
        {
            worldPos = hit.point;
            return true;
        }

        worldPos = Vector3.zero;
        return false;
    }

    private static bool IsPointerOverUi()
    {
        return IsPointerOverToolkitUi() || IsPointerOverLegacyUi();
    }

    private static bool IsPointerOverToolkitUi()
    {
        if (Mouse.current == null)
            return false;

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Vector2 panelPosition = new(screenPosition.x, Screen.height - screenPosition.y);

        foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
        {
            if (document == null || !document.isActiveAndEnabled)
                continue;

            VisualElement root = document.rootVisualElement;
            if (root?.panel == null || root.resolvedStyle.display == DisplayStyle.None)
                continue;

            Vector2 localPanelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, panelPosition);
            VisualElement picked = root.panel.Pick(localPanelPosition);

            if (IsBlockingToolkitElement(picked))
                return true;
        }

        return false;
    }

    private static bool IsBlockingToolkitElement(VisualElement element)
    {
        for (VisualElement current = element; current != null; current = current.parent)
        {
            if (current.resolvedStyle.display == DisplayStyle.None ||
                current.resolvedStyle.visibility == Visibility.Hidden)
                return false;

            if (current is Button || current is Toggle || current is Slider || current is TextField)
                return true;

            if (current.name == "radial-root" && current.resolvedStyle.display == DisplayStyle.Flex)
                return true;

            if (current.ClassListContains("screen-panel") &&
                current.resolvedStyle.display == DisplayStyle.Flex)
                return true;

            if (current.ClassListContains("radial-entry-node") ||
                current.ClassListContains("radial-center-button") ||
                current.ClassListContains("radial-backdrop"))
                return true;
        }

        return false;
    }

    private static bool IsPointerOverLegacyUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || Mouse.current == null)
            return false;

        PointerEventData pointerData = new(eventSystem)
        {
            position = Mouse.current.position.ReadValue()
        };

        UiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerData, UiRaycastResults);

        foreach (RaycastResult result in UiRaycastResults)
        {
            if (result.module != null && result.module.GetType().Name == "PanelRaycaster")
                continue;

            if (result.gameObject != null && result.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    #endregion

}
