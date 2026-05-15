using UnityEngine;

public class PlayerStateController : MonoBehaviour
{
    public bool IsBuilding { get; private set; }

    private void OnEnable()
    {
        ConstructionEvents.OnPlacementStarted += HandlePlacementStarted;
        ConstructionEvents.OnPlacementEnded += HandlePlacementEnded;
    }

    private void OnDisable()
    {
        ConstructionEvents.OnPlacementStarted -= HandlePlacementStarted;
        ConstructionEvents.OnPlacementEnded -= HandlePlacementEnded;
    }

    private void HandlePlacementStarted(BuildingData _)
    {
        IsBuilding = true;
    }

    private void HandlePlacementEnded()
    {
        IsBuilding = false;
    }
}