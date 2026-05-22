using System;
using UnityEngine;

// Decouples the input layer from every system that needs to react to it.
// The input provider (e.g. PlayerInputHandler) calls the static raise helpers;
// consumers subscribe to the events without knowing where input comes from.
public static class InputEvents
{
    #region Pointer Events

    // Fired every frame the world-space cursor is over a valid surface.
    // worldPos is the raw hit point; gridCoords is the snapped cell index.
    public static event Action<Vector3, Vector2Int> OnWorldPointerMoved;

    // Fired when the cursor leaves the valid surface (e.g. moves off the grid).
    public static event Action OnWorldPointerLost;

    #endregion

    #region Camera Events

    public static event Action<Vector2> OnMoveCamera; // WASD / arrow keys camera movement.
    public static event Action OnToggleCameraFollow; // Y / Toggle camera follow mode.

    #endregion

    #region Button Events

    public static event Action<Vector3> OnPrimaryPressed; // Left-click / confirm.
    public static event Action<Vector3> OnPrimaryHeld; // Fired every frame left button is held.
    public static event Action<Vector3> OnPrimaryReleased; // Left button released.
    public static event Action<Vector3> OnSecondaryPressed; // Right-click / context menu.
    public static event Action<Vector3> OnTertiaryPressed; // Middle-click / duplicate building.
    public static event Action OnCancelPressed; // Escape / cancel current action.

    #endregion

    #region Raise Helpers

    public static void WorldPointerMoved(Vector3 worldPos, Vector2Int gridCoords)
        => OnWorldPointerMoved?.Invoke(worldPos, gridCoords);
    public static void WorldPointerLost() => OnWorldPointerLost?.Invoke();

    public static void MoveCamera(Vector2 direction) => OnMoveCamera?.Invoke(direction);
    public static void ToggleCameraFollow() => OnToggleCameraFollow?.Invoke();

    public static void PrimaryPressed(Vector3 worldPos) => OnPrimaryPressed?.Invoke(worldPos);
    public static void PrimaryHeld(Vector3 worldPos) => OnPrimaryHeld?.Invoke(worldPos);
    public static void PrimaryReleased(Vector3 worldPos) => OnPrimaryReleased?.Invoke(worldPos);
    public static void SecondaryPressed(Vector3 worldPos) => OnSecondaryPressed?.Invoke(worldPos);
    public static void TertiaryPressed(Vector3 worldPos) => OnTertiaryPressed?.Invoke(worldPos);
    public static void CancelPressed() => OnCancelPressed?.Invoke();

    #endregion
}