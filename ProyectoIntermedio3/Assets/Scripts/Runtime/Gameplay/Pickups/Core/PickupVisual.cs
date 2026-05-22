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

    private float timer;
    private Vector3 startPosition;
    private Vector3 initialScale;

    private void Start()
    {
        timer = itemLifeTime;
        startPosition = transform.position;
        initialScale = transform.localScale;
    }

    private void Update()
    {
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
        }

        if (timer <= 0f)
        {
            Destroy(gameObject);
        }
    }
}