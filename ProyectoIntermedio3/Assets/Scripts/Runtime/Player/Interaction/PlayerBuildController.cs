using UnityEngine;
using UnityEngine.AI;

public class PlayerBuildController : MonoBehaviour
{
    private enum BuildState
    {
        Idle,
        MovingToSite,
        Constructing
    }

    #region Inspector

    [SerializeField] private GridManager grid;
    [SerializeField] private BuildManager buildManager;

    [Header("Build Feedback")]
    [SerializeField] private float buildDuration = 2f;

    #endregion

    #region State

    private NavMeshAgent _agent;

    private Vector2Int _targetBuildCell;
    private BuildingData _pendingBuilding;

    private BuildState _state;
    private float _buildTimer;

    // Neighboring addresses used to search for a valid mailbox
    // around the site where it will be built.
    private static readonly Vector2Int[] AdjacentDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right,

        new Vector2Int(1, 1),
        new Vector2Int(-1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, -1)
    };

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        GameplayEvents.OnBuildRequested += HandleBuildRequest;
    }

    private void OnDisable()
    {
        GameplayEvents.OnBuildRequested -= HandleBuildRequest;
    }

    private void Update()
    {
        switch (_state)
        {
            case BuildState.Idle:
                break;

            case BuildState.MovingToSite:
                UpdateMovementToSite();
                break;

            case BuildState.Constructing:
                UpdateConstruction();
                break;
        }
    }

    #endregion

    #region Build Flow

    private void HandleBuildRequest(Vector2Int coords, BuildingData data)
    {
        if (!_agent.isOnNavMesh)
            return;

        _targetBuildCell = coords;
        _pendingBuilding = data;

        MoveToBuildPosition(coords);

        _state = BuildState.MovingToSite;
    }

    private void MoveToBuildPosition(Vector2Int buildCell)
    {
        Vector2Int adjacentCell = FindBestAdjacentCell(buildCell);
        Vector3 worldPosition = grid.GridToWorld(adjacentCell);

        worldPosition.y = transform.position.y;
        _agent.SetDestination(worldPosition);
    }

    private void UpdateMovementToSite()
    {
        if (_agent.pathPending)
            return;

        if (_agent.remainingDistance > _agent.stoppingDistance)
            return;

        StartConstruction();
    }

    private void StartConstruction()
    {
        _state = BuildState.Constructing;
        _buildTimer = buildDuration;

        // TODO-Reproducir sonido
        // TODO-Mostrar icono martillo
        // TODO-Mostrar barra circular
    }

    private void UpdateConstruction()
    {
        _buildTimer -= Time.deltaTime;

        if (_buildTimer > 0f)
        {
            // TODO-Actualizar barra circular
            return;
        }

        CompleteBuild();
    }

    private void CompleteBuild()
    {
        if (_pendingBuilding == null)
        {
            CancelBuild();
            return;
        }

        bool success = buildManager.TryBuild(
            _targetBuildCell,
            _pendingBuilding);

        if (!success)
        {
            Debug.LogWarning(
                $"[PlayerBuildController] Failed to build at {_targetBuildCell}");
        }

        CancelBuild();
    }

    private void CancelBuild()
    {
        _state = BuildState.Idle;
        _pendingBuilding = null;
    }

    #endregion

    #region Cell Search

    private Vector2Int FindBestAdjacentCell(Vector2Int buildCell)
    {
        Vector2Int bestCell = buildCell;
        float bestDistance = float.MaxValue;

        foreach (Vector2Int direction in AdjacentDirections)
        {
            Vector2Int candidate = buildCell + direction;
            GridCell cell = grid.GetCell(candidate);

            if (cell == null)
                continue;

            if (cell.IsOccupied)
                continue;

            if (!cell.IsBuildable)
                continue;

            Vector3 worldPos = grid.GridToWorld(candidate);
            float distance = Vector3.Distance(transform.position, worldPos);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCell = candidate;
            }
        }

        return bestCell;
    }

    #endregion

}