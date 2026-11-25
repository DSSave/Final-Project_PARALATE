using UnityEngine;

/// <summary>
/// ลูกบิดประตูที่ยิงได้ (ใส่ไว้ที่วัตถุ Knob ของ HingedDoor ต้องมี Collider แบบไม่ใช่ Trigger)
/// กระสุนโดน → แจ้ง HingedDoor ให้ทำลายกลอน (ทำงานเฉพาะประตูที่ติ๊ก Shootable Knob และกำลังล็อกอยู่)
/// GunAction เช็ก IShootable จาก Collider ที่กระสุนโดน (ต่อจาก EnemyHealth) จึงไม่ต้องแก้ปืน
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorKnob : MonoBehaviour, IShootable
{
    private HingedDoor door;

    private void Awake()
    {
        door = GetComponentInParent<HingedDoor>();
    }

    public void TakeShot(float damage, Vector3 hitPoint)
    {
        if (door != null)
            door.ShootKnob(hitPoint);
    }
}
