using UnityEngine;

/// <summary>
/// บันทึก 1 หัวข้อในหน้า Logs (เมนู Esc) แบบ Memo: หัวข้อ + คำอธิบายที่กดเปิด/ปิดดูได้
///
/// วิธีเพิ่มบันทึกใหม่: คลิกขวาใน Project > Create > Paralate > Log Entry แล้ววางไฟล์ไว้ใน Assets/Resources/Logs/
///   - Unlock At Start = ติ๊ก  → ขึ้นตั้งแต่เริ่มเกม
///   - Unlock At Start = ไม่ติ๊ก → ขึ้นเมื่อถูกปลดล็อก: เดินเข้าโซน LogUnlockTrigger, เรียกจาก UnityEvent,
///     หรือโค้ดเรียก GameLog.Unlock("id") ตอนเกิดเหตุการณ์
/// </summary>
[CreateAssetMenu(menuName = "Paralate/Log Entry", fileName = "Log_New")]
public class LogEntryData : ScriptableObject
{
    [Tooltip("รหัสสำหรับปลดล็อกจากโค้ด/Trigger (ห้ามซ้ำกัน) เช่น movement, noise, kill_or_not")]
    public string id = "new_log";

    public string title = "New Log";

    [TextArea(4, 12)]
    public string body = "Description...";

    [Tooltip("ติ๊ก = ขึ้นตั้งแต่เริ่มเกม / ไม่ติ๊ก = รอปลดล็อกจากอีเว้นท์หรือจุด Trigger")]
    public bool unlockAtStart = true;

    [Tooltip("ลำดับในรายการ (น้อยอยู่บน)")]
    public int order;
}
