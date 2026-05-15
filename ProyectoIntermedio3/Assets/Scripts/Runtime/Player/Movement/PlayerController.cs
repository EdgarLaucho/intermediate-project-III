using UnityEngine;
using UnityEngine.AI;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager grid;

    private NavMeshAgent _agent;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        InputEvents.OnPrimaryPressed += HandlePrimaryPressed;
    }

    private void OnDisable()
    {
        InputEvents.OnPrimaryPressed -= HandlePrimaryPressed;
    }

    private void HandlePrimaryPressed()
    {
        if (grid == null)
            return;

        if (!_agent.isOnNavMesh)
            return;

        if (!TryGetMouseWorldPosition(out Vector3 worldPos))
            return;

        Vector2Int gridCoords = grid.WorldToGrid(worldPos);

        GridCell cell = grid.GetCell(gridCoords);

        if (cell == null)
            return;

        if (cell.IsOccupied)
            return;

        Vector3 targetPosition = grid.GridToWorld(gridCoords);

        targetPosition.y = transform.position.y;

        _agent.SetDestination(targetPosition);
    }

    private bool TryGetMouseWorldPosition(out Vector3 worldPos)
    {
        Camera cam = Camera.main;

        Ray ray = cam.ScreenPointToRay(
            UnityEngine.InputSystem.Mouse.current.position.ReadValue());

        Plane plane = new Plane(Vector3.up, Vector3.zero);

        if (plane.Raycast(ray, out float enter))
        {
            worldPos = ray.GetPoint(enter);
            return true;
        }

        worldPos = Vector3.zero;
        return false;
    }
}