using System;
using System.Collections.Generic;
using UnityEngine;

// All events and payload types related to building placement, selection, and management.
// Using a single static bus keeps BuildManager, ConstructionPresenter, BuildPreview,
// and UI panels independent — none need direct references to each other.

// ---------------------------------------------------------------------------
// Payload structs
// Readonly structs are used here instead of classes to avoid heap allocations
// on every event fire (they’re raised every pointer-moved frame during placement).
// ---------------------------------------------------------------------------

public readonly struct CellHoveredArgs
{
    public readonly Vector2Int Coords;
    public readonly GridCell Cell;
    public readonly Vector3 WorldPos;
    public readonly float CellSize;
    // Null when hovering in Idle state (no building selected); non-null during placement
    // so listeners can colour the highlight based on placement validity.
    public readonly BuildManager.PlacementValidation? Validation;

    public CellHoveredArgs(Vector2Int coords, GridCell cell, Vector3 worldPos, float cellSize,
                           BuildManager.PlacementValidation? validation)
    {
        Coords = coords;
        Cell = cell;
        WorldPos = worldPos;
        CellSize = cellSize;
        Validation = validation;
    }
}

public readonly struct PlacementUpdatedArgs
{
    public readonly Vector2Int Coords;
    public readonly Vector3 WorldPos;
    public readonly BuildManager.PlacementValidation Validation;
    public readonly GridCell Cell;

    public PlacementUpdatedArgs(Vector2Int coords, Vector3 worldPos,
                                BuildManager.PlacementValidation validation, GridCell cell)
    {
        Coords = coords;
        WorldPos = worldPos;
        Validation = validation;
        Cell = cell;
    }
}

// Shared payload for placed / repaired / upgraded notifications.
public readonly struct BuildingActionArgs
{
    public readonly Vector2Int Coords;
    public readonly BuildingBase Building;

    public BuildingActionArgs(Vector2Int coords, BuildingBase building)
    {
        Coords = coords;
        Building = building;
    }
}

public readonly struct PaintPlacementCell
{
    public readonly Vector2Int Coords;
    public readonly Vector3 WorldPos;
    public readonly float CellSize;
    public readonly BuildManager.PlacementValidation Validation;
    public readonly bool WillPlace;

    public PaintPlacementCell(Vector2Int coords, Vector3 worldPos, float cellSize,
                              BuildManager.PlacementValidation validation, bool willPlace)
    {
        Coords = coords;
        WorldPos = worldPos;
        CellSize = cellSize;
        Validation = validation;
        WillPlace = willPlace;
    }
}

public readonly struct PaintPlacementPreviewArgs
{
    public readonly BuildingData Data;
    public readonly IReadOnlyList<PaintPlacementCell> Cells;
    public readonly int PlaceableCount;
    public readonly int BlockedCount;
    public readonly int TotalCost;
    public readonly int CurrentGold;
    public readonly int RemainingGold;

    public PaintPlacementPreviewArgs(BuildingData data, IReadOnlyList<PaintPlacementCell> cells,
                                     int placeableCount, int blockedCount,
                                     int totalCost, int currentGold, int remainingGold)
    {
        Data = data;
        Cells = cells;
        PlaceableCount = placeableCount;
        BlockedCount = blockedCount;
        TotalCost = totalCost;
        CurrentGold = currentGold;
        RemainingGold = remainingGold;
    }
}

public static class ConstructionEvents
{
    #region Hover Events

    // Fired every frame the cursor is over a valid grid cell.
    public static event Action<CellHoveredArgs> OnCellHovered;
    // Fired when the cursor leaves the grid or the pointer is lost.
    public static event Action OnCellLost;

    #endregion

    #region Placement Events

    // OnPlacementStarted: a building was selected and the ghost is now active.
    public static event Action<BuildingData> OnPlacementStarted;
    // OnPlacementUpdated: the cursor moved to a new cell while placing.
    public static event Action<PlacementUpdatedArgs> OnPlacementUpdated;
    // OnPlacementEnded: placement was confirmed or cancelled.
    public static event Action OnPlacementEnded;
    public static event Action<PaintPlacementPreviewArgs> OnPaintPlacementPreviewUpdated;
    public static event Action OnPaintPlacementPreviewEnded;

    #endregion

    #region Tower Focus Events

    // OnTowerFocused: a tower is under the cursor or selected — listeners
    // should display its range indicator and stats panel.
    public static event Action<Tower> OnTowerFocused;
    // OnTowerUpgradeHovered: the Upgrade radial entry is highlighted, so
    // listeners should preview post-upgrade stats instead of current stats.
    public static event Action<Tower> OnTowerUpgradeHovered;

    #endregion

    #region Build Action Events

    // Raised by BuildManager after each successful mutation so the rest of
    // the game (economy, UI, analytics) can react without coupling to BuildManager.
    public static event Action<BuildingActionArgs> OnBuildingPlaced;
    public static event Action<Vector2Int> OnBuildingDemolished;
    public static event Action<BuildingActionArgs> OnBuildingRepaired;
    public static event Action<BuildingActionArgs> OnBuildingUpgraded;

    #endregion

    #region Raise Helpers

    public static void CellHovered(CellHoveredArgs args) => OnCellHovered?.Invoke(args);
    public static void CellLost() => OnCellLost?.Invoke();
    public static void PlacementStarted(BuildingData data) => OnPlacementStarted?.Invoke(data);
    public static void PlacementUpdated(PlacementUpdatedArgs args) => OnPlacementUpdated?.Invoke(args);
    public static void PlacementEnded() => OnPlacementEnded?.Invoke();
    public static void PaintPlacementPreviewUpdated(PaintPlacementPreviewArgs args) => OnPaintPlacementPreviewUpdated?.Invoke(args);
    public static void PaintPlacementPreviewEnded() => OnPaintPlacementPreviewEnded?.Invoke();
    public static void TowerFocused(Tower tower) => OnTowerFocused?.Invoke(tower);
    public static void TowerUpgradeHovered(Tower tower) => OnTowerUpgradeHovered?.Invoke(tower);
    public static void BuildingPlaced(BuildingActionArgs args) => OnBuildingPlaced?.Invoke(args);
    public static void BuildingDemolished(Vector2Int coords) => OnBuildingDemolished?.Invoke(coords);
    public static void BuildingRepaired(BuildingActionArgs args) => OnBuildingRepaired?.Invoke(args);
    public static void BuildingUpgraded(BuildingActionArgs args) => OnBuildingUpgraded?.Invoke(args);

    #endregion
}