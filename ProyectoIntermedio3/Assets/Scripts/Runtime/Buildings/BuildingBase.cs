using UnityEngine;
using System;

// Base class for every placeable structure in the game.
// Implements IDamageable so towers, traps, and walls can all receive damage through
// the same interface without knowing each other's concrete types.
public class BuildingBase : MonoBehaviour, IDamageable
{
    #region IDamageable Interface

    public bool IsAlive => CurrentHealth > 0;
    public event Action<IDamageable> OnDeath;
    public event Action OnHealthChanged;

    #endregion

    #region Events

    public event Action<BuildingBase, UpgradeLevelData> OnUpgraded;

    #endregion

    #region Runtime State

    public BuildingData Data { get; private set; }

    public int CurrentHealth { get; protected set; }
    public int MaxHealth { get; protected set; }
    // 0 = base state; each call to Upgrade() increments this by 1.
    public int CurrentLevel { get; protected set; }
    public int TotalGoldInvested { get; internal set; }

    // Cached grid placement – populated by BuildingManager right after the building is placed.
    private GridCell _boundCell;
    private Vector2Int _boundCoords;
    private bool _hasBoundCell;

    #endregion

    #region Derived Queries

    public bool IsMaxLevel => Data != null && CurrentLevel >= Data.maxLevel;

    // Returns the gold cost to fully restore this building to MaxHealth.
    public int GetRepairCost()
    {
        if (Data == null) return 0;
        int missing = MaxHealth - CurrentHealth;
        return Mathf.CeilToInt(missing * Data.repairCostPerHP);
    }

    // Overload that prices only a partial heal amount.
    public int GetRepairCost(int healAmount)
    {
        return Mathf.CeilToInt(healAmount * Data.repairCostPerHP);
    }

    public int GetUpgradeCost()
    {
        if (IsMaxLevel || Data == null) return 0;
        return Data.upgradeLevels[CurrentLevel].upgradeCost;
    }

    // Refund is floored so the player never receives a fractional gold windfall.
    public int GetRefundAmount()
    {
        return Mathf.FloorToInt(TotalGoldInvested * Data.refundPercentage);
    }

    #endregion

    #region Lifecycle

    public virtual void Initialize(BuildingData data)
    {
        Data = data;
        MaxHealth = data.baseMaxHealth;
        CurrentHealth = MaxHealth;
        CurrentLevel = 0;
        TotalGoldInvested = data.buyCost;
        EnsureLevelIndicator();
    }

    // Called by the grid system right after placement so the building knows which
    // cell it occupies and can clear it on death.
    internal void BindToGridCell(Vector2Int coords, GridCell cell)
    {
        _boundCoords = coords;
        _boundCell = cell;
        _hasBoundCell = cell != null;
    }

    #endregion

    #region Actions

    public virtual void TakeDamage(int amount)
    {
        if (!IsAlive) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke();
        if (!IsAlive) DieFromDamage();
    }

    public void Heal(int amount)
    {
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke();
    }

    public void Upgrade()
    {
        if (IsMaxLevel) return;

        if (Data.upgradeLevels == null || CurrentLevel >= Data.upgradeLevels.Length)
        {
            Debug.LogError($"[BuildingBase] '{Data.buildingName}': upgradeLevels array is too short (length {Data.upgradeLevels?.Length ?? 0}, expected at least {CurrentLevel + 1}). Check BuildingData asset.");
            return;
        }

        UpgradeLevelData upgradeData = Data.upgradeLevels[CurrentLevel];

        // Topping up current health by the same bonus gives the player a small
        // immediate reward for upgrading mid-combat.
        MaxHealth += upgradeData.bonusMaxHealth;
        CurrentHealth = Mathf.Min(CurrentHealth + upgradeData.bonusMaxHealth, MaxHealth);

        ApplyUpgradeStats(upgradeData);
        CurrentLevel++;
        OnUpgraded?.Invoke(this, upgradeData);
    }

    // Override in subclasses to apply type-specific stat changes (damage, range, etc.).
    protected virtual void ApplyUpgradeStats(UpgradeLevelData upgradeData) { }

    #endregion

    #region Private Helpers

    private void EnsureLevelIndicator()
    {
        if (!TryGetComponent(out BuildingLevelIndicator indicator))
            indicator = gameObject.AddComponent<BuildingLevelIndicator>();

        indicator.Initialize(this);
    }

    private void DieFromDamage()
    {
        if (_hasBoundCell)
        {
            _boundCell?.ClearBuilding();
            ConstructionEvents.BuildingDemolished(_boundCoords);
        }

        OnDeath?.Invoke(this);
        Destroy(gameObject);
    }

    #endregion
}