using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>1 สูตรในหน้า Esc &gt; Logs &gt; Command (ชื่อ + จำนวนที่มี + แถวลูกศร)</summary>
public class CommandItemView : MonoBehaviour
{
    public TextMeshProUGUI title;
    public TextMeshProUGUI amount;
    public RectTransform arrowRow;
    [Tooltip("ลูกศรต้นแบบ (ปิดไว้ อยู่ใต้ Arrow Row) รูปต้องชี้ลงที่มุม 0")]
    public Image arrowTemplate;
}
