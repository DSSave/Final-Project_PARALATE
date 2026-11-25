using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้าตาของ Weapon HUD มุมขวาล่าง (Prefab: Resources/UI/WeaponHUD)
///   rows      : แถวเรียงตามลำดับ Unarmed, Knife, Command, X-Gear — ตำแหน่งใน Prefab = ตำแหน่งตอนกด Tab กางออก
///   closedSlot: ตำแหน่งของแถวที่ถืออยู่ตอนไม่ได้กด Tab
/// ย้าย/ปรับขนาด/สีใน Editor ได้ทั้งหมด สคริปต์ WeaponWheelUI แค่สลับแถวที่แสดงและเติมข้อความ
/// </summary>
public class WeaponHudView : MonoBehaviour
{
    public const string PrefabName = "WeaponHUD";

    [Tooltip("ลำดับต้องเป็น Unarmed, Knife, Weapon, X-Gear (ตำแหน่งบนจอจัดได้อิสระ ลูกกลิ้งเลื่อนตามตำแหน่งจริง)")]
    public WeaponHudRowView[] rows = new WeaponHudRowView[4];
    [Tooltip("จุดที่แถวที่ถืออยู่ไปอยู่ตอนไม่ได้กด Tab (ใช้ตำแหน่ง anchoredPosition ของวัตถุนี้)")]
    public RectTransform closedSlot;
    public GameObject hint;

    [Header("Look")]
    public Color normalColor = new Color(0f, 0f, 0f, 0.7f);
    public Color selectedColor = new Color(0.25f, 0.7f, 1f, 0.95f);
    public Color emptyColor = new Color(0f, 0f, 0f, 0.35f);
    [Tooltip("สีพื้นหลังของแถวตอนไม่ได้กด Tab")]
    public Color idleColor = new Color(0f, 0f, 0f, 0.45f);
    [Tooltip("แถวที่เลือกยื่นออกมาทางซ้ายกี่พิกเซล ตอนกด Tab")]
    public float selectedShift = 28f;

    [Header("ซ่อนอัตโนมัติ")]
    [Tooltip("เปิด = HUD ซ่อนอยู่ ขึ้นเฉพาะตอนกด Tab และค้างไว้หลังเปลี่ยนอาวุธ / ปิด = แสดงตลอด")]
    public bool autoHide = true;
    [Tooltip("เปลี่ยนอาวุธแล้วค้าง HUD ไว้กี่วินาทีก่อนหาย")]
    [Min(0f)] public float showAfterChangeSeconds = 1f;
    [Tooltip("เวลาค่อยๆ จางหาย (วินาที)")]
    [Min(0f)] public float fadeOutSeconds = 0.2f;

    // ---------------------------------------------------------------- หน้าตาเริ่มต้น (ใช้สร้าง Prefab ครั้งแรก)

    public static WeaponHudView BuildDefault()
    {
        Canvas canvas = UIBuilder.CreateCanvas(PrefabName, 60);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;
        WeaponHudView view = canvas.gameObject.AddComponent<WeaponHudView>();

        // ลำดับใน rows ต้องเป็น Unarmed, Knife, Weapon, X-Gear (ตรงกับ WeaponWheelUI) แต่บนจอเรียง X-Gear บนสุด → Unarmed ล่างสุด
        string[] titles = { "UNARMED", "KNIFE", "WEAPON", "X-GEAR" };
        Vector2 corner = new Vector2(1f, 0f);
        Vector2 margin = new Vector2(40f, 40f);
        Vector2 rowSize = new Vector2(360f, 68f);
        const float gap = 8f;

        view.closedSlot = UIBuilder.Place(UIBuilder.Rect("ClosedSlot", canvas.transform), corner, corner, new Vector2(-margin.x, margin.y), rowSize);

        for (int i = 0; i < titles.Length; i++)
        {
            Image background = UIBuilder.Panel("Row_" + titles[i], canvas.transform, view.idleColor, false);
            float y = i * (rowSize.y + gap);
            UIBuilder.Place(background.rectTransform, corner, corner, new Vector2(-margin.x, margin.y + y), rowSize);

            WeaponHudRowView row = background.gameObject.AddComponent<WeaponHudRowView>();
            row.background = background;

            row.accent = UIBuilder.Panel("Accent", background.transform, UIBuilder.AccentColor, false);
            row.accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            row.accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            row.accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            row.accent.rectTransform.anchoredPosition = Vector2.zero;
            row.accent.rectTransform.sizeDelta = new Vector2(5f, 0f);

            row.icon = UIBuilder.Panel("Icon", background.transform, Color.white, false);
            row.icon.preserveAspect = true;
            UIBuilder.Place(row.icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(48f, 48f));

            row.title = UIBuilder.Text("Title", background.transform, titles[i], 15f, TextAlignmentOptions.TopLeft, UIBuilder.MutedText);
            UIBuilder.Stretch(row.title.rectTransform, 18f, 0f, 110f, 8f);

            row.itemName = UIBuilder.Text("Name", background.transform, string.Empty, 24f, TextAlignmentOptions.BottomLeft);
            UIBuilder.Stretch(row.itemName.rectTransform, 18f, 8f, 110f, 0f);
            row.itemName.textWrappingMode = TextWrappingModes.NoWrap;
            row.itemName.overflowMode = TextOverflowModes.Ellipsis;

            row.ammo = UIBuilder.Text("Ammo", background.transform, string.Empty, 26f, TextAlignmentOptions.MidlineRight);
            UIBuilder.Stretch(row.ammo.rectTransform, 18f, 0f, 16f, 0f);

            row.count = UIBuilder.Text("Count", background.transform, string.Empty, 18f, TextAlignmentOptions.BottomRight);
            UIBuilder.Stretch(row.count.rectTransform, 18f, 4f, 8f, 0f);

            view.rows[i] = row;
        }

        TextMeshProUGUI hintText = UIBuilder.Text("Hint", canvas.transform, "Scroll to change  •  Release Tab to confirm", 16f,
            TextAlignmentOptions.BottomRight, UIBuilder.MutedText);
        float stackHeight = titles.Length * rowSize.y + (titles.Length - 1) * gap;
        UIBuilder.Place(hintText.rectTransform, corner, corner, new Vector2(-margin.x, margin.y + stackHeight + 8f), new Vector2(rowSize.x + 80f, 24f));
        view.hint = hintText.gameObject;

        return view;
    }
}
