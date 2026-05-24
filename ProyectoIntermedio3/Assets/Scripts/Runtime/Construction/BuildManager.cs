using UnityEngine;

public class BuildManager : MonoBehaviour
{
    public enum PlacementState
    {
        Valid,
        Blocked,
        InvalidPhase,
        OutOfBounds,
        NotBuildable,
        Occupied,
        InsufficientGold,
    }

    public readonly struct PlacementValidation
    {
        public PlacementValidation(PlacementState state, GridCell cell, string reason)
        {
            State = state;
            Cell = cell;
            Reason = reason;
        }

        public PlacementState State { get; }
        public GridCell Cell { get; }
        public string Reason { get; }
        public bool IsValid => State == PlacementState.Valid;
    }

    [SerializeField] private GridManager grid;
    [SerializeField] private EconomyManager economy;

    [Header("Actions Allowed During Combat")]
    [SerializeField] private bool allowBuildDuringCombat = false;
    [SerializeField] private bool allowUpgradeDuringCombat = true;
    [SerializeField] private bool allowDemolishDuringCombat = true;
    [SerializeField] private bool allowRepairDuringCombat = true;

    private GamePhase _currentPhase;

    public bool CanBuild => _currentPhase == GamePhase.Preparation || allowBuildDuringCombat;
    public bool CanUpgrade => _currentPhase == GamePhase.Preparation || allowUpgradeDuringCombat;
    public bool CanDemolish => _currentPhase == GamePhase.Preparation || allowDemolishDuringCombat;
    public bool CanRepair => _currentPhase == GamePhase.Preparation || allowRepairDuringCombat;
    public int CurrentGold => economy != null ? economy.Gold : 0;

