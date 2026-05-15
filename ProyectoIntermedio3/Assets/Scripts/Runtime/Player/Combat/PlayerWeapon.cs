using UnityEngine;

public class PlayerWeapon : MonoBehaviour
{
    [SerializeField] private WeaponData startingWeapon;

    public WeaponData CurrentWeapon { get; private set; }

    private void Awake()
    {
        EquipWeapon(startingWeapon);
    }

    public void EquipWeapon(WeaponData weapon)
    {
        if (weapon == null)
            return;

        CurrentWeapon = weapon;

        Debug.Log($"Equipped: {weapon.weaponName}");
    }
}