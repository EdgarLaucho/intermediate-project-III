using UnityEngine;

public class ItemBullet : MonoBehaviour, IPickable
{
    public void PickUp()
    {
        Debug.Log("Bullet Picked!");
        Destroy(gameObject);
    }
}