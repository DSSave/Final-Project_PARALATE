using System.Collections.Generic;
using UnityEngine;

public enum ItemCategory
{
    Resource,
    Ammo,
    PassiveChip,
    ActiveChip,
    MapData,
    Consumable, // ของใช้ (ยาเพิ่มเลือด, เซลล์ X-Gear): เก็บเข้าคลัง แล้วเรียกใช้ผ่าน Command
    Weapon      // อาวุธที่เป็นเจ้าของ (เก็บครั้งแรก = ปลดล็อกสูตร Command ของอาวุธนั้น)
}

public enum AmmoType
{
    Auto = 0,   // เดาจากชื่ออาวุธ (usp/pistol = Pistol, sniper = Sniper, อื่นๆ = Rifle)
    Rifle = 1,  // AK
    Pistol = 2, // USP
    Sniper = 3
}

public enum WeaponSlotType
{
    Auto = 0,     // ปืนพก/มีด = Secondary, อื่นๆ = Primary
    Primary = 1,
    Secondary = 2
}

public enum HeldSlot
{
    Unarmed = 0,
    Primary = 1,   // ไม่ใช้แล้ว (อาวุธเรียกผ่านช่อง Command) เก็บค่าไว้ไม่ให้ข้อมูลเดิมเพี้ยน
    Secondary = 2, // ไม่ใช้แล้ว
    XGear = 3,     // แขนกล: Active Chip ที่เรียกด้วย Command อยู่ช่องนี้ คลิกซ้ายใช้
    Weapon = 4,    // ปืนที่ถือ (ชิ้นแรกที่เก็บได้ / เปลี่ยนด้วย Command) — เดิมชื่อ Command
    Knife = 5      // มีดติดตัวตั้งแต่เริ่ม คลิกซ้ายฟัน
}

public sealed class ItemDef
{
    public string id;
    public string displayName;
    public string description;
    public ItemCategory category;
    public Color color = Color.white;

    /// <summary>ใช้กับ MapData: ชื่อ Scene ที่แผนที่นี้ใช้ได้</summary>
    public string sceneName;
}

/// <summary>
/// ทะเบียนไอเทมทั้งหมดของเกม (แหล่งข้อมูลเดียว): Inventory, X-Gear, Upgrade Corner และหน้า Pause อ่านจากที่นี่
/// เพิ่มไอเทมใหม่ = เพิ่มบรรทัดใน Build() บรรทัดเดียว ไม่ต้องแก้ UI
/// ไอคอน (ไม่บังคับ): วาง Sprite ชื่อเดียวกับ id ไว้ที่ Resources/Icons/
/// </summary>
public static class ItemCatalog
{
    public const string Scrap = "Scrap";
    public const string Metal = "Metal";
    public const string AmmoRifle = "ammo_ak";
    public const string AmmoPistol = "ammo_usp";
    public const string AmmoSniper = "ammo_sniper";
    public const string ChipFleetFoot = "chip_fleetfoot";
    public const string ChipInvisible = "chip_invisible";
    public const string MapHideOut = "map_hideout";
    public const string Medkit = "medkit";
    public const string XGearCell = "xgear_cell";
    public const string WeaponUsp = "weapon_usp";
    public const string WeaponSniper = "weapon_sniper";
    public const string WeaponAk = "weapon_ak";
    public const string WeaponKnife = "weapon_knife";

    private static Dictionary<string, ItemDef> defs;
    private static List<ItemDef> ordered;
    private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

    private static void Build()
    {
        defs = new Dictionary<string, ItemDef>();
        ordered = new List<ItemDef>();

        Add(Scrap, "Scrap", "Salvaged parts. Used for upgrades and crafting.", ItemCategory.Resource, new Color(0.8f, 0.6f, 0.35f));
        Add(Metal, "Metal", "Refined metal. Used for crafting ammo.", ItemCategory.Resource, new Color(0.7f, 0.75f, 0.8f));

        Add(AmmoRifle, "AK Ammo", "7.62mm rounds for the AK47.", ItemCategory.Ammo, new Color(0.95f, 0.8f, 0.3f));
        Add(AmmoPistol, "USP Ammo", ".45 rounds for the USP.", ItemCategory.Ammo, new Color(0.95f, 0.8f, 0.3f));
        Add(AmmoSniper, "Sniper Ammo", "High-caliber rounds for the sniper rifle.", ItemCategory.Ammo, new Color(0.95f, 0.8f, 0.3f));

        // ชิป X-Gear: เก็บแล้วเข้าคลังทันที (หน้า Esc > X-Gear)
        //   Passive = ได้ผลอัตโนมัติ ซ้อนตามจำนวนที่มี / Active = เรียกด้วย Command เข้าช่อง X-Gear แล้วคลิกซ้าย
        Add(ChipFleetFoot, "Passive Chip : FleetFoot", "Passive. Crouch movement speed +5% per chip (stacks).", ItemCategory.PassiveChip, new Color(0.4f, 0.9f, 0.5f));
        Add(ChipInvisible, "Active Chip : Invisible",
            "Active. Become invisible for 5 seconds. Enemies cannot detect you by sight.\nCall it with its Command, then Left Click with X-Gear. Each use costs 1 X-Gear charge.",
            ItemCategory.ActiveChip, new Color(0.35f, 0.7f, 1f));

        // Map Chip: เข้าโซน (MapArea) ที่มีชิปของโซนนั้น = Minimap ขึ้นเอง
        Add(MapHideOut, "Map Data : HideOut", "Shows the HideOut minimap, including enemies, while you are inside it.", ItemCategory.MapData, new Color(0.9f, 0.5f, 0.9f));

        // ของใช้: ใช้ทันทีด้วย Command หรือหน้า Esc > Inventory (คลิกขวา > Use) ปริมาณที่ฟื้นตั้งใน ItemData ของไอเทม
        Add(Medkit, "Medkit", "Restores health.\nUse it with its Command, or right-click it in the Inventory.", ItemCategory.Consumable, new Color(1f, 0.35f, 0.35f));
        Add(XGearCell, "X-Gear Cell", "Restores 1 X-Gear charge (up to the maximum).\nUse it with its Command, or right-click it in the Inventory.", ItemCategory.Consumable, new Color(0.25f, 0.7f, 1f));

        // อาวุธ: เก็บครั้งแรก = เป็นเจ้าของถาวร + ปลดล็อกสูตร Command (ชิ้นแรกที่เก็บเข้าช่อง Weapon ทันที)
        Add(WeaponUsp, "USP", "Pistol. Swap it into the Weapon slot with its Command. Reloads from your USP ammo.", ItemCategory.Weapon, new Color(1f, 0.6f, 0.3f));
        Add(WeaponSniper, "Sniper", "Sniper rifle. Swap it into the Weapon slot with its Command. Reloads from your sniper ammo.", ItemCategory.Weapon, new Color(1f, 0.6f, 0.3f));
        Add(WeaponAk, "AK47", "Assault rifle. Swap it into the Weapon slot with its Command. Reloads from your AK ammo.", ItemCategory.Weapon, new Color(1f, 0.6f, 0.3f));
        Add(WeaponKnife, "Knife", "Makes stealth kills from a strangle faster.", ItemCategory.Weapon, new Color(1f, 0.6f, 0.3f));
    }

    private static ItemDef Add(string id, string name, string description, ItemCategory category, Color color)
    {
        ItemDef def = new ItemDef { id = id, displayName = name, description = description, category = category, color = color };
        defs[id] = def;
        ordered.Add(def);
        return def;
    }

    public static IReadOnlyList<ItemDef> All
    {
        get
        {
            if (defs == null) Build();
            return ordered;
        }
    }

    /// <summary>ไม่คืน null: id ที่ไม่รู้จัก (เช่น ResourceData ใหม่) จะได้ข้อมูลพื้นฐานที่ใช้ชื่อ id</summary>
    public static ItemDef Get(string id)
    {
        if (defs == null) Build();

        if (string.IsNullOrEmpty(id))
            return null;

        if (!defs.TryGetValue(id, out ItemDef def))
        {
            // Map Chip ของโซนใหม่ (id ขึ้นต้น map_) ไม่ต้องเพิ่มโค้ด: MapArea ลงทะเบียนชื่อให้ตอนโหลด Scene
            bool isMap = id.StartsWith(MapPrefix, System.StringComparison.Ordinal);
            def = new ItemDef
            {
                id = id,
                displayName = isMap ? "Map Data : " + id.Substring(MapPrefix.Length) : id,
                description = isMap ? "Shows this area's minimap when you are inside it." : string.Empty,
                category = isMap ? ItemCategory.MapData : ItemCategory.Resource,
                color = isMap ? new Color(0.9f, 0.5f, 0.9f) : Color.white
            };
            defs[id] = def;
            ordered.Add(def);
        }

        return def;
    }

    /// <summary>คำนำหน้า id ของ Map Chip (map_metro, map_warehouse ...)</summary>
    public const string MapPrefix = "map_";

    /// <summary>ตั้งชื่อ/คำอธิบายของ Map Chip ให้ตรงกับโซน (MapArea เรียกตอนเริ่ม Scene)</summary>
    public static void RegisterMap(string id, string displayName, string description)
    {
        if (string.IsNullOrEmpty(id))
            return;

        ItemDef def = Get(id);
        def.category = ItemCategory.MapData;
        if (!string.IsNullOrEmpty(displayName)) def.displayName = displayName;
        if (!string.IsNullOrEmpty(description)) def.description = description;
    }

    public static string NameOf(string id)
    {
        ItemDef def = Get(id);
        return def != null ? def.displayName : "-";
    }

    public static Sprite IconOf(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (!icons.TryGetValue(id, out Sprite sprite))
        {
            sprite = Resources.Load<Sprite>("Icons/" + id);
            icons[id] = sprite;
        }

        return sprite;
    }

    /// <summary>id ของอาวุธในคลัง จากตัวปืน/มีดที่วางในฉาก (null = ไม่ใช่อาวุธ)</summary>
    public static string WeaponIdOf(GameObject weapon)
    {
        if (weapon == null)
            return null;

        if (weapon.GetComponent<KnifeAction>() != null)
            return WeaponKnife;

        GunAction gun = weapon.GetComponent<GunAction>();
        if (gun == null)
            return null;

        switch (gun.ResolvedAmmoType)
        {
            case AmmoType.Pistol: return WeaponUsp;
            case AmmoType.Sniper: return WeaponSniper;
            default: return WeaponAk;
        }
    }

    public static string AmmoId(AmmoType type)
    {
        switch (type)
        {
            case AmmoType.Pistol: return AmmoPistol;
            case AmmoType.Sniper: return AmmoSniper;
            default: return AmmoRifle;
        }
    }
}
