using UnityEngine;

public enum BuildingCategory { Tower, Trap, Wall }

[System.Serializable]
public struct UpgradeLevelData
{
    public int upgradeCost;
    public int bonusMaxHealth;
    public float statMultiplier;
    public float fireRateMultiplier;
    public int bonusTrapUses;
    public int bonusRange;
    public int bonusArmor;
    public int bonusThornsDamage;
    public int bonusProjectilesPerAttack;
    public GameObject visualPrefabOverride;
    public string specialLabel;
}

public class BuildingData : ScriptableObject
{
    [Header("Identity")]
    public string buildingName;
    public GameObject prefab;

    public virtual BuildingCategory Category => BuildingCategory.Wall;

    [Header("Economy")]
    public int buyCost;
    [Range(0f, 1f)]
    public float refundPercentage = 0.5f;
    public float repairCostPerHP = 0.5f;

    [Header("Health")]
    public int baseMaxHealth = 100;

    [Header("Placement Rotation")]
    public bool autoOrientRadially = false;
    public float placementYawOffset = 0f;

    [Header("Placement Behaviour")]
    public bool allowPaintPlacement = false;

    [Header("Upgrades")]
    public int maxLevel = 3;
    public UpgradeLevelData[] upgradeLevels;
}