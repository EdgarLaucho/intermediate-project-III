using UnityEngine;

public class HealthPickup : MonoBehaviour, IPickable
{
    [SerializeField] private int healAmount = 25;

    public void PickUp(GameObject picker)
    {
        Health health = picker.GetComponent<Health>();

        if (health == null)
            return;

        health.Heal(healAmount);

        Destroy(gameObject);
    }
}