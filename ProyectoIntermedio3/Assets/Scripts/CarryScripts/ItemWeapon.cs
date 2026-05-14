using UnityEngine;

public class ItemWeapon : MonoBehaviour, IPickable
{
    public void PickUp()
    {
        Debug.Log("Weapon Picked!");
        Destroy(gameObject);
    }
}