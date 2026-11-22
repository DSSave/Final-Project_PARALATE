using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>ช่องของ 1 ชิ้นในหน้า Esc &gt; Inventory (ต้นแบบ = CellTemplate ใน Prefab PauseMenu)</summary>
public class InventoryCellView : MonoBehaviour
{
    public Image background;
    [Tooltip("ไอคอน (Resources/Icons/id) ถ้าไม่มีรูปจะซ่อนแล้วแสดงชื่อแทน")]
    public Image icon;
    public TextMeshProUGUI label;
    public TextMeshProUGUI amount;
}
