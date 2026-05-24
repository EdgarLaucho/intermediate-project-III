using System;
using UnityEngine;

public static class GameplayEvents
{
    #region Movement Events

    public static event Action<Vector3> OnMoveCommandIssued;

    #endregion

    #region Construction Events
    public static event Action<Vector2Int, BuildingData> OnBuildRequested;

    #endregion

    #region Raise Helpers

    public static void MoveCommandIssued(Vector3 targetPosition) => OnMoveCommandIssued?.Invoke(targetPosition);
    public static void BuildRequested(Vector2Int coords, BuildingData data) => OnBuildRequested?.Invoke(coords, data);

    #endregion
}