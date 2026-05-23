using UnityEngine;

public class PickupVisual : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField]
    private float itemLifeTime = -1f;

    [SerializeField]
    private bool pulseBeforeDestroy = true;

    [Header("Floating")]
    [SerializeField]
    private float floatHeight = 0.25f;

    [SerializeField]
    private float floatSpeed = 2f;

    [Header("Rotation")]
    [SerializeField]
    private float rotationSpeed = 60f;

    [Header("Flashing Material")]
    [SerializeField]
    private Renderer itemRenderer;
    [SerializeField]
    private Material flashMaterial;

    private float timer;
    private Vector3 startPosition;
    private Vector3 initialScale;
    private Material originalMaterial;

    private void Start()
    {
        timer = itemLifeTime;
        startPosition = transform.position;
        initialScale = transform.localScale;

        if (itemRenderer == null)
        {
            itemRenderer = GetComponentInChildren<Renderer>();
        }

        if (itemRenderer != null)
        {
            originalMaterial = itemRenderer.material;
        }
    }

    private void Update()
    {
        if (GamePauseEvents.IsPaused)
            return;

        HandleFloating();
        HandleRotation();
        HandleLifetime();
    }

    private void HandleFloating()
    {
        Vector3 position = startPosition;
        position.y += Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = position;
    }

    private void HandleRotation()
    {
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
    }

    private void HandleLifetime()
    {
        if (itemLifeTime <= 0f)
            return;

        timer -= Time.deltaTime;

        if (pulseBeforeDestroy && timer < 3f)
        {
            float scale = Mathf.PingPong(Time.time * 5f, 0.5f) + 0.5f;
            transform.localScale = initialScale * scale;

            if (itemRenderer != null && flashMaterial != null)
            {
                bool shouldFlash = Mathf.PingPong(Time.time * 5f, 1f) > 0.5f;
                itemRenderer.material = shouldFlash ? flashMaterial : originalMaterial;
            }
        }

        if (timer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (originalMaterial != null)
        {
            Destroy(originalMaterial);
        }
    }
}