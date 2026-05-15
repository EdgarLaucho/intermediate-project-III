using UnityEngine;

public class ItemFood : MonoBehaviour, IPickable
{
    [SerializeField] private float itemLifeTime = 10f;
    private float foodTimer;

    void Start()
    {
        foodTimer = itemLifeTime;
    }

    void Update()
    {
        foodTimer -= Time.deltaTime;

        if (foodTimer < 3f)
        {
            float scale = Mathf.PingPong(Time.time * 5, 0.5f) + 0.5f;
            transform.localScale = Vector3.one * scale;
        }

        if (foodTimer <= 0)
        {
            LosingFood();
        }
    }

    public void PickUp()
    {
        Debug.Log("Food Collected!");
        Destroy(gameObject);
    }

    private void LosingFood()
    {
        Debug.Log("Food Lost!");
        Destroy(gameObject);
    }
}