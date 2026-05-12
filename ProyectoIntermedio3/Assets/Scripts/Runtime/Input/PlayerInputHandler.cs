using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private GridManager grid;
    [SerializeField] private Camera gameplayCamera;

    [Header("Configuration")]
    [SerializeField] private LayerMask groundLayer;

    private Camera _cam;

    private void Awake()
    {
        _cam = gameplayCamera != null ? gameplayCamera : Camera.main;
    }

    private void Update()
    {
        HandleCancelInput();
        HandlePointerInput();
        HandleClickInput();
    }

    private void HandleCancelInput()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            InputEvents.CancelPressed();
    }

    private void HandlePointerInput()
    {
        if (TryGetGroundHit(out Vector3 worldPos))
        {
            Vector2Int gridCoords = grid != null ? grid.WorldToGrid(worldPos) : Vector2Int.zero;
            InputEvents.WorldPointerMoved(worldPos, gridCoords);
        }
        else
        {
            InputEvents.WorldPointerLost();
        }
    }

    private void HandleClickInput()
    {
        if (Mouse.current == null) return;
        if (Mouse.current.leftButton.wasPressedThisFrame) InputEvents.PrimaryPressed();
        if (Mouse.current.leftButton.isPressed) InputEvents.PrimaryHeld();
        if (Mouse.current.leftButton.wasReleasedThisFrame) InputEvents.PrimaryReleased();
        if (Mouse.current.rightButton.wasPressedThisFrame) InputEvents.SecondaryPressed();
        if (Mouse.current.middleButton.wasPressedThisFrame) InputEvents.TertiaryPressed();
    }

    private bool TryGetGroundHit(out Vector3 worldPos)
    {
        if (_cam == null || Mouse.current == null)
        {
            worldPos = Vector3.zero;
            return false;
        }

        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        float gridPlaneY = grid != null ? grid.GridToWorld(Vector2Int.zero).y : 0f;
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
}