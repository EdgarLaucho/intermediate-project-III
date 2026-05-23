using UnityEngine;

public class PlayerWeaponController : MonoBehaviour
{
    [SerializeField]
    private WeaponData startingWeapon;

    private WeaponData currentWeapon;

    private float weaponTimer;
    private bool usingTemporaryWeapon;

    public WeaponData CurrentWeapon => currentWeapon;

    private void Awake()
    {
        currentWeapon = startingWeapon;
    }

    private void Update()
    {
        if (GamePauseEvents.IsPaused)
            return;

        HandleWeaponTimer();
    }

    public void EquipWeapon(WeaponData newWeapon, float duration)
    {
        currentWeapon = newWeapon;
        weaponTimer = duration;
        usingTemporaryWeapon = true;

        Debug.Log($"Equipped weapon: {newWeapon.weaponName}");
    }

    private void HandleWeaponTimer()
    {
        if (!usingTemporaryWeapon)
            return;

        weaponTimer -= Time.deltaTime;

        if (weaponTimer > 0f)
            return;

        ReturnToDefaultWeapon();
    }

    private void ReturnToDefaultWeapon()
    {
        currentWeapon = startingWeapon;
        usingTemporaryWeapon = false;

        Debug.Log("Weapon expired.");
    }
}