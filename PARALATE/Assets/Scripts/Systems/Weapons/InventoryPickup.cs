using UnityEngine;

/// <summary>
/// ของเก็บได้ทั่วไปตาม id ใน ItemCatalog: กล่องกระสุน, ชิป X-Gear, Map Data
/// (ใช้ระบบเดียวกับ ItemPickup/ResourcePickup: ต้องมี Collider และอยู่ใน Layer "Items")
/// - กระสุน: เข้ากระเป๋าทันที ใช้รีโหลดได้เลย
/// - ของใช้ (Medkit, X-Gear Cell): เข้าคลังทันที + ปลดล็อกสูตร Command ครั้งแรก (Amount = จำนวนชิ้น)
///   เรียกใช้ด้วย CommandLine (Q) แล้วคลิกซ้าย
/// - อย่างอื่น: เข้า RunInventory ก่อน จบด่านสำเร็จถึงเป็นของถาวร (กติกาเดียวกับ Scrap/Metal)
/// </summary>
[RequireComponent(typeof(Collider))]
public class InventoryPickup : MonoBehaviour, IInteractable
{
    [Tooltip("id ตาม ItemCatalog เช่น ammo_ak, ammo_usp, ammo_sniper, chip_fleetfoot, chip_invisible, map_hideout")]
    public string itemId = ItemCatalog.AmmoRifle;
    [Min(1)] public int amount = 1;

    [Tooltip("{0} = ชื่อไอเทม, {1} = จำนวน")]
    [TextArea] public string promptFormat = "[ E ] to collect\n{0} x{1}";

    public AudioClip pickupSound;

    public string PromptText
    {
        get
        {
            return string.Format(promptFormat, ItemCatalog.NameOf(itemId), amount);
        }
    }

    public void Interact(GameObject interactor)
    {
        ItemDef def = ItemCatalog.Get(itemId);
        if (def == null)
            return;

        if (def.category == ItemCategory.Ammo || def.category == ItemCategory.Consumable || def.category == ItemCategory.Weapon)
        {
            if (PersistentInventory.Instance == null)
                return;

            PersistentInventory.Instance.Add(itemId, amount);
            CommandBook.LearnFromItem(itemId); // เก็บครั้งแรก = สูตรขึ้นในหน้า Logs > Command
        }
        else
        {
            if (RunInventory.Instance == null)
                return;

            RunInventory.Instance.Add(itemId, amount);
        }

        // เสียงเก็บของเสียงเดียวกันทั้งเกม (ถ้าไม่มีไฟล์ ใช้ pickupSound ของ Prefab / เสียงเริ่มต้นของ AudioManager)
        if (!SfxLibrary.PlayPickup())
        {
            if (pickupSound != null)
                AudioManager.PlayPickup(pickupSound, transform.position);
            else
                AudioManager.Instance?.PlayItemPickupSfx();
        }

        HudToast.Show("+ " + def.displayName + " x" + amount);
        Destroy(gameObject);
    }
}
