using UnityEngine;
using UnityEngine.AI;

public class PlayerMovementController : MonoBehaviour
{
    [SerializeField] private GridManager grid;
    [SerializeField] private BuildModeManager constructionPresenter;
    [SerializeField] private LayerMask enemyLayer;
    
    private NavMeshAgent _agent;
    private PlayerCombat combat;
    private int _lastBuildPlacedFrame = -1;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        combat = GetComponent<PlayerCombat>();

        if (grid == null)
            grid = FindAnyObjectByType<GridManager>(FindObjectsInactive.Exclude);

        if (constructionPresenter == null)
            constructionPresenter = FindAnyObjectByType<BuildModeManager>(FindObjectsInactive.Exclude);
    }

    private void OnEnable()
    {
        InputEvents.OnPrimaryPressed += HandleMoveCommand;
        ConstructionEvents.OnBuildingPlaced += HandleBuildingPlaced;
    }

    private void OnDisable()
    {
        InputEvents.OnPrimaryPressed -= HandleMoveCommand;
        ConstructionEvents.OnBuildingPlaced -= HandleBuildingPlaced;
    }

    private void Update()
    {
        if (GamePauseEvents.IsPaused)
            return;

        if (!_agent.pathPending &&
            _agent.remainingDistance <= _agent.stoppingDistance &&
            !_agent.hasPath)
        {
            combat.EnableAutoCombat();
        }
    }

    private void HandleMoveCommand(Vector3 worldPos)
    {
        if (GamePauseEvents.IsPaused)
            return;

        if (constructionPresenter != null && constructionPresenter.IsPlacing)
            return;

        if (_lastBuildPlacedFrame == Time.frameCount)
            return;

        if (_agent == null || !_agent.isOnNavMesh || grid == null)
            return;

        if (Camera.main == null || UnityEngine.InputSystem.Mouse.current == null)
            return;

        Ray ray = Camera.main.ScreenPointToRay(
            UnityEngine.InputSystem.Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, enemyLayer))
        {
            combat.SetTarget(hit.transform);
            return;
        }

        Vector2Int gridCoords = grid.WorldToGrid(worldPos);

        GridCell cell = grid.GetCell(gridCoords);

        if (cell == null || cell.IsOccupied)
            return;

        Vector3 targetPosition = grid.GridToWorld(gridCoords);

        targetPosition.y = transform.position.y;

        combat.StopCombat();

        _agent.SetDestination(targetPosition);

        GameplayEvents.MoveCommandIssued(targetPosition);
    }

    private void HandleBuildingPlaced(BuildingActionArgs args)
    {
        _lastBuildPlacedFrame = Time.frameCount;
    }
}
