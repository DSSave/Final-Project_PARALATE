using UnityEngine;

/// <summary>
/// กุญแจเฉพาะประตู (1 ไฟล์ = กุญแจ 1 ดอก) สร้างที่ Create > Paralate > Door Key
/// ไฟล์ต้องอยู่ใน Assets/Resources/Keys/ (ระบบเซฟหากุญแจจาก Id ในโฟลเดอร์นี้)
///
/// วิธีใช้: ลากไฟล์นี้ใส่ช่อง Required Key ของ HingedDoor และช่อง Key ของ DoorKeyPickup (กุญแจที่วางในฉาก)
/// ต่างจาก Keycard: Keycard เป็นระดับ (Lv.2 เปิดประตู Lv.1 ได้) ส่วนกุญแจเปิดได้เฉพาะประตูที่ระบุกุญแจดอกนี้
/// </summary>
[CreateAssetMenu(menuName = "Paralate/Door Key", fileName = "Key_New")]
public class DoorKeyData : ScriptableObject
{
    public const string ResourcesFolder = "Keys";

    [Tooltip("ชื่อเฉพาะของกุญแจ ห้ามซ้ำ (ใช้ในเซฟ) ว่าง = ใช้ชื่อไฟล์")]
    public string keyId;
    public string displayName = "Key";
    [TextArea] public string description = "Opens a specific door.";
    public Sprite icon;
    public Color color = new Color(1f, 0.8f, 0.35f);

    public string Id => string.IsNullOrEmpty(keyId) ? name : keyId;

    /// <summary>หากุญแจจาก Id (ใช้ตอนโหลดเซฟ) ไม่เจอ = null</summary>
    public static DoorKeyData Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        foreach (DoorKeyData key in Resources.LoadAll<DoorKeyData>(ResourcesFolder))
        {
            if (key != null && key.Id == id)
                return key;
        }

        return null;
    }
}
