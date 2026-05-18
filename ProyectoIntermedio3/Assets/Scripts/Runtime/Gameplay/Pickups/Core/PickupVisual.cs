using UnityEngine;

public class PickupVisual : MonoBehaviour
{
    [SerializeField] private float itemLifeTime = -1f;
    [SerializeField] private bool pulseBeforeDestroy = true;

    private float timer;
    private Vector3 initialScale;

    private void Start()
    {
        timer = itemLifeTime;
        initialScale = transform.localScale;
    }

    private void Update()
    {
        if (itemLifeTime <= 0f)
            return;

        timer -= Time.deltaTime;

        if (pulseBeforeDestroy && timer < 3f)
        {
            float scale =
                Mathf.PingPong(Time.time * 5f, 0.5f) + 0.5f;

            transform.localScale = initialScale * scale;
        }

        if (timer <= 0f)
        {
            Destroy(gameObject);
        }
    }
}