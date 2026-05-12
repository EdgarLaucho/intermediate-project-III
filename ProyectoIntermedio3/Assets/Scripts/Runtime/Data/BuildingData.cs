using UnityEngine;

public enum BuildingCategory { Tower, Trap, Wall }

[System.Serializable]
public struct UpgradeLevelData
{
    public int upgradeCost; // gold required to apply this upgrade
    public int bonusMaxHealth; // HP added to MaxHealth on upgrade
    public float statMultiplier; // multiplier applied to type-specific stats (damage, range…)
    public float fireRateMultiplier; // towers only; 0/1 = unchanged
    public int bonusTrapUses; // traps only
    public int bonusRange; // towers/traps; additive cell-range bonus
    public int bonusArmor; // walls only
    public int bonusThornsDamage; // walls only
    public int bonusProjectilesPerAttack; // towers only
    public GameObject visualPrefabOverride; // optional visual replacement applied by upgrade
    public string specialLabel; // design note for the final tier or unique upgrade
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
    public float refundPercentage = 0.5f; // fraction of TotalGoldInvested returned on demolish
    public float repairCostPerHP = 0.5f; // gold per 1 HP restored

    [Header("Health")]
    public int baseMaxHealth = 100;

    [Header("Placement Rotation")]
    // When true, the building auto-rotates to face away from the nexus (snap to 45°).
    public bool autoOrientRadially = false;
    // Extra yaw applied after radial orientation (use to fix prefab forward direction).
    public float placementYawOffset = 0f;

    [Header("Placement Behaviour")]
    // When true, holding left-click and dragging places this building on every
    // new valid cell the cursor crosses (paint / brush mode).
    public bool allowPaintPlacement = false;

    [Header("Upgrades")]
    public int maxLevel = 3;
    public UpgradeLevelData[] upgradeLevels; // length must equal maxLevel
}