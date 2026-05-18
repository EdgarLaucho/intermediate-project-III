using System;
using UnityEngine;

// Global gameplay-level events.
// Used for high-level player intentions such as movement,
// build requests, interaction requests, etc.
public static class GameplayEvents
{
    #region Movement Events

    // Fired when the player issues a movement command.
    public static event Action<Vector3> OnMoveCommandIssued;

    #endregion

    #region Construction Events

    // Fired when the player requests to build something.
    // This does NOT mean the building was constructed yet.
    public static event Action<Vector2Int, BuildingData> OnBuildRequested;

    #endregion

    #region Raise Helpers

    public static void MoveCommandIssued(Vector3 targetPosition)
        => OnMoveCommandIssued?.Invoke(targetPosition);
    public static void BuildRequested(Vector2Int coords, BuildingData data)
        => OnBuildRequested?.Invoke(coords, data);

    #endregion
}