using UnityEngine;

public class WeaponPickup : MonoBehaviour, IPickable
{
    [SerializeField] private WeaponData weaponData;

    public void PickUp(GameObject picker)
    {
        PlayerWeaponController weaponController =
            picker.GetComponent<PlayerWeaponController>();

        if (weaponController == null)
            return;

        weaponController.EquipWeapon(weaponData);

        Destroy(gameObject);
    }
}