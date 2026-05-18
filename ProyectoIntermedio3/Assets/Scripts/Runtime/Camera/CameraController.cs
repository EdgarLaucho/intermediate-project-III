using UnityEngine;

public class CameraController : MonoBehaviour
{
    public enum CameraMode
    {
        Follow,
        Free
    }

    [Header("References")]
    [SerializeField] private Transform target;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float edgeSize = 15f;

    [Header("Follow")]
    [SerializeField] private float followSmoothness = 8f;

    [Header("Offset")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 14f, -8f);

    [Header("State")]
    [SerializeField] private CameraMode mode = CameraMode.Follow;

    private Vector2 _moveInput;

    #region Unity Lifecycle

    private void OnEnable()
    {
        InputEvents.OnMoveCamera += HandleCameraMove;
        InputEvents.OnToggleCameraFollow += ToggleFollowMode;
    }

    private void OnDisable()
    {
        InputEvents.OnMoveCamera -= HandleCameraMove;
        InputEvents.OnToggleCameraFollow -= ToggleFollowMode;
    }

    private void Update()
    {
        if (mode == CameraMode.Free)
        {
            HandleFreeMovement();
        }
    }

    private void LateUpdate()
    {
        if (mode != CameraMode.Follow || target == null)
            return;

        HandleFollowMovement();
    }

    #endregion

    #region Input Handlers

    private void HandleCameraMove(Vector2 input)
    {
        _moveInput = input;
    }

    private void ToggleFollowMode()
    {
        mode = mode == CameraMode.Follow
            ? CameraMode.Free
            : CameraMode.Follow;
    }

    #endregion

    #region Movement

    private void HandleFreeMovement()
    {
        Vector3 moveDirection = Vector3.zero;

        moveDirection.x = _moveInput.x;
        moveDirection.z = _moveInput.y;

        // Edge scrolling
        Vector3 mousePos = Input.mousePosition;

        if (mousePos.x >= Screen.width - edgeSize)
            moveDirection.x += 1f;

        if (mousePos.x <= edgeSize)
            moveDirection.x -= 1f;

        if (mousePos.y >= Screen.height - edgeSize)
            moveDirection.z += 1f;

        if (mousePos.y <= edgeSize)
            moveDirection.z -= 1f;

        moveDirection.Normalize();

        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    private void HandleFollowMovement()
    {
        Vector3 desiredPosition = target.position + offset;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSmoothness * Time.deltaTime);
    }

    #endregion

    #region Public API

    public void FocusTarget()
    {
        if (target == null)
            return;

        transform.position = target.position + offset;
    }

    public void SetMode(CameraMode newMode)
    {
        mode = newMode;
    }

    #endregion

}