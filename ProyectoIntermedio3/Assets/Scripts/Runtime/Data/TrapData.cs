using UnityEngine;

public enum TrapRole
{
    Spikes,
    Tar,
    Mine,
}

[System.Serializable]
public struct TrapStatsData
{
    public int triggerDamage;
    public float cooldown;
    public int maxUses;
    public int effectRadius;
    public float effectDuration;
    [Range(0f, 1f)] public float slowPercent;

    public bool HasValidValues => triggerDamage >= 0 && cooldown >= 0f && maxUses > 0 && effectRadius >= 0;
}

[CreateAssetMenu(fileName = "NewTrapData", menuName = "TD/Building Data/Trap")]
public class TrapData : BuildingData
{
    public override BuildingCategory Category => BuildingCategory.Trap;

    [Header("Trap Stats")]
    public TrapRole role = TrapRole.Spikes;
    public TrapStatsData trapStats = new TrapStatsData
    {
        triggerDamage = 20,
        cooldown = 3f,
        maxUses = 5,
        effectRadius = 0,
        effectDuration = 0f,
        slowPercent = 0f,
    };
}