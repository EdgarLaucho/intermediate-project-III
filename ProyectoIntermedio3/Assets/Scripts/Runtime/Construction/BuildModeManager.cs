using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Sits between raw input events and BuildManager. It owns the placement state
// machine (Idle / Placing / MenuOpen), feeds the radial menu with contextual
// entries, and translates user decisions into BuildManager calls.
public sealed class BuildModeManager : MonoBehaviour
{
    #region Inspector Fields

    [SerializeField] private BuildManager construction;
    [SerializeField] private GridManager grid;
    [SerializeField] private RadialMenu radialMenu;
    [SerializeField] private BuildingData[] availableBuildings;

    #endregion

    #region State

    private enum State { Idle, Placing, MenuOpen }
    public bool IsPlacing => _state == State.Placing || _state == State.MenuOpen;
    private enum ActionId { Repair, Upgrade, Demolish, Back }

    private State _state;
    private BuildingData _selectedBuilding;
    private Vector2Int _hoveredCell;
    private Vector3 _hoveredWorldPos;
    // Cached when opening a context menu so radial callbacks know which cell/building they target.
    private Vector2Int _menuCell;
    private BuildingBase _menuBuilding;
    private bool _hasPointer;

    // Paint mode: true while the player holds left-click during a paint-capable placement.
    private bool _isPainting;
    private Vector2Int _lastPaintCell;
    private bool _hasLastPaintCell;
    private Vector2Int _paintCursorCell;
    private bool _hasPaintCursorCell;
    private readonly List<Vector2Int> _paintStroke = new();
    private readonly HashSet<Vector2Int> _paintStrokeSet = new();
    private bool _isGamePaused;

    // Filtered copy of availableBuildings with null entries removed.
    private BuildingData[] _catalogue;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        if (construction == null)
            construction = FindAnyObjectByType<BuildManager>(FindObjectsInactive.Exclude);

        if (grid == null)
            grid = FindAnyObjectByType<GridManager>(FindObjectsInactive.Exclude);

        if (radialMenu == null)
            radialMenu = FindAnyObjectByType<RadialMenu>(FindObjectsInactive.Exclude);

