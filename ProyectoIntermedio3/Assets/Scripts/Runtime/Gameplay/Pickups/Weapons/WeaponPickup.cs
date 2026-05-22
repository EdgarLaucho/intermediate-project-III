using UnityEngine;

public class WeaponPickup : MonoBehaviour, IPickable
{
    [SerializeField] private WeaponData weaponData;

    [SerializeField] private float weaponDuration = 15f;

    public void PickUp(GameObject picker)
    {
        PlayerWeaponController weaponController =
            picker.GetComponent<PlayerWeaponController>();

        if (weaponController == null)
            return;

        weaponController.EquipWeapon(weaponData, weaponDuration);

        Destroy(gameObject);
    }
}