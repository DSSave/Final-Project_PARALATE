using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>1 แถวของ Weapon HUD (Unarmed / Command / X-Gear) — ช่องอ้างอิงให้ WeaponWheelUI เติมข้อมูล</summary>
public class WeaponHudRowView : MonoBehaviour
{
    public Image background;
    [Tooltip("ขีดสีด้านซ้าย (บอกช่องที่ถืออยู่)")]
    public Image accent;
    public Image icon;
    public TextMeshProUGUI title;
    public TextMeshProUGUI itemName;
    public TextMeshProUGUI ammo;
    [Tooltip("จำนวนของใช้ในช่อง Command (ตัวเลขเล็กมุมขวาล่าง)")]
    public TextMeshProUGUI count;

    public RectTransform Rect => (RectTransform)transform;
}
