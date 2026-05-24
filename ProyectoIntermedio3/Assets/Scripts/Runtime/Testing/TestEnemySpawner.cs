using UnityEngine;
using UnityEngine.InputSystem;

public class TestEnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Transform spawnParent;
    [SerializeField] private float spawnPlaneY = 0f;
    [SerializeField] private float spawnHeightOffset = 0f;

    private Camera _camera;

    private void Awake()
    {
        _camera = gameplayCamera != null ? gameplayCamera : Camera.main;
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.spaceKey.wasPressedThisFrame) return;
        if (enemyPrefab == null || !TryGetSpawnPoint(out Vector3 spawnPoint)) return;

        Instantiate(enemyPrefab, spawnPoint, Quaternion.identity, spawnParent);
    }

    private bool TryGetSpawnPoint(out Vector3 spawnPoint)
    {
        Camera cameraToUse = _camera != null ? _camera : Camera.main;
        if (cameraToUse == null || Mouse.current == null)
        {
            spawnPoint = Vector3.zero;
            return false;
        }

        Ray ray = cameraToUse.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Mathf.Abs(ray.direction.y) <= 0.0001f)
        {
            spawnPoint = Vector3.zero;
            return false;
        }

        float distance = (spawnPlaneY - ray.origin.y) / ray.direction.y;
        if (distance <= 0f)
        {
            spawnPoint = Vector3.zero;
            return false;
        }

        spawnPoint = ray.origin + ray.direction * distance;
        spawnPoint.y += spawnHeightOffset;
        return true;
    }
}