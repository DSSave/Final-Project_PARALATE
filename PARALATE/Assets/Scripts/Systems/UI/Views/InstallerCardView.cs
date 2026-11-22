using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>การ์ด 1 ใบในหน้า X-Gear Installer (Active / Passive / Maps)</summary>
public class InstallerCardView : MonoBehaviour
{
    [Tooltip("หัวการ์ด (ชื่อ ▽) กดเพื่อกาง/พับรายการ")]
    public Button headerButton;
    public Image header;
    public TextMeshProUGUI headerText;
    public Image arrow;
    public TextMeshProUGUI body;

    [Header("รายการให้เลือก (กางเหนือหัวการ์ด)")]
    public RectTransform dropdown;
    [Tooltip("ปุ่มต้นแบบของตัวเลือก (ปิดไว้ อยู่ใต้ Dropdown)")]
    public Button optionTemplate;
    [Tooltip("ปุ่มถอดชิปคืนคลัง (อยู่ใต้ Dropdown)")]
    public Button removeButton;
    [Tooltip("ข้อความตอนไม่มีชิปในคลัง (อยู่ใต้ Dropdown)")]
    public GameObject emptyText;
}
