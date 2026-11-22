using System;
using System.Collections.Generic;
using UnityEngine;

public enum CommandArrow { Up, Down, Left, Right }

public enum CommandKind
{
    OpenSaveMenu, // เปิดหน้าต่าง Save & Load
    Consumable,   // ใส่ของใช้ (Medkit, X-Gear Cell) ลงช่อง Command แล้วคลิกซ้ายเพื่อใช้
    Weapon        // เรียกปืนมา 1 แม็ก ยิงหมดติด Cooldown (ตั้งที่ GunAction.commandCooldown)
}

[Serializable]
public class CommandRecipe
{
    [Tooltip("ชื่อเฉพาะของสูตร (ห้ามซ้ำ)")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    public CommandKind kind = CommandKind.Consumable;

    [Tooltip("ลูกศรที่ต้องกด (W ขึ้น / S ลง / A ซ้าย / D ขวา) แล้วกด Q ยืนยัน")]
    public CommandArrow[] sequence = Array.Empty<CommandArrow>();

    [Tooltip("ไอเทมในคลังที่สูตรนี้ใช้ (id ตาม ItemCatalog) เก็บไอเทมนี้ครั้งแรก = ปลดล็อกสูตร")]
    public string itemId;

    [Tooltip("Weapon: Prefab ของปืนที่จะเรียกมา")]
    public GameObject weaponPrefab;

    [Tooltip("Consumable: ปริมาณต่อการใช้ 1 ชิ้น (Medkit = HP ที่ฟื้น, X-Gear Cell = จำนวน Charge)")]
    public float effectAmount = 1f;

    [Tooltip("รู้สูตรนี้ตั้งแต่เริ่มเกม (ไม่ต้องเก็บของก่อน) เช่น SaveGame")]
    public bool knownFromStart;
}

/// <summary>
/// สูตร CommandLine ทั้งเกม (Assets/Resources/CommandBook.asset) แก้ลำดับลูกศร/ปริมาณ/Prefab ใน Inspector ได้
/// ถ้าไม่มีไฟล์ จะใช้ค่าเริ่มต้นจาก Defaults() (สูตรเดียวกัน แต่ยังไม่มี Prefab ปืน)
/// </summary>
[CreateAssetMenu(menuName = "Paralate/Command Book", fileName = "CommandBook")]
public class CommandBookData : ScriptableObject
{
    [Tooltip("ใส่ลูกศรได้มากสุดกี่ตัว")]
    [Min(3)] public int maxLength = 8;

    public List<CommandRecipe> recipes = new List<CommandRecipe>();

    public static List<CommandRecipe> Defaults()
    {
        CommandArrow U = CommandArrow.Up, D = CommandArrow.Down, L = CommandArrow.Left, R = CommandArrow.Right;

        return new List<CommandRecipe>
        {
            new CommandRecipe
            {
                id = "save_game", displayName = "SaveGame", kind = CommandKind.OpenSaveMenu, knownFromStart = true,
                description = "Opens the Save & Load window.", sequence = new[] { D, D, L, R, U }
            },
            new CommandRecipe
            {
                id = "medkit", displayName = "Medkit", kind = CommandKind.Consumable, itemId = ItemCatalog.Medkit, effectAmount = 30f,
                description = "Restores 30 HP. Left Click to use.", sequence = new[] { U, D, L, R }
            },
            new CommandRecipe
            {
                id = "xgear_cell", displayName = "X-Gear Cell", kind = CommandKind.Consumable, itemId = ItemCatalog.XGearCell, effectAmount = 1f,
                description = "Restores 1 X-Gear charge. Left Click to use.", sequence = new[] { L, R, U, D }
            },
            new CommandRecipe
            {
                id = "usp", displayName = "USP", kind = CommandKind.Weapon, itemId = ItemCatalog.WeaponUsp,
                description = "Pistol. One magazine, then a cooldown.", sequence = new[] { U, D, L, L, R, D, L }
            },
            new CommandRecipe
            {
                id = "sniper", displayName = "Sniper", kind = CommandKind.Weapon, itemId = ItemCatalog.WeaponSniper,
                description = "Sniper rifle. One magazine, then a cooldown.", sequence = new[] { U, D, L, R, L, U, D }
            },
            new CommandRecipe
            {
                id = "ak47", displayName = "AK47", kind = CommandKind.Weapon, itemId = ItemCatalog.WeaponAk,
                description = "Assault rifle. One magazine, then a cooldown.", sequence = new[] { R, R, U, D, L, R }
            },
        };
    }
}
