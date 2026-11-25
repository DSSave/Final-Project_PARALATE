using UnityEngine;

/// <summary>
/// ข้อมูลของไอเทมแต่ละชิ้น (ไฟล์อยู่ที่ Assets/Resources/Items/ — ต้องอยู่ในโฟลเดอร์นี้ CommandBook ถึงหาเจอ)
/// - ส่วนบน: ชื่อ/ไอคอน/เสียง/ข้อความตอนเก็บ (WeaponInfo และ ItemPickup ใช้)
/// - ส่วน CommandLine: สูตรลูกศรสำหรับเรียกไอเทมนี้ (ติ๊ก Has Command แล้วแก้ลำดับลูกศรได้ที่นี่เลย)
///   สูตรที่ไม่ใช่ไอเทม (เช่น SaveGame) ยังอยู่ใน Resources/CommandBook.asset
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Items/Item Data")]
public class ItemData : ScriptableObject
{
    public AudioClip pickupSound;
    public string itemName;
    public Sprite icon;
    [TextArea] public string promptText = "Press E to pick up";

    [Tooltip("ของที่วางลงพื้นเมื่อกด Drop ในหน้า Esc > Inventory (ว่าง = สร้างกล่องเก็บได้ทั่วไปให้เอง)")]
    public GameObject dropPrefab;

    [Header("CommandLine (สูตรเรียกไอเทมนี้)")]
    [Tooltip("ติ๊ก = ไอเทมนี้มีสูตร CommandLine (เก็บไอเทมครั้งแรก = ปลดล็อกสูตร)")]
    public bool hasCommand;

    [Tooltip("Id = ชื่อสูตร ห้ามซ้ำและห้ามเปลี่ยนหลังปล่อยเกม (เซฟจำสูตรที่รู้แล้วด้วย Id)\n" +
             "Sequence = ลูกศรที่ต้องกด (W ขึ้น / S ลง / A ซ้าย / D ขวา) แล้วกด Q ยืนยัน\n" +
             "Item Id = id ในคลัง (ItemCatalog) เช่น weapon_ak, medkit")]
    public CommandRecipe command = new CommandRecipe();

    private void OnValidate()
    {
        // เปิดสูตรครั้งแรก: ตั้งชื่อที่แสดงให้ตรงกับชื่อไอเทม
        if (hasCommand && command != null && string.IsNullOrEmpty(command.displayName))
            command.displayName = itemName;
    }
}
