using Game;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager grid;
    [SerializeField] private Camera gameplayCamera;

    [Header("Configuration")]
    [SerializeField] private LayerMask groundLayer;

    private GameInput _input;
    private Camera _cam;

    #region Unity Lifecycle

    private void Awake()
    {
        _input = new GameInput();

        _cam = gameplayCamera != null
            ? gameplayCamera
            : Camera.main;
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
        if (TryGetGroundHit(out Vector3 worldPos))
        {
            InputEvents.PrimaryPressed(worldPos);
        }
    }

    private void OnPrimaryRelease(InputAction.CallbackContext ctx)
    {
        if (TryGetGroundHit(out Vector3 worldPos))
        {
            InputEvents.PrimaryReleased(worldPos);
        }
    }

    private void OnSecondaryClick(InputAction.CallbackContext ctx)
    {
        if (TryGetGroundHit(out Vector3 worldPos))
        {
            InputEvents.SecondaryPressed(worldPos);
        }
    }

    #endregion

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
            Vector2Int gridCoords = grid != null
                ? grid.WorldToGrid(worldPos)
                : Vector2Int.zero;

            InputEvents.WorldPointerMoved(worldPos, gridCoords);
        }
        else
        {
            InputEvents.WorldPointerLost();
        }
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

    #endregion

}