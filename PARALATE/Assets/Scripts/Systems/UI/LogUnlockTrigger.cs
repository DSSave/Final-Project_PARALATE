using UnityEngine;

/// <summary>
/// ปลดล็อกบันทึก (Log) ในหน้า Logs
///   - โหมด Trigger: วางบนวัตถุที่มี Collider (Is Trigger) ผู้เล่นเดินผ่านจุดนี้ = บันทึกขึ้น
///   - เรียก Unlock() จาก UnityEvent ของอย่างอื่นได้ (เช่น InteractableObject.onInteract, ปุ่ม, จบ Timeline)
/// ปลดล็อกแล้วเป็นถาวร (บันทึกในเซฟ) เดินผ่านซ้ำไม่ขึ้นซ้ำ
/// </summary>
public class LogUnlockTrigger : MonoBehaviour
{
    [Tooltip("บันทึกที่จะปลดล็อก (ลากไฟล์จาก Assets/Resources/Logs)")]
    public LogEntryData entry;

    [Tooltip("ใช้แทน entry ได้: พิมพ์ id ของบันทึก")]
    public string entryId;

    [Tooltip("ปลดล็อกเมื่อผู้เล่นเดินเข้า Collider (ต้องติ๊ก Is Trigger)")]
    public bool unlockOnEnter = true;

    private string Id => entry != null ? entry.id : entryId;

    public void Unlock()
    {
        GameLog.Unlock(Id);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (unlockOnEnter && other.CompareTag("Player"))
            Unlock();
    }

    private void Reset()
    {
        // เพิ่มคอมโพเนนต์ใหม่: ตั้ง Collider ที่มีอยู่ให้เป็น Trigger ให้เลย
        Collider existing = GetComponent<Collider>();
        if (existing != null)
            existing.isTrigger = true;
    }

    private void OnDrawGizmos()
    {
        Collider area = GetComponent<Collider>();
        if (area == null)
            return;

        Gizmos.color = new Color(0.25f, 0.7f, 1f, 0.25f);
        Gizmos.DrawCube(area.bounds.center, area.bounds.size);
    }
}
