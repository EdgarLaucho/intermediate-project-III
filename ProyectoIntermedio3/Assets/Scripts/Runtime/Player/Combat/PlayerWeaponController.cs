using UnityEngine;

public class PlayerWeaponController : MonoBehaviour
{
    [SerializeField] private WeaponData startingWeapon;

    private WeaponData currentWeapon;

    public WeaponData CurrentWeapon => currentWeapon;

    private void Awake()
    {
        currentWeapon = startingWeapon;
    }

    public void EquipWeapon(WeaponData newWeapon)
    {
        currentWeapon = newWeapon;

        Debug.Log($"Equipped weapon: {newWeapon.weaponName}");
    }
}