using UnityEngine;
using System;

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
    public int CurrentLevel { get; protected set; }
    public int TotalGoldInvested { get; internal set; }

    private GridCell _boundCell;
    private Vector2Int _boundCoords;
    private bool _hasBoundCell;

    #endregion

    #region Derived Queries

    public bool IsMaxLevel => Data != null && CurrentLevel >= Data.maxLevel;

    public int GetRepairCost()
    {
        if (Data == null) return 0;
        var missing = MaxHealth - CurrentHealth;
        return Mathf.CeilToInt(missing * Data.repairCostPerHP);
    }

    public int GetRepairCost(int healAmount)
    {
        return Mathf.CeilToInt(healAmount * Data.repairCostPerHP);
    }

    public int GetUpgradeCost()
    {
        if (IsMaxLevel || Data == null) return 0;
        return Data.upgradeLevels[CurrentLevel].upgradeCost;
    }

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

        var upgradeData = Data.upgradeLevels[CurrentLevel];

        MaxHealth += upgradeData.bonusMaxHealth;
        CurrentHealth = Mathf.Min(CurrentHealth + upgradeData.bonusMaxHealth, MaxHealth);

        ApplyUpgradeStats(upgradeData);
        CurrentLevel++;
        OnUpgraded?.Invoke(this, upgradeData);
    }

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