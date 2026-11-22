using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Installer ใน Safe Room: กด E เพื่อเปิดหน้าปรับแต่ง X-Gear 3 หัวข้อ (Active / Passive / Maps)
/// ต้องมี Collider และอยู่ใน Layer "Items" ให้ PickupDetector (ระบบกด E) เห็น
///
/// View Camera: CinemachineCamera ที่วางในฉาก (จัดมุมเองใน Editor ได้ กด Solo ดูภาพได้)
///   เปิดหน้า Installer = ยก Priority ของกล้องนี้ขึ้น / ปิด = ลดกลับเป็น 0
///   ว่าง = ใช้กล้องโชว์ตัวผู้เล่นอัตโนมัติ
/// </summary>
[RequireComponent(typeof(Collider))]
public class XGearInstaller : MonoBehaviour, IInteractable
{
    public string promptText = "[ E ] X-Gear Installer";

    [Tooltip("กล้องตอนเปิดหน้า Installer (วางใน Scene แล้วจัดมุมเอง) ว่าง = กล้องอัตโนมัติ")]
    public CinemachineCamera viewCamera;
    [Tooltip("Priority ตอนเปิด (ต้องสูงกว่ากล้องห้อง 10-900 และ Action Camera 1000)")]
    public int activePriority = 3000;

    public string PromptText => XGearInstallerUI.IsOpen ? string.Empty : promptText;

    private void Awake()
    {
        if (viewCamera != null)
            viewCamera.Priority.Value = 0;
    }

    public void Interact(GameObject interactor)
    {
        if (!XGearInstallerUI.IsOpen)
            XGearInstallerUI.Open(this);
    }

    public void SetCameraActive(bool active)
    {
        if (viewCamera != null)
            viewCamera.Priority.Value = active ? activePriority : 0;
    }
}
