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

    [Header("Corner Piece")]
    // Optional prefab used on diagonal cells (|x|==|y|).
    // Leave null to reuse the normal prefab on corners.
    public GameObject cornerPrefab;
    // Extra yaw applied to the corner prefab if its forward direction differs from the normal one.
    public float cornerPlacementYawOffset = 0f;
}