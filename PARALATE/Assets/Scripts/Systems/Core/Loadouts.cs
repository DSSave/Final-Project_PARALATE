using System;
using UnityEngine;

// (XGearLoadout ระบบติดตั้งชิป 3 ช่องแบบเก่า ถูกเอาออกแล้ว: ชิปอยู่ในคลัง ดู XGearSystem)

/// <summary>ระดับอัปเกรดของอาวุธแต่ละชนิด (อัปที่ Upgrade Corner ใน Base ด้วย Scrap) บันทึกลง PlayerPrefs</summary>
public static class WeaponUpgrades
{
    public const int MaxLevel = 5;
    public const float DamagePerLevel = 0.05f;      // ดาเมจ +5% ต่อระดับ
    public const float ReloadSpeedPerLevel = 0.05f; // รีโหลดเร็วขึ้น +5% ต่อระดับ

    public static readonly string[] WeaponKeys = { "AK47", "USP", "Sniper" };

    private const string KeyPrefix = "WeaponLevel_";

    public static string KeyOf(AmmoType type)
    {
        switch (type)
        {
            case AmmoType.Pistol: return "USP";
            case AmmoType.Sniper: return "Sniper";
            default: return "AK47";
        }
    }

    public static int GetLevel(string weaponKey) => Mathf.Clamp(PlayerPrefs.GetInt(KeyPrefix + weaponKey, 0), 0, MaxLevel);

    public static float DamageMultiplier(string weaponKey) => 1f + GetLevel(weaponKey) * DamagePerLevel;

    public static float ReloadSpeedMultiplier(string weaponKey) => 1f + GetLevel(weaponKey) * ReloadSpeedPerLevel;

    /// <summary>Scrap ที่ต้องใช้เพื่ออัปจากระดับปัจจุบันไประดับถัดไป</summary>
    public static int NextCost(string weaponKey) => 2 * (GetLevel(weaponKey) + 1);

    public static bool TryUpgrade(string weaponKey)
    {
        int level = GetLevel(weaponKey);
        if (level >= MaxLevel || PersistentInventory.Instance == null)
            return false;

        if (!PersistentInventory.Instance.TrySpend(ItemCatalog.Scrap, NextCost(weaponKey)))
            return false;

        PlayerPrefs.SetInt(KeyPrefix + weaponKey, level + 1);
        PlayerPrefs.Save();
        return true;
    }

    public static void ResetSave()
    {
        for (int i = 0; i < WeaponKeys.Length; i++)
            PlayerPrefs.DeleteKey(KeyPrefix + WeaponKeys[i]);
    }
}

/// <summary>สูตรคราฟต์กระสุนที่ Upgrade Corner</summary>
public sealed class AmmoRecipe
{
    public string ammoId;
    public int scrap;
    public int metal;
    public int yield;

    public static readonly AmmoRecipe[] All =
    {
        new AmmoRecipe { ammoId = ItemCatalog.AmmoRifle, scrap = 0, metal = 1, yield = 30 },
        new AmmoRecipe { ammoId = ItemCatalog.AmmoPistol, scrap = 1, metal = 1, yield = 12 },
        new AmmoRecipe { ammoId = ItemCatalog.AmmoSniper, scrap = 1, metal = 2, yield = 5 },
    };

    public bool TryCraft()
    {
        PersistentInventory inventory = PersistentInventory.Instance;
        if (inventory == null || !inventory.TrySpend((ItemCatalog.Scrap, scrap), (ItemCatalog.Metal, metal)))
            return false;

        inventory.Add(ammoId, yield);
        return true;
    }
}

/// <summary>สถานะล่องหนของผู้เล่น (ชิป Invisible) ระบบมองเห็นของศัตรู/กล้องวงจรปิดเช็กค่านี้</summary>
public static class PlayerStealth
{
    public static bool IsInvisible { get; private set; }

    public static void SetInvisible(bool value) => IsInvisible = value;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsInvisible = false;
}
