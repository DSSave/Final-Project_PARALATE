using UnityEngine;

/// <summary>
/// ของที่ไม่ใช่ศัตรูแต่ยิงโดนแล้วได้รับดาเมจ (เช่น กล้องวงจรปิด)
/// GunAction เช็ก EnemyHealth ก่อน ถ้าไม่ใช่ศัตรูจะหาอินเทอร์เฟซนี้ใน Collider ที่โดน (รวมพ่อ)
/// </summary>
public interface IShootable
{
    void TakeShot(float damage, Vector3 hitPoint);
}
