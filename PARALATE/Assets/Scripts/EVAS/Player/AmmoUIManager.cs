using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AmmoUIManager : MonoBehaviour
{
    [Header("UI Elements - Panel")]
    public GameObject weaponHudPanel; // panel รวม โชว์ทุกครั้งที่ถืออาวุธ (ไม่ว่าปืนหรือมีด)

    [Header("UI Elements - Weapon Info")]
    public TextMeshProUGUI weaponNameText;
    public Image weaponIconImage;

    [Header("UI Elements - Ammo (โชว์เฉพาะปืน)")]
    public GameObject ammoGroup; // แยก GameObject ย่อยเฉพาะส่วนกระสุน จะได้ซ่อน/โชว์แยกจาก panel หลักได้
    public TextMeshProUGUI ammoText;

    private GunAction currentGun;
    private WeaponInfo currentWeaponInfo;
    private bool isWeaponHeld;

    private int lastCurrentAmmo;
    private int lastMaxAmmo;
    private bool isReloading;

    /// <summary>เรียกจาก PickupSystem ทุกครั้งที่ถือ/สลับ/ถอดอาวุธ ส่ง null ถ้าไม่ถืออะไรเลย</summary>
    public void SetActiveWeapon(GameObject item)
    {
        UnsubscribeFromGun(currentGun);
        currentGun = null;
        currentWeaponInfo = null;
        labelOverride = null;

        if (item == null)
        {
            isWeaponHeld = false;
            RefreshDisplay();
            return;
        }

        isWeaponHeld = true;
        currentWeaponInfo = item.GetComponent<WeaponInfo>();

        GunAction gun = item.GetComponent<GunAction>();
        if (gun != null)
        {
            currentGun = gun;
            SubscribeToGun(gun);
            return; // SubscribeToGun จะเรียก RefreshDisplay ให้เองผ่าน SyncFromGun
        }

        // ไม่ใช่ปืน (มีด หรืออาวุธชนิดอื่นที่ไม่มีกระสุน)
        isReloading = false;
        RefreshDisplay();
    }

    private string labelOverride;
    private bool suppressed;

    /// <summary>ซ่อนกล่อง WeaponHUD เดิมถาวร (HUD อาวุธมุมขวาล่างแบบใหม่ของ WeaponWheelUI แสดงแทน)</summary>
    public void SetSuppressed(bool value)
    {
        suppressed = value;
        RefreshDisplay();

        // ซ่อนทั้งกล่อง (รวมพื้นหลังสีเทาของ WeaponHUD เอง ไม่ใช่แค่ panel ข้างใน)
        // เมธอดของคอมโพเนนต์ยังเรียกได้ตอนปิดอยู่ ระบบอื่นที่สั่ง SetActiveWeapon จึงไม่พัง
        if (gameObject.activeSelf == value)
            gameObject.SetActive(!value);
    }

    /// <summary>โชว์ HUD แบบมีแต่ชื่อ (ใช้ตอนถือ X-Gear ที่ไม่ใช่ GameObject อาวุธ) ส่ง null เพื่อปิด</summary>
    public void SetLabelOnly(string label)
    {
        UnsubscribeFromGun(currentGun);
        currentGun = null;
        currentWeaponInfo = null;
        labelOverride = label;
        isWeaponHeld = !string.IsNullOrEmpty(label);
        isReloading = false;
        RefreshDisplay();
    }

    private void OnDisable()
    {
        UnsubscribeFromGun(currentGun);
    }

    private void SubscribeToGun(GunAction gun)
    {
        if (gun == null)
            return;

        gun.OnAmmoChanged += HandleAmmoChanged;
        gun.OnReloadStatusChanged += HandleReloadChanged;

        lastCurrentAmmo = gun.CurrentAmmo;
        lastMaxAmmo = gun.ReserveAmmo;
        isReloading = gun.IsReloading;
        RefreshDisplay();
    }

    private void UnsubscribeFromGun(GunAction gun)
    {
        if (gun == null)
            return;

        gun.OnAmmoChanged -= HandleAmmoChanged;
        gun.OnReloadStatusChanged -= HandleReloadChanged;
    }

    private void HandleAmmoChanged(int currentAmmo, int maxAmmo)
    {
        lastCurrentAmmo = currentAmmo;
        lastMaxAmmo = maxAmmo;
        RefreshDisplay();
    }

    private void HandleReloadChanged(bool reloading)
    {
        isReloading = reloading;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (weaponHudPanel != null)
            weaponHudPanel.SetActive(isWeaponHeld && !suppressed);

        if (!isWeaponHeld || suppressed)
            return;

        // ชื่อ/ไอคอน โชว์เสมอไม่ว่าอาวุธชนิดไหน
        if (weaponNameText != null)
            weaponNameText.text = !string.IsNullOrEmpty(labelOverride) ? labelOverride
                : currentWeaponInfo != null ? currentWeaponInfo.DisplayName : "Weapon";

        if (weaponIconImage != null)
        {
            Sprite icon = currentWeaponInfo != null ? currentWeaponInfo.Icon : null;
            weaponIconImage.sprite = icon;
            weaponIconImage.enabled = icon != null;
        }

        // ส่วนกระสุน โชว์เฉพาะตอนถือปืน (currentGun != null)
        bool showAmmo = currentGun != null;

        if (ammoGroup != null)
            ammoGroup.SetActive(showAmmo);

        if (showAmmo && ammoText != null)
        {
            ammoText.text = isReloading
                ? "Reloading..."
                : $"{lastCurrentAmmo} / {lastMaxAmmo}";
        }
    }
}