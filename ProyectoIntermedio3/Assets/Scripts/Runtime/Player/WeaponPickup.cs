using UnityEngine;

public class WeaponPickup : MonoBehaviour
{
    [SerializeField] private WeaponData weapon;

    private void OnTriggerEnter(Collider other)
    {
        PlayerWeapon playerWeapon =
            other.GetComponent<PlayerWeapon>();

        if (playerWeapon == null)
            return;

        playerWeapon.EquipWeapon(weapon);

        Destroy(gameObject);
    }
}