        _catalogue = CleanCatalogue(availableBuildings).ToArray();
    }

    private void OnEnable()
    {
        InputEvents.OnWorldPointerMoved += OnPointerMoved;
        InputEvents.OnWorldPointerLost += OnPointerLost;
        InputEvents.OnPrimaryPressed += OnPrimaryPressed;
        InputEvents.OnPrimaryHeld += OnPrimaryHeld;
        InputEvents.OnPrimaryReleased += OnPrimaryReleased;
        InputEvents.OnSecondaryPressed += OnSecondaryPressed;
        InputEvents.OnTertiaryPressed += OnTertiaryPressed;
        InputEvents.OnCancelPressed += CancelToIdle;
        GamePauseEvents.OnGamePaused += HandleGamePaused;
        GamePauseEvents.OnGameResumed += HandleGameResumed;
        PhaseEvents.OnPhaseChanged += HandlePhaseChanged;
        SubscribeRadial();

        _isGamePaused = GamePauseEvents.IsPaused;
        if (_isGamePaused)
            HandleGamePaused();
    }

    private void OnDisable()
    {
        InputEvents.OnWorldPointerMoved -= OnPointerMoved;
        InputEvents.OnWorldPointerLost -= OnPointerLost;
        InputEvents.OnPrimaryPressed -= OnPrimaryPressed;
        InputEvents.OnPrimaryHeld -= OnPrimaryHeld;
        InputEvents.OnPrimaryReleased -= OnPrimaryReleased;
        InputEvents.OnSecondaryPressed -= OnSecondaryPressed;
        InputEvents.OnTertiaryPressed -= OnTertiaryPressed;
        InputEvents.OnCancelPressed -= CancelToIdle;
        GamePauseEvents.OnGamePaused -= HandleGamePaused;
        GamePauseEvents.OnGameResumed -= HandleGameResumed;
        PhaseEvents.OnPhaseChanged -= HandlePhaseChanged;
        UnsubscribeRadial();
    }

    // If the radial menu closes externally (e.g. clicking outside it), snap back to Idle
    // so the state machine doesn't get stuck in MenuOpen with no visible menu.
    private void Update()
    {
        if (_isGamePaused)
            return;

        if (_state == State.MenuOpen && (radialMenu == null || !radialMenu.IsOpen))
            _state = State.Idle;
    }

    #endregion

    #region Input Handlers

    private void OnPointerMoved(Vector3 worldPos, Vector2Int gridCoords)
    {
        if (_isGamePaused)
            return;

        _hasPointer = true;
        _hoveredCell = gridCoords;
        _hoveredWorldPos = worldPos;

        switch (_state)
        {
            case State.Idle: UpdateIdleHover(gridCoords); break;
            case State.Placing:
                UpdatePlacementPreview(gridCoords, worldPos);
                if (_isPainting) AppendPaintCellsThrough(gridCoords);
                break;
        }
    }

    private void OnPointerLost()
    {
        if (_isGamePaused)
            return;

        _hasPointer = false;
        if (_state == State.Idle)
        {
            ConstructionEvents.CellLost();
            ConstructionEvents.TowerFocused(null);
        }
    }

    private void OnPrimaryPressed(Vector3 worldPos)
    {
        if (_isGamePaused)
            return;

        if (_state != State.Placing) return;

        if (CanPaintSelectedBuilding())
            BeginPaintStroke(_hoveredCell);
        else
            ConfirmPlacement();
    }

    private void OnPrimaryHeld(Vector3 worldPos)
    {
        if (_isGamePaused)
            return;

        if (_state == State.Placing && _isPainting)
            AppendPaintCellsThrough(_hoveredCell);
    }

    private void OnPrimaryReleased(Vector3 worldPos)
    {
        if (_isGamePaused)
            return;

        if (_isPainting)
            CommitPaintStroke();
    }

    private void OnSecondaryPressed(Vector3 worldPos)
    {
        if (_isGamePaused)
            return;

        if (_state == State.Placing) { CancelToIdle(); return; }
        if (_state == State.Idle && _hasPointer) OpenContextMenu(_hoveredCell);
    }

    private void OnTertiaryPressed(Vector3 worldPos)
    {
        if (_isGamePaused)
            return;

        if (_state != State.Idle || !_hasPointer) return;
        TryDuplicateHoveredBuilding();
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

        if (construction != null && !construction.CanBuild && !cell.IsOccupied)
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
        else if (cell.IsBuildable && construction != null && construction.CanBuild)
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

    private void TryDuplicateHoveredBuilding()
    {
        if (construction != null && !construction.CanBuild)
            return;

        GridCell cell = grid.GetCell(_hoveredCell);
        BuildingBase sourceBuilding = cell?.CurrentBuilding;
        BuildingData data = sourceBuilding != null ? sourceBuilding.Data : null;
        if (data == null) return;

        if (!construction.CanBeginPlacement(data, out string reason))
        {
            Debug.Log($"[ConstructionPresenter] Cannot duplicate '{data.buildingName}' from {_hoveredCell}: {reason}.");
            return;
        }

        BeginPlacement(data);
    }

    private void ConfirmPlacement()
    {
        if (_selectedBuilding == null) { CancelToIdle(); return; }

        BuildManager.PlacementValidation validation = 
            construction.GetPlacementValidation(_hoveredCell, _selectedBuilding);

        if (!validation.IsValid)
        {
            // Re-broadcast the updated state so BuildPreview shows the rejection shake.
            ConstructionEvents.PlacementUpdated(new PlacementUpdatedArgs(
                _hoveredCell, _hoveredWorldPos, validation,
                validation.Cell ?? grid.GetCell(_hoveredCell)));
            return;
        }

        try
        {
            construction.TryBuild(_hoveredCell, _selectedBuilding);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            CancelToIdle();
        }
    }

    private void CancelToIdle()
    {
        _state = State.Idle;
        _selectedBuilding = null;
        _menuBuilding = null;
        ClearPaintStroke(true);
        radialMenu?.HideImmediate();
        ConstructionEvents.PlacementEnded();
        ConstructionEvents.TowerFocused(null);
    }

    private void HandleGamePaused()
    {
        _isGamePaused = true;
        _hasPointer = false;
        CancelToIdle();
        ConstructionEvents.CellLost();
    }

    private void HandleGameResumed()
    {
        _isGamePaused = false;
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Combat)
            CancelToIdle();
    }

    private void BeginPlacement(BuildingData data)
    {
        _selectedBuilding = data;
        _state = State.Placing;
        _menuBuilding = null;
        ClearPaintStroke(true);
        radialMenu?.HideImmediate();
        ConstructionEvents.PlacementStarted(data);
        ConstructionEvents.CellLost();

        // If the cursor is already over the grid, prime the preview immediately
        // so it doesn't wait for the next pointer-moved event.
        if (_hasPointer)
            UpdatePlacementPreview(_hoveredCell, _hoveredWorldPos);
    }

    private bool CanPaintSelectedBuilding()
        => _selectedBuilding != null
        && (_selectedBuilding.allowPaintPlacement || _selectedBuilding.Category == BuildingCategory.Wall);

    private void BeginPaintStroke(Vector2Int coords)
    {
        ClearPaintStroke(false);
        _isPainting = true;
        AppendPaintCellsThrough(coords);
    }

    private void AppendPaintCellsThrough(Vector2Int coords)
    {
        if (_selectedBuilding == null) return;

        if (!_hasLastPaintCell)
        {
            ProcessPaintCell(coords);
            _lastPaintCell = coords;
            _hasLastPaintCell = true;
            BroadcastPaintPreview();
            return;
        }

        int dx = coords.x - _lastPaintCell.x;
        int dy = coords.y - _lastPaintCell.y;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        if (steps <= 0) return;

        for (int step = 1; step <= steps; step++)
        {
            float t = step / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(_lastPaintCell.x, coords.x, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(_lastPaintCell.y, coords.y, t));
            ProcessPaintCell(new Vector2Int(x, y));
        }

        _lastPaintCell = coords;
        BroadcastPaintPreview();
    }

    private void ProcessPaintCell(Vector2Int coords)
    {
        _paintCursorCell = coords;
        _hasPaintCursorCell = true;

        if (TryGetPaintCellIndex(coords, out int existingIndex))
        {
            TrimPaintStrokeTo(existingIndex);
            return;
        }

        BuildManager.PlacementValidation validation = construction.GetPlacementValidation(coords, _selectedBuilding);
        if (!ShouldKeepPaintCell(validation))
            return;

        AddPaintCell(coords);
    }

    private bool ShouldKeepPaintCell(BuildManager.PlacementValidation validation)
    {
        return validation.IsValid || validation.State == BuildManager.PlacementState.InsufficientGold;
    }

    private bool TryGetPaintCellIndex(Vector2Int coords, out int index)
    {
        for (int i = 0; i < _paintStroke.Count; i++)
        {
            if (_paintStroke[i] == coords)
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }

    private void TrimPaintStrokeTo(int index)
    {
        for (int i = _paintStroke.Count - 1; i > index; i--)
        {
            _paintStrokeSet.Remove(_paintStroke[i]);
            _paintStroke.RemoveAt(i);
        }
    }

    private void AddPaintCell(Vector2Int coords)
    {
        if (_paintStrokeSet.Add(coords))
            _paintStroke.Add(coords);
    }

    private void CommitPaintStroke()
    {
        List<PaintPlacementCell> previewCells = BuildPaintPreviewCells(
            out _, out _, out _, out _, out _);

        foreach (PaintPlacementCell cell in previewCells)
        {
            if (cell.WillPlace)
                construction.TryBuild(cell.Coords, _selectedBuilding);
        }

        ClearPaintStroke(true);

        if (_state == State.Placing && _hasPointer)
            UpdatePlacementPreview(_hoveredCell, _hoveredWorldPos);
    }

    private void BroadcastPaintPreview()
    {
        List<PaintPlacementCell> previewCells = BuildPaintPreviewCells(
            out int placeableCount, out int blockedCount, out int totalCost,
            out int currentGold, out int remainingGold);

        ConstructionEvents.PaintPlacementPreviewUpdated(new PaintPlacementPreviewArgs(
            _selectedBuilding, previewCells, placeableCount, blockedCount,
            totalCost, currentGold, remainingGold));
    }

    private List<PaintPlacementCell> BuildPaintPreviewCells(out int placeableCount, out int blockedCount,
                                                            out int totalCost, out int currentGold,
                                                            out int remainingGold)
    {
        var previewCells = new List<PaintPlacementCell>(_paintStroke.Count);
        placeableCount = 0;
        blockedCount = 0;
        totalCost = 0;
        currentGold = construction != null ? construction.CurrentGold : 0;
        remainingGold = currentGold;

        if (_selectedBuilding == null || grid == null || construction == null)
            return previewCells;

        int cost = Mathf.Max(0, _selectedBuilding.buyCost);

        foreach (Vector2Int coords in _paintStroke)
        {
            BuildManager.PlacementValidation validation = construction.GetPlacementValidation(coords, _selectedBuilding);
            bool willPlace = validation.IsValid && totalCost + cost <= currentGold;

            if (validation.IsValid && !willPlace)
            {
                GridCell cell = validation.Cell ?? grid.GetCell(coords);
                int missing = totalCost + cost - currentGold;
                validation = new BuildManager.PlacementValidation(
                    BuildManager.PlacementState.InsufficientGold,
                    cell,
                    $"Need {missing} more gold");
            }

            if (willPlace)
            {
                placeableCount++;
                totalCost += cost;
                remainingGold = currentGold - totalCost;
            }
            else
            {
                blockedCount++;
            }

            previewCells.Add(new PaintPlacementCell(
                coords,
                grid.GridToWorld(coords),
                grid.CellSize,
                validation,
                willPlace));
        }

        AddTransientCursorCell(previewCells, ref blockedCount);

        return previewCells;
    }

    private void AddTransientCursorCell(List<PaintPlacementCell> previewCells, ref int blockedCount)
    {
        if (!_hasPaintCursorCell || _paintStrokeSet.Contains(_paintCursorCell))
            return;

        BuildManager.PlacementValidation validation = construction.GetPlacementValidation(_paintCursorCell, _selectedBuilding);
        if (ShouldKeepPaintCell(validation))
            return;

        blockedCount++;
        previewCells.Add(new PaintPlacementCell(
            _paintCursorCell,
            grid.GridToWorld(_paintCursorCell),
            grid.CellSize,
            validation,
            false));
    }

    private void ClearPaintStroke(bool notify)
    {
        _isPainting = false;
        _hasLastPaintCell = false;
        _hasPaintCursorCell = false;
        _paintStroke.Clear();
        _paintStrokeSet.Clear();

        if (notify)
            ConstructionEvents.PaintPlacementPreviewEnded();
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
        if (_isGamePaused)
            return;

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
                RunBuildActionAndCancel(() => construction.TryRepair(_menuCell));
                break;
            case ActionId.Upgrade:
                RunBuildActionAndCancel(() => construction.TryUpgrade(_menuCell));
                break;
            case ActionId.Demolish:
                RunBuildActionAndCancel(() => construction.TryDemolish(_menuCell));
                break;
        }
    }

    private void RunBuildActionAndCancel(System.Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            CancelToIdle();
        }
    }

    // While hovering the Upgrade entry, show the post-upgrade stat preview on the
    // tower indicator; revert to the current stats when the cursor moves away.
    private void HandleRadialEntryHovered(RadialMenu.Entry? entry)
    {
        if (_isGamePaused)
            return;

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
            bool canBegin = construction.CanBeginPlacement(data, out string reason);
            string subLabel = canBegin ? $"{data.buyCost}g" : reason;
            entries.Add(new RadialMenu.Entry(data.buildingName, subLabel, canBegin, data));
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
            tower.TryPreviewNextUpgrade(out int nextDamage, out int nextRange, out float nextFireRate))
        {
            UpgradeLevelData upgrade = building.Data.upgradeLevels[building.CurrentLevel];
            var towerParts = new List<string> { $"{upgradeCost}g" };
            if (nextDamage - tower.AttackDamage > 0) towerParts.Add($"+{nextDamage - tower.AttackDamage} DMG");
            if (nextRange - tower.AttackRange > 0) towerParts.Add($"+{nextRange - tower.AttackRange} RNG");
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
