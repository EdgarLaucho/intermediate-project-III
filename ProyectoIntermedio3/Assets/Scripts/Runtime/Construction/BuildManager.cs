using UnityEngine;

// Central authority for all construction operations: placing, upgrading, repairing,
// and demolishing buildings. It validates each action against the current game phase
// and the player's gold balance before touching any game state.
public class BuildManager : MonoBehaviour
{
    #region Nested Types

    // Each case describes exactly why a placement was rejected, so callers can
    // show a specific message or tint the preview differently per reason.
    public enum PlacementState
    {
        Valid,
        MissingDependency,  // Grid or Economy reference not set in Inspector.
        MissingData,        // No BuildingData was provided by the caller.
        MissingPrefab,      // BuildingData.prefab is null.
        InvalidPrefab,      // Prefab exists but lacks a BuildingBase component.
        InvalidPhase,       // Action is blocked during the current game phase.
        OutOfBounds,        // Coordinates fall outside the grid.
        NotBuildable,       // Cell exists but is marked non-buildable (e.g. path tile).
        Occupied,           // Another building already occupies the cell.
        InsufficientGold,   // Player cannot afford the cost.
        InvalidCost,        // BuildingData has a negative cost (data authoring error).
    }

    // Immutable result value returned by GetPlacementValidation.
    // Keeping it as a struct avoids a heap allocation on every preview frame.
    public readonly struct PlacementValidation
    {
        public PlacementValidation(PlacementState state, GridCell cell, string reason)
        {
            State = state;
            Cell = cell;
            Reason = reason;
        }

        public PlacementState State { get; }
        public GridCell Cell { get; }       // Null for out-of-bounds or dependency failures.
        public string Reason { get; }
        public bool IsValid => State == PlacementState.Valid;
    }

    #endregion

    #region Inspector Fields

    [SerializeField] private GridManager grid;
    [SerializeField] private EconomyManager economy;

    // Which actions remain available when a combat wave is active.
    // Repair is on by default so players can maintain damaged buildings mid-wave.
    [Header("Actions Allowed During Combat")]
    [SerializeField] private bool allowBuildDuringCombat = false;
    [SerializeField] private bool allowUpgradeDuringCombat = false;
    [SerializeField] private bool allowDemolishDuringCombat = false;
    [SerializeField] private bool allowRepairDuringCombat = true;

    #endregion

    #region Runtime State

