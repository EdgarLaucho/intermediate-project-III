using System;
using UnityEngine;

public static class InputEvents
{
    #region Pointer Events
    public static event Action<Vector3, Vector2Int> OnWorldPointerMoved;
    public static event Action OnWorldPointerLost;

    #endregion

    #region Camera Events

    public static event Action<Vector2> OnMoveCamera;
    public static event Action OnToggleCameraFollow;

    #endregion

    #region Button Events

    public static event Action<Vector3> OnPrimaryPressed;
    public static event Action<Vector3> OnPrimaryHeld;
    public static event Action<Vector3> OnPrimaryReleased;
    public static event Action<Vector3> OnSecondaryPressed;
    public static event Action<Vector3> OnTertiaryPressed;
    public static event Action OnCancelPressed;

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