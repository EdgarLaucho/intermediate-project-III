using UnityEngine;

public static class BuildingRotationHelper
{
    public static Quaternion ComputePlacementRotation(Vector2Int coords, BuildingData data)
    {
        var yaw = data.placementYawOffset;

        if (data.autoOrientRadially && (coords.x != 0 || coords.y != 0))
        {
            var rawAngle = Mathf.Atan2(coords.x, coords.y) * Mathf.Rad2Deg;

            var snapStep = (Mathf.Abs(coords.x) == Mathf.Abs(coords.y)) ? 45f : 90f;
            var snapped = Mathf.Round(rawAngle / snapStep) * snapStep;

            yaw += snapped;
        }

        return Quaternion.Euler(0f, yaw, 0f);
    }

    public static bool IsCornerCell(Vector2Int coords)
        => coords.x != 0 && coords.y != 0 && Mathf.Abs(coords.x) == Mathf.Abs(coords.y);

    public static GameObject ResolvePrefab(Vector2Int coords, BuildingData data)
    {
        if (IsCornerCell(coords) && data is WallData wallData && wallData.cornerPrefab != null)
            return wallData.cornerPrefab;
        return data.prefab;
    }

    public static Quaternion ComputePlacementRotationForCell(Vector2Int coords, BuildingData data)
    {
        if (IsCornerCell(coords) && data is WallData wd && wd.cornerPrefab != null)
        {
            var rawAngle = Mathf.Atan2(coords.x, coords.y) * Mathf.Rad2Deg;
            var snapped = Mathf.Round(rawAngle / 45f) * 45f;
            return Quaternion.Euler(0f, snapped + wd.cornerPlacementYawOffset, 0f);
        }
        
        return ComputePlacementRotation(coords, data);
    }
}