    private GamePhase _currentPhase;

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        _currentPhase = GamePhase.Preparation;
        PhaseEvents.OnPhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        PhaseEvents.OnPhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        _currentPhase = phase;
    }

    #endregion

    #region Phase Queries

    // These are read by ConstructionPresenter to enable/disable UI options,
    // and by GetPlacementValidation to gate the build action itself.
    public bool CanBuild => _currentPhase == GamePhase.Preparation || allowBuildDuringCombat;
    public bool CanUpgrade => _currentPhase == GamePhase.Preparation || allowUpgradeDuringCombat;
    public bool CanDemolish => _currentPhase == GamePhase.Preparation || allowDemolishDuringCombat;
    public bool CanRepair => _currentPhase == GamePhase.Preparation || allowRepairDuringCombat;

    // Convenience checks that also verify the building's state and gold balance.
    // Used by the UI to decide whether to render an action as interactable.
    public bool CanRepairBuilding(BuildingBase building)
    {
        if (!CanRepair || building == null) return false;
        if (building.CurrentHealth >= building.MaxHealth) return false;
        int missing = building.MaxHealth - building.CurrentHealth;
        int cost = building.GetRepairCost(missing);
        return economy != null && economy.CanAfford(cost);
    }

    public bool CanUpgradeBuilding(BuildingBase building)
    {
        if (!CanUpgrade || building == null || building.IsMaxLevel) return false;
        if (!TryGetNextUpgradeData(building, Vector2Int.zero, out UpgradeLevelData upgradeData)) return false;
        return economy != null && economy.CanAfford(upgradeData.upgradeCost);
    }

    #endregion

    #region Placement Validation

    // Runs every check in dependency → data → phase → grid → gold order so the
    // returned state always describes the first blocker the player needs to fix.
    public PlacementValidation GetPlacementValidation(Vector2Int coords, BuildingData data)
    {
        if (grid == null || economy == null)
            return new PlacementValidation(PlacementState.MissingDependency, null, "Build system not ready");

        if (data == null)
            return new PlacementValidation(PlacementState.MissingData, null, "No building selected");

        if (data.prefab == null)
            return new PlacementValidation(PlacementState.MissingPrefab, null, $"{data.buildingName} has no prefab");

        if (!data.prefab.TryGetComponent<BuildingBase>(out _))
            return new PlacementValidation(PlacementState.InvalidPrefab, null, $"{data.buildingName} prefab needs a BuildingBase component");

        if (data.buyCost < 0)
            return new PlacementValidation(PlacementState.InvalidCost, null, $"{data.buildingName} has an invalid build cost");

        if (!CanBuild)
            return new PlacementValidation(PlacementState.InvalidPhase, null, "Building unavailable in this phase");

        GridCell cell = grid.GetCell(coords);
        if (cell == null)
            return new PlacementValidation(PlacementState.OutOfBounds, null, "Outside build grid");

        if (!cell.IsBuildable)
            return new PlacementValidation(PlacementState.NotBuildable, cell, "Cannot build here");

        if (cell.IsOccupied)
            return new PlacementValidation(PlacementState.Occupied, cell, "Cell occupied");

        if (!economy.CanAfford(data.buyCost))
            return new PlacementValidation(PlacementState.InsufficientGold, cell, $"Need {data.buyCost} gold");

        return new PlacementValidation(PlacementState.Valid, cell, "Buildable");
    }

    // Shorthand for callers that only need a yes/no answer.
    public bool IsValidPlacement(Vector2Int coords, BuildingData data)
        => GetPlacementValidation(coords, data).IsValid;

    #endregion

    #region Actions

    public bool TryBuild(Vector2Int coords, BuildingData data)
    {
        PlacementValidation validation = GetPlacementValidation(coords, data);
        if (!validation.IsValid)
        {
            LogPlacementFailure(coords, validation);
            return false;
        }

        GridCell cell = validation.Cell;

        Vector3 worldPos = grid.GridToWorld(coords);
        GameObject buildingObject = Instantiate(data.prefab, worldPos, Quaternion.identity);

        // GetComponent is safe here: GetPlacementValidation already verified the prefab.
        BuildingBase building = buildingObject.GetComponent<BuildingBase>();
        if (building == null)
        {
            Debug.LogError($"[BuildManager] Prefab '{data.prefab.name}' has no BuildingBase component. Aborting.");
            Destroy(buildingObject);
            return false;
        }

        building.Initialize(data);

        // Spend gold after Initialize so we never leave a partially-initialised building
        // alive if the economy rejects the transaction (edge case: gold changed mid-frame).
        if (!TrySpendGold(data.buyCost, $"build '{data.buildingName}' at {coords}"))
        {
            Destroy(buildingObject);
            return false;
        }

        cell.SetBuilding(building);
        building.BindToGridCell(coords, cell);

        ConstructionEvents.BuildingPlaced(new BuildingActionArgs(coords, building));
        Debug.Log($"[BuildManager] Built '{data.buildingName}' at {coords}. Gold remaining: {economy.Gold}.");
        return true;
    }

    public bool TryRepair(Vector2Int coords)
    {
        if (!HasRuntimeDependencies("repair"))
            return false;

        if (!CanRepair)
        {
            Debug.Log("[BuildManager] Cannot repair — phase restriction.");
            return false;
        }

        if (!TryGetOccupiedBuilding(coords, "repair", out BuildingBase building))
            return false;

        if (!HasBuildingData(building, coords, "repair"))
            return false;

        int missing = building.MaxHealth - building.CurrentHealth;

        if (missing <= 0)
        {
            Debug.Log($"[BuildManager] Building at {coords} is already at full health.");
            return false;
        }

        int repairCost = building.GetRepairCost(missing);
        if (!TrySpendGold(repairCost, $"repair building at {coords}"))
            return false;

        building.Heal(missing);

        ConstructionEvents.BuildingRepaired(new BuildingActionArgs(coords, building));
        Debug.Log($"[BuildManager] Repaired building at {coords} for {repairCost} gold.");
        return true;
    }

    public bool TryUpgrade(Vector2Int coords)
    {
        if (!HasRuntimeDependencies("upgrade"))
            return false;

        if (!CanUpgrade)
        {
            Debug.Log("[BuildManager] Cannot upgrade — phase restriction.");
            return false;
        }

        if (!TryGetOccupiedBuilding(coords, "upgrade", out BuildingBase building))
            return false;

        if (building.IsMaxLevel)
        {
            Debug.Log($"[BuildManager] Building at {coords} is already at max level.");
            return false;
        }

        if (!TryGetNextUpgradeData(building, coords, out UpgradeLevelData upgradeData))
            return false;

        int upgradeCost = upgradeData.upgradeCost;
        if (!TrySpendGold(upgradeCost, $"upgrade building at {coords}"))
            return false;

        // Track invested gold before calling Upgrade so the refund calculation
        // always reflects the full amount spent on this building.
        building.TotalGoldInvested += upgradeCost;
        building.Upgrade();

        ConstructionEvents.BuildingUpgraded(new BuildingActionArgs(coords, building));
        Debug.Log($"[BuildManager] Upgraded building at {coords} to level {building.CurrentLevel}.");
        return true;
    }

    public bool TryDemolish(Vector2Int coords)
    {
        if (!HasRuntimeDependencies("demolish"))
            return false;

        if (!CanDemolish)
        {
            Debug.Log("[BuildManager] Cannot demolish — phase restriction.");
            return false;
        }

        if (!TryGetOccupiedBuilding(coords, "demolish", out BuildingBase building))
            return false;

        if (!HasBuildingData(building, coords, "demolish"))
            return false;

        int refund = Mathf.Max(0, building.GetRefundAmount());
        GridCell cell = grid.GetCell(coords);

        // Destroy first so the building's OnDeath path doesn't also fire ClearBuilding.
        Destroy(building.gameObject);
        cell.ClearBuilding();
        if (refund > 0) economy.AddGold(refund);

        ConstructionEvents.BuildingDemolished(coords);
        Debug.Log($"[BuildManager] Demolished building at {coords}. Refunded {refund} gold.");
        return true;
    }

    #endregion

    #region Private Helpers

    private bool HasRuntimeDependencies(string actionName)
    {
        bool ok = true;
        if (grid == null) { Debug.LogError($"[BuildManager] Cannot {actionName}: GridManager not assigned."); ok = false; }
        if (economy == null) { Debug.LogError($"[BuildManager] Cannot {actionName}: EconomyManager not assigned."); ok = false; }
        return ok;
    }

    private bool TryGetOccupiedBuilding(Vector2Int coords, string actionName, out BuildingBase building)
    {
        building = null;

        GridCell cell = grid.GetCell(coords);
        if (cell == null || !cell.IsOccupied)
        {
            Debug.Log($"[BuildManager] No building at {coords} to {actionName}.");
            return false;
        }

        building = cell.CurrentBuilding;
        return true;
    }

    private bool HasBuildingData(BuildingBase building, Vector2Int coords, string actionName)
    {
        if (building != null && building.Data != null)
            return true;

        Debug.LogError($"[BuildManager] Cannot {actionName} building at {coords}: building data is missing.");
        return false;
    }

    private bool TryGetNextUpgradeData(BuildingBase building, Vector2Int coords, out UpgradeLevelData upgradeData)
    {
        upgradeData = default;

        if (!HasBuildingData(building, coords, "upgrade"))
            return false;

        if (building.Data.upgradeLevels == null || building.CurrentLevel >= building.Data.upgradeLevels.Length)
        {
            int length = building.Data.upgradeLevels?.Length ?? 0;
            Debug.LogError($"[BuildManager] Cannot upgrade building at {coords}: '{building.Data.buildingName}' upgradeLevels length is {length}, expected at least {building.CurrentLevel + 1}.");
            return false;
        }

        upgradeData = building.Data.upgradeLevels[building.CurrentLevel];
        if (upgradeData.upgradeCost < 0)
        {
            Debug.LogError($"[BuildManager] Cannot upgrade building at {coords}: '{building.Data.buildingName}' has a negative upgrade cost.");
            return false;
        }

        return true;
    }

    private bool TrySpendGold(int cost, string context)
    {
        if (cost <= 0)
            return true;  // Free actions always succeed.

        if (economy.SpendGold(cost))
            return true;

        Debug.Log($"[BuildManager] Cannot {context} — need {cost} gold, have {economy.Gold}.");
        return false;
    }

    // Configuration failures (bad data / missing references) are logged as errors
    // because they indicate a designer or programmer mistake, not a runtime condition.
    private static void LogPlacementFailure(Vector2Int coords, PlacementValidation validation)
    {
        string message = $"[BuildManager] Cannot build at {coords}: {validation.Reason}.";
        if (IsConfigurationFailure(validation.State))
            Debug.LogError(message);
        else
            Debug.Log(message);
    }

    private static bool IsConfigurationFailure(PlacementState state)
    {
        return state == PlacementState.MissingDependency
            || state == PlacementState.MissingData
            || state == PlacementState.MissingPrefab
            || state == PlacementState.InvalidPrefab
            || state == PlacementState.InvalidCost;
    }

    #endregion
}
