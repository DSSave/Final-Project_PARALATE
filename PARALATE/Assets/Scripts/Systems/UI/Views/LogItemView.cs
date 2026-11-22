using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>1 หัวข้อในหน้า Esc &gt; Logs &gt; Log (แถบหัวข้อกดได้ + กล่อง info พับเปิด)</summary>
public class LogItemView : MonoBehaviour
{
    public Button headerButton;
    public Image header;
    public TextMeshProUGUI title;
    public GameObject newBadge;
    [Tooltip("ลูกศรสามเหลี่ยม: ชี้ลง = ปิด, หมุน 180 = เปิด")]
    public Image arrow;
    public GameObject info;
    public TextMeshProUGUI body;
}
