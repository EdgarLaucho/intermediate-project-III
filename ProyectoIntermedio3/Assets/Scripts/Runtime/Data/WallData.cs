using UnityEngine;

public enum WallRole
{
    Wooden,
    Thorned,
}

[CreateAssetMenu(fileName = "NewWallData", menuName = "TD/Building Data/Wall")]
public class WallData : BuildingData
{
    public override BuildingCategory Category => BuildingCategory.Wall;

    [Header("Wall Stats")]
    public WallRole role = WallRole.Wooden;
    public int armor;
    public int thornsDamage;
}
