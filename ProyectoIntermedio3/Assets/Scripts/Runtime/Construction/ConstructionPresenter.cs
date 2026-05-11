using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Sits between raw input events and BuildManager. It owns the placement state
// machine (Idle / Placing / MenuOpen), feeds the radial menu with contextual
// entries, and translates user decisions into BuildManager calls.
public sealed class ConstructionPresenter : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private BuildManager construction;
    [SerializeField] private GridManager grid;
    [SerializeField] private RadialMenu radialMenu;
    [SerializeField] private BuildingData[] availableBuildings;

    #endregion

    #region State

    private enum State { Idle, Placing, MenuOpen }
    private enum ActionId { Repair, Upgrade, Demolish, Back }

    private State _state;
    private BuildingData _selectedBuilding;
    private Vector2Int _hoveredCell;
    private Vector3 _hoveredWorldPos;
    // Cached when opening a context menu so radial callbacks know which cell/building they target.
    private Vector2Int _menuCell;
    private BuildingBase _menuBuilding;
    private bool _hasPointer;

    // Filtered copy of availableBuildings with null entries removed.
    private BuildingData[] _catalogue;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        _catalogue = CleanCatalogue(availableBuildings).ToArray();
    }

    private void OnEnable()
    {
        InputEvents.OnWorldPointerMoved += OnPointerMoved;
        InputEvents.OnWorldPointerLost += OnPointerLost;
        InputEvents.OnPrimaryPressed += OnPrimaryPressed;
        InputEvents.OnSecondaryPressed += OnSecondaryPressed;
        InputEvents.OnCancelPressed += CancelToIdle;
        SubscribeRadial();
    }

    private void OnDisable()
    {
        InputEvents.OnWorldPointerMoved -= OnPointerMoved;
        InputEvents.OnWorldPointerLost -= OnPointerLost;
        InputEvents.OnPrimaryPressed -= OnPrimaryPressed;
        InputEvents.OnSecondaryPressed -= OnSecondaryPressed;
        InputEvents.OnCancelPressed -= CancelToIdle;
        UnsubscribeRadial();
    }

    // If the radial menu closes externally (e.g. clicking outside it), snap back to Idle
    // so the state machine doesn't get stuck in MenuOpen with no visible menu.
    private void Update()
    {
        if (_state == State.MenuOpen && (radialMenu == null || !radialMenu.IsOpen))
            _state = State.Idle;
    }

    #endregion

    #region Input Handlers

    private void OnPointerMoved(Vector3 worldPos, Vector2Int gridCoords)
    {
        _hasPointer = true;
        _hoveredCell = gridCoords;
        _hoveredWorldPos = worldPos;

        switch (_state)
        {
            case State.Idle: UpdateIdleHover(gridCoords); break;
            case State.Placing: UpdatePlacementPreview(gridCoords, worldPos); break;
        }
    }

    private void OnPointerLost()
    {
        _hasPointer = false;
        if (_state == State.Idle)
        {
            ConstructionEvents.CellLost();
            ConstructionEvents.TowerFocused(null);
        }
    }

    private void OnPrimaryPressed()
    {
        if (_state == State.Placing) ConfirmPlacement();
    }

    private void OnSecondaryPressed()
    {
        if (_state == State.Placing) { CancelToIdle(); return; }
        if (_state == State.Idle && _hasPointer) OpenContextMenu(_hoveredCell);
    }

    #endregion

    #region State Transitions

    // Broadcasts hover info while idle so the range indicator and cell highlight
    // stay responsive even when no building is selected.
    private void UpdateIdleHover(Vector2Int coords)
    {
        GridCell cell = grid.GetCell(coords);
        if (cell == null)
        {
            ConstructionEvents.CellLost();
            ConstructionEvents.TowerFocused(null);
            return;
        }

        ConstructionEvents.CellHovered(new CellHoveredArgs(
            coords, cell, grid.GridToWorld(coords), grid.CellSize, null));

        ConstructionEvents.TowerFocused(cell.CurrentBuilding as Tower);
    }

    private void UpdatePlacementPreview(Vector2Int coords, Vector3 worldPos)
    {
        if (_selectedBuilding == null) return;

        BuildManager.PlacementValidation validation = construction.GetPlacementValidation(coords, _selectedBuilding);
        // Use the cell from the validation when available; fall back to GetCell so the
        // highlight still appears for out-of-range or invalid cells.
        GridCell cell = validation.Cell ?? grid.GetCell(coords);
        Vector3 snappedPos = grid.GridToWorld(coords);

        ConstructionEvents.PlacementUpdated(new PlacementUpdatedArgs(coords, snappedPos, validation, cell));

        if (cell != null)
            ConstructionEvents.CellHovered(new CellHoveredArgs(
                coords, cell, snappedPos, grid.CellSize, validation));
        else
            ConstructionEvents.CellLost();
    }

    private void OpenContextMenu(Vector2Int coords)
    {
        GridCell cell = grid.GetCell(coords);
        if (cell == null) return;

        _menuCell = coords;
        _menuBuilding = cell.CurrentBuilding;
        _state = State.MenuOpen;
        ConstructionEvents.CellLost();

        if (cell.IsOccupied)
        {
            // Show actions relevant to the existing building (repair / upgrade / demolish).
            ConstructionEvents.TowerFocused(cell.CurrentBuilding as Tower);
            radialMenu?.ShowEntries(BuildActionEntries(coords, cell.CurrentBuilding));
        }
        else if (cell.IsBuildable)
        {
            // Show the category browser so the player can pick what to build.
            ConstructionEvents.TowerFocused(null);
            radialMenu?.ShowEntries(BuildCategoryEntries());
        }
        else
        {
            // Non-buildable cell (path, water, etc.) — nothing to show.
            CancelToIdle();
        }
    }

    private void ConfirmPlacement()
    {
        if (_selectedBuilding == null) { CancelToIdle(); return; }

        BuildManager.PlacementValidation validation = construction.GetPlacementValidation(_hoveredCell, _selectedBuilding);
        if (!validation.IsValid)
        {
            // Re-broadcast the updated state so BuildPreview shows the rejection shake.
            ConstructionEvents.PlacementUpdated(new PlacementUpdatedArgs(
                _hoveredCell, _hoveredWorldPos, validation,
                validation.Cell ?? grid.GetCell(_hoveredCell)));
            return;
        }

        if (construction.TryBuild(_hoveredCell, _selectedBuilding))
            CancelToIdle();
    }

    private void CancelToIdle()
    {
        _state = State.Idle;
        _selectedBuilding = null;
        _menuBuilding = null;
        radialMenu?.HideImmediate();
        ConstructionEvents.PlacementEnded();
        ConstructionEvents.TowerFocused(null);
    }

    private void BeginPlacement(BuildingData data)
    {
        _selectedBuilding = data;
        _state = State.Placing;
        _menuBuilding = null;
        radialMenu?.HideImmediate();
        ConstructionEvents.PlacementStarted(data);
        ConstructionEvents.CellLost();

        // If the cursor is already over the grid, prime the preview immediately
        // so it doesn't wait for the next pointer-moved event.
        if (_hasPointer)
            UpdatePlacementPreview(_hoveredCell, _hoveredWorldPos);
    }

    #endregion

    #region Radial Menu

    private void SubscribeRadial()
    {
        if (radialMenu == null) return;
        radialMenu.OnEntrySelected += HandleRadialEntrySelected;
        radialMenu.OnEntryHovered += HandleRadialEntryHovered;
        radialMenu.OnClosed += CancelToIdle;
    }

    private void UnsubscribeRadial()
    {
        if (radialMenu == null) return;
        radialMenu.OnEntrySelected -= HandleRadialEntrySelected;
        radialMenu.OnEntryHovered -= HandleRadialEntryHovered;
        radialMenu.OnClosed -= CancelToIdle;
    }

    private void HandleRadialEntrySelected(RadialMenu.Entry entry)
    {
        if (!entry.Interactable) return;

        // Payload is typed, so each case handles one kind of data without casting unsafely.
        switch (entry.Payload)
        {
            case BuildingCategory category:
                radialMenu?.ShowEntries(BuildBuildingEntries(category));
                break;
            case BuildingData data:
                BeginPlacement(data);
                break;
            case ActionId.Back:
                radialMenu?.ShowEntries(BuildCategoryEntries());
                break;
            case ActionId.Repair:
                construction.TryRepair(_menuCell);
                CancelToIdle();
                break;
            case ActionId.Upgrade:
                construction.TryUpgrade(_menuCell);
                CancelToIdle();
                break;
            case ActionId.Demolish:
                construction.TryDemolish(_menuCell);
                CancelToIdle();
                break;
        }
    }

    // While hovering the Upgrade entry, show the post-upgrade stat preview on the
    // tower indicator; revert to the current stats when the cursor moves away.
    private void HandleRadialEntryHovered(RadialMenu.Entry? entry)
    {
        if (_menuBuilding is not Tower tower) return;

        if (entry.HasValue && entry.Value.Payload is ActionId.Upgrade && entry.Value.Interactable)
            ConstructionEvents.TowerUpgradeHovered(tower);
        else
            ConstructionEvents.TowerFocused(tower);
    }

    #endregion

    #region Entry Builders

    // One entry per distinct building category, sorted by the enum value so the
    // order in the menu is deterministic and matches the asset authoring intent.
    private IReadOnlyList<RadialMenu.Entry> BuildCategoryEntries()
    {
        return CleanCatalogue(_catalogue)
            .Select(d => d.Category)
            .Distinct()
            .OrderBy(c => (int)c)
            .Select(c => new RadialMenu.Entry(c.ToString(), string.Empty, true, c))
            .ToArray();
    }

    private IReadOnlyList<RadialMenu.Entry> BuildBuildingEntries(BuildingCategory category)
    {
        var entries = new List<RadialMenu.Entry>();
        foreach (BuildingData data in CleanCatalogue(_catalogue).Where(d => d.Category == category))
        {
            BuildManager.PlacementValidation v = construction.GetPlacementValidation(_menuCell, data);
            entries.Add(new RadialMenu.Entry(data.buildingName, BuildSubLabel(data, v), v.IsValid, data));
        }
        entries.Add(new RadialMenu.Entry("Back", string.Empty, true, ActionId.Back));
        return entries;
    }

    private IReadOnlyList<RadialMenu.Entry> BuildActionEntries(Vector2Int coords, BuildingBase building)
    {
        var entries = new List<RadialMenu.Entry>();

        int repairCost = building.GetRepairCost();
        bool canRepair = construction.CanRepairBuilding(building);
        entries.Add(new RadialMenu.Entry("Repair", $"{repairCost}g", canRepair, ActionId.Repair));

        int upgradeCost = building.GetUpgradeCost();
        bool canUpgrade = construction.CanUpgradeBuilding(building);
        string upgradeLabel = building.IsMaxLevel ? "Max Level" : "Upgrade";
        string upgradeSub = building.IsMaxLevel ? string.Empty : BuildUpgradeSubLabel(building, upgradeCost);
        entries.Add(new RadialMenu.Entry(upgradeLabel, upgradeSub, canUpgrade, ActionId.Upgrade));

        int refund = building.GetRefundAmount();
        entries.Add(new RadialMenu.Entry("Demolish", $"+{refund}g", construction.CanDemolish, ActionId.Demolish));

        return entries;
    }

    #endregion

    #region Helpers

    // Strips nulls so the rest of the class never needs to null-check catalogue entries.
    private static IEnumerable<BuildingData> CleanCatalogue(IEnumerable<BuildingData> catalogue)
        => catalogue?.Where(d => d != null) ?? Enumerable.Empty<BuildingData>();

    // Sub-label shown under the building name: cost when valid, rejection reason when not.
    private static string BuildSubLabel(BuildingData data, BuildManager.PlacementValidation v)
        => v.IsValid ? $"{data.buyCost}g" : v.Reason;

    // Builds a compact stat-delta string like "150g  +12 DMG  +1.5 RNG" so the
    // player can see exactly what the upgrade will change before committing gold.
    private static string BuildUpgradeSubLabel(BuildingBase building, int upgradeCost)
    {
        if (building is Tower tower &&
            tower.TryPreviewNextUpgrade(out int nextDamage, out float nextRange, out float nextFireRate))
        {
            UpgradeLevelData upgrade = building.Data.upgradeLevels[building.CurrentLevel];
            var towerParts = new List<string> { $"{upgradeCost}g" };
            if (nextDamage - tower.AttackDamage > 0) towerParts.Add($"+{nextDamage - tower.AttackDamage} DMG");
            if (nextRange - tower.AttackRange > 0.05f) towerParts.Add($"+{FormatStat(nextRange - tower.AttackRange)} RNG");
            if (nextFireRate - tower.FireRate > 0.05f) towerParts.Add($"+{FormatStat(nextFireRate - tower.FireRate)} CAD");
            if (upgrade.bonusProjectilesPerAttack > 0) towerParts.Add($"+{upgrade.bonusProjectilesPerAttack} PROJ");
            if (upgrade.visualPrefabOverride != null) towerParts.Add("VIS");
            return string.Join("  ", towerParts);
        }

        if (building is Trap trap && building.Data?.upgradeLevels != null && building.CurrentLevel < building.Data.upgradeLevels.Length)
        {
            UpgradeLevelData upgrade = building.Data.upgradeLevels[building.CurrentLevel];
            var trapParts = new List<string> { $"{upgradeCost}g" };
            if (upgrade.statMultiplier > 1f && trap.TriggerDamage > 0) trapParts.Add("+DMG");
            if (upgrade.bonusTrapUses > 0) trapParts.Add($"+{upgrade.bonusTrapUses} USO");
            if (upgrade.bonusRange > 0f || upgrade.statMultiplier > 1f) trapParts.Add("+RNG");
            return string.Join("  ", trapParts);
        }

        if (building is Wall && building.Data?.upgradeLevels != null && building.CurrentLevel < building.Data.upgradeLevels.Length)
        {
            UpgradeLevelData upgrade = building.Data.upgradeLevels[building.CurrentLevel];
            var wallParts = new List<string> { $"{upgradeCost}g" };
            if (upgrade.bonusMaxHealth > 0) wallParts.Add($"+{upgrade.bonusMaxHealth} HP");
            if (upgrade.bonusArmor > 0) wallParts.Add($"+{upgrade.bonusArmor} ARM");
            if (upgrade.bonusThornsDamage > 0) wallParts.Add($"+{upgrade.bonusThornsDamage} THR");
            return string.Join("  ", wallParts);
        }

        return $"{upgradeCost}g";
    }

    // Format floats compactly: whole numbers for values >= 10, one decimal place otherwise.
    private static string FormatStat(float v) => v >= 10f ? v.ToString("0") : v.ToString("0.#");

    #endregion
}