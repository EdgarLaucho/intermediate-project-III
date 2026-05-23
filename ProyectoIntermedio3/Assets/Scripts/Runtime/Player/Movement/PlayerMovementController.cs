using UnityEngine;
using UnityEngine.AI;

public class PlayerMovementController : MonoBehaviour
{
    [SerializeField] private GridManager grid;
    [SerializeField] private ConstructionPresenter constructionPresenter;
    [SerializeField] private LayerMask enemyLayer;
    
    private NavMeshAgent _agent;
    private PlayerCombat combat;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        combat = GetComponent<PlayerCombat>();
    }

    private void OnEnable()
    {
        InputEvents.OnPrimaryPressed += HandleMoveCommand;
    }

    private void OnDisable()
    {
        InputEvents.OnPrimaryPressed -= HandleMoveCommand;
    }

    private void Update()
    {
        if (!_agent.pathPending &&
            _agent.remainingDistance <= _agent.stoppingDistance &&
            !_agent.hasPath)
        {
            combat.EnableAutoCombat();
        }
    }

    private void HandleMoveCommand(Vector3 worldPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(
            UnityEngine.InputSystem.Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, enemyLayer))
        {
            combat.SetTarget(hit.transform);
            return;
        }

        if (constructionPresenter != null && constructionPresenter.IsPlacing)
            return;

        if (!_agent.isOnNavMesh)
            return;

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
}