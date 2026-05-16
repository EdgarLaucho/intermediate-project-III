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

    #region Button Events

    public static event Action OnPrimaryPressed; // Left-click / confirm.
    public static event Action OnPrimaryHeld; // Fired every frame left button is held.
    public static event Action OnPrimaryReleased; // Left button released.
    public static event Action OnSecondaryPressed; // Right-click / context menu.
    public static event Action OnTertiaryPressed; // Middle-click / duplicate building.
    public static event Action OnCancelPressed; // Escape / cancel current action.

    #endregion

    #region Raise Helpers

    public static void WorldPointerMoved(Vector3 worldPos, Vector2Int gridCoords)
        => OnWorldPointerMoved?.Invoke(worldPos, gridCoords);

    public static void WorldPointerLost() => OnWorldPointerLost?.Invoke();
    public static void PrimaryPressed() => OnPrimaryPressed?.Invoke();
    public static void PrimaryHeld() => OnPrimaryHeld?.Invoke();
    public static void PrimaryReleased() => OnPrimaryReleased?.Invoke();
    public static void SecondaryPressed() => OnSecondaryPressed?.Invoke();
    public static void TertiaryPressed() => OnTertiaryPressed?.Invoke();
    public static void CancelPressed() => OnCancelPressed?.Invoke();

    #endregion
}