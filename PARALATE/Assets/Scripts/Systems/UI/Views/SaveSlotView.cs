using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>1 ช่องเซฟ (ต้นแบบ = SlotTemplate ใน Prefab SaveSlots)</summary>
public class SaveSlotView : MonoBehaviour
{
    [Tooltip("กดทั้งแถวเพื่อโหลด (ใช้ในหน้า Load Game ของ Main Menu)")]
    public Button rowButton;
    [Tooltip("ข้อความ Scene / เวลาเซฟ")]
    public TextMeshProUGUI info;
    [Tooltip("ข้อความตอนช่องว่าง (- No Data -)")]
    public TextMeshProUGUI emptyText;
    public Button saveButton;
    public Button loadButton;
    [Tooltip("ปุ่ม X ลบเซฟ (ถาม Yes / No ก่อน)")]
    public Button deleteButton;
}
