using UnityEngine;

// Computes the world-space placement rotation for a building given the grid cell
// it is being placed on. The nexus sits at grid (0,0).
public static class BuildingRotationHelper
{
    // Returns the Quaternion to use when spawning or previewing a building.
    // For buildings with autoOrientRadially the yaw is derived from the angle
    // between the nexus and the target cell, snapped to the nearest 45°.
    public static Quaternion ComputePlacementRotation(Vector2Int coords, BuildingData data)
    {
        float yaw = data.placementYawOffset;

        if (data.autoOrientRadially && (coords.x != 0 || coords.y != 0))
        {
            // In Unity, yaw=0 faces +Z (world forward). To face away from nexus at
            // (worldX, worldZ) = (coords.x, coords.y), use Atan2(x, z).
            float rawAngle = Mathf.Atan2(coords.x, coords.y) * Mathf.Rad2Deg;

            // Diagonal cells (|x| == |y|) snap to 45°; all other cells snap to 90°
            // so cardinal positions always get a clean 0/90/180/270° rotation.
            float snapStep = (Mathf.Abs(coords.x) == Mathf.Abs(coords.y)) ? 45f : 90f;
            float snapped = Mathf.Round(rawAngle / snapStep) * snapStep;

            yaw += snapped;
        }

        return Quaternion.Euler(0f, yaw, 0f);
    }

    // Returns true when the cell sits on an exact diagonal from the nexus (|x|==|y|, both non-zero).
    public static bool IsCornerCell(Vector2Int coords)
        => coords.x != 0 && coords.y != 0 && Mathf.Abs(coords.x) == Mathf.Abs(coords.y);

    // Returns the prefab that should be instantiated for a given cell.
    // Uses the corner prefab when on a diagonal cell and WallData has one assigned.
    public static GameObject ResolvePrefab(Vector2Int coords, BuildingData data)
    {
        if (IsCornerCell(coords) && data is WallData wallData && wallData.cornerPrefab != null)
            return wallData.cornerPrefab;
        return data.prefab;
    }

    // Like ComputePlacementRotation but uses cornerPlacementYawOffset (not placementYawOffset)
    // for corner cells, so the two prefabs are tuned independently in the Inspector.
    public static Quaternion ComputePlacementRotationForCell(Vector2Int coords, BuildingData data)
    {
        if (IsCornerCell(coords) && data is WallData wd && wd.cornerPrefab != null)
        {
            // Radial angle only — does NOT include the normal wall's placementYawOffset.
            float rawAngle = Mathf.Atan2(coords.x, coords.y) * Mathf.Rad2Deg;
            float snapped = Mathf.Round(rawAngle / 45f) * 45f;
            return Quaternion.Euler(0f, snapped + wd.cornerPlacementYawOffset, 0f);
        }
        return ComputePlacementRotation(coords, data);
    }
}