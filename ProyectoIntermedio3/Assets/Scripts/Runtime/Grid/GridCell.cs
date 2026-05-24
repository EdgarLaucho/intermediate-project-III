using UnityEngine;

public class GridCell
{
    #region Properties

    public Vector2Int Coordinates { get; }
    public bool IsBuildable { get; set; }
    public bool IsOccupiedByNexus { get; set; }
    public bool IsOccupied => CurrentBuilding != null;

    public BuildingBase CurrentBuilding { get; private set; }

    #endregion

    #region Constructor

    public GridCell(Vector2Int coordinates, bool isBuildable = true)
    {
        Coordinates = coordinates;
        IsBuildable = isBuildable;
    }

    #endregion

    #region Methods

    public void SetBuilding(BuildingBase building)
    {
        CurrentBuilding = building;
    }

    public void ClearBuilding()
    {
        CurrentBuilding = null;
    }

    #endregion
}