    private void OnEnable()
    {
        _currentPhase = PhaseEvents.CurrentPhase;
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

    public bool CanRepairBuilding(BuildingBase building)
    {
        if (!CanRepair || building == null || economy == null) return false;
        if (building.CurrentHealth >= building.MaxHealth) return false;
        return economy.CanAfford(building.GetRepairCost());
    }

    public bool CanUpgradeBuilding(BuildingBase building)
    {
        if (!CanUpgrade || building == null || building.IsMaxLevel || economy == null) return false;
        if (!TryGetNextUpgradeData(building, out var upgradeData)) return false;
        return economy.CanAfford(upgradeData.upgradeCost);
    }

    public bool CanBeginPlacement(BuildingData data, out string reason)
    {
        if (!CanUseBuildData(data, out reason))
            return false;

        if (!CanBuild)
        {
            reason = "Building unavailable in this phase";
            return false;
        }

        if (economy != null && !economy.CanAfford(data.buyCost))
        {
            reason = $"Need {data.buyCost} gold";
            return false;
        }

        reason = "Buildable";
        return true;
    }

    public PlacementValidation GetPlacementValidation(Vector2Int coords, BuildingData data)
    {
        if (!CanUseBuildData(data, out var reason))
            return new PlacementValidation(PlacementState.Blocked, null, reason);

        if (!CanBuild)
            return new PlacementValidation(PlacementState.InvalidPhase, null, "Building unavailable in this phase");

        var placementPrefab = BuildingRotationHelper.ResolvePrefab(coords, data);
        if (placementPrefab == null || !placementPrefab.TryGetComponent<BuildingBase>(out _))
            return new PlacementValidation(PlacementState.Blocked, null, "Invalid building prefab");

        var cell = grid.GetCell(coords);
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

    public bool IsValidPlacement(Vector2Int coords, BuildingData data)
        => GetPlacementValidation(coords, data).IsValid;

    public bool TryBuild(Vector2Int coords, BuildingData data)
    {
        var validation = GetPlacementValidation(coords, data);
        if (!validation.IsValid)
        {
            Debug.Log($"[BuildManager] Cannot build at {coords}: {validation.Reason}.");
            return false;
        }

        var spawnPrefab = BuildingRotationHelper.ResolvePrefab(coords, data);
        var worldPos = grid.GridToWorld(coords);
        var placementRotation = BuildingRotationHelper.ComputePlacementRotationForCell(coords, data) * spawnPrefab.transform.rotation;
        var buildingObject = Instantiate(spawnPrefab, worldPos, placementRotation);
        var building = buildingObject.GetComponent<BuildingBase>();

        if (!SpendGold(data.buyCost))
        {
            Destroy(buildingObject);
            Debug.Log($"[BuildManager] Cannot build '{data.buildingName}' at {coords}: need {data.buyCost} gold.");
            return false;
        }

        building.Initialize(data);
        validation.Cell.SetBuilding(building);
        building.BindToGridCell(coords, validation.Cell);

        ConstructionEvents.BuildingPlaced(new BuildingActionArgs(coords, building)); 
        return true;
    }

    public bool TryRepair(Vector2Int coords)
    {
        if (!CanRepair || !TryGetBuilding(coords, out var building)) return false;

        var missingHealth = building.MaxHealth - building.CurrentHealth;
        if (missingHealth <= 0) return false;

        var repairCost = building.GetRepairCost(missingHealth);
        if (!SpendGold(repairCost)) return false;

        building.Heal(missingHealth);
        ConstructionEvents.BuildingRepaired(new BuildingActionArgs(coords, building));
        Debug.Log($"[BuildManager] Repaired building at {coords} for {repairCost} gold.");
        return true;
    }

    public bool TryUpgrade(Vector2Int coords)
    {
        if (!CanUpgrade || !TryGetBuilding(coords, out var building)) return false;
        if (building.IsMaxLevel || !TryGetNextUpgradeData(building, out var upgradeData)) return false;
        if (!SpendGold(upgradeData.upgradeCost)) return false;

        building.TotalGoldInvested += upgradeData.upgradeCost;
        building.Upgrade();

        ConstructionEvents.BuildingUpgraded(new BuildingActionArgs(coords, building));
        Debug.Log($"[BuildManager] Upgraded building at {coords} to level {building.CurrentLevel}.");
        return true;
    }

    public bool TryDemolish(Vector2Int coords)
    {
        if (!CanDemolish || !TryGetBuilding(coords, out var building)) return false;

        var refund = Mathf.Max(0, building.GetRefundAmount());
        var cell = grid.GetCell(coords);

        Destroy(building.gameObject);
        cell.ClearBuilding();
        if (refund > 0) economy.AddGold(refund);

        ConstructionEvents.BuildingDemolished(coords);
        Debug.Log($"[BuildManager] Demolished building at {coords}. Refunded {refund} gold.");
        return true;
    }

    private bool CanUseBuildData(BuildingData data, out string reason)
    {
        if (grid == null || economy == null)
        {
            reason = "Build system not ready";
            return false;
        }

        if (data == null || data.prefab == null)
        {
            reason = "No building selected";
            return false;
        }

        if (!data.prefab.TryGetComponent<BuildingBase>(out _))
        {
            reason = "Invalid building prefab";
            return false;
        }

        if (data.buyCost < 0)
        {
            reason = "Invalid build cost";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private bool TryGetBuilding(Vector2Int coords, out BuildingBase building)
    {
        building = null;
        if (grid == null || economy == null) return false;

        var cell = grid.GetCell(coords);
        building = cell != null && cell.IsOccupied ? cell.CurrentBuilding : null;
        return building != null && building.Data != null;
    }

    private bool TryGetNextUpgradeData(BuildingBase building, out UpgradeLevelData upgradeData)
    {
        upgradeData = default;

        if (building?.Data?.upgradeLevels == null)
            return false;

        if (building.CurrentLevel < 0 || building.CurrentLevel >= building.Data.upgradeLevels.Length)
            return false;

        upgradeData = building.Data.upgradeLevels[building.CurrentLevel];
        return upgradeData.upgradeCost >= 0;
    }

    private bool SpendGold(int amount)
    {
        return amount <= 0 || economy.SpendGold(amount);
    }
}