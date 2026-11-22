using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้าตาของแถบ CommandLine (Prefab: Resources/UI/CommandLine)
///   arrowTemplate: รูปลูกศร 1 ตัว (ปิดไว้) ระบบ Clone ตามจำนวนที่กด แล้วหมุนตามทิศ (ต้นแบบชี้ลง = ▽)
/// </summary>
public class CommandLineView : MonoBehaviour
{
    public const string PrefabName = "CommandLine";

    public Image panel;
    [Tooltip("ที่วางลูกศร (มี HorizontalLayoutGroup)")]
    public RectTransform arrowRow;
    [Tooltip("ลูกศรต้นแบบ (ปิดไว้ อยู่ใต้ Arrow Row) รูปต้องชี้ลงที่มุม 0")]
    public Image arrowTemplate;
    [Tooltip("ข้อความตอนยังไม่ได้ใส่ลูกศร")]
    public TextMeshProUGUI placeholder;
    [Tooltip("ข้อความผลลัพธ์ (สำเร็จ/ผิดสูตร) เหนือแถบ")]
    public TextMeshProUGUI message;

    [Header("Look")]
    public Color panelColor = new Color(0f, 0f, 0f, 0.75f);
    public Color errorColor = new Color(0.8f, 0.15f, 0.15f, 0.85f);
    public Color successColor = new Color(0.2f, 0.6f, 1f, 0.85f);

    public static CommandLineView BuildDefault()
    {
        Canvas canvas = UIBuilder.CreateCanvas(PrefabName, 61);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;
        CommandLineView view = canvas.gameObject.AddComponent<CommandLineView>();

        Color arrowColor = new Color(0.35f, 0.8f, 1f, 1f);
        Vector2 corner = new Vector2(1f, 0f);
        Vector2 margin = new Vector2(40f, 118f); // เหนือแถว Weapon HUD
        Vector2 size = new Vector2(360f, 64f);

        view.panel = UIBuilder.Panel("Panel", canvas.transform, view.panelColor, false);
        UIBuilder.Place(view.panel.rectTransform, corner, corner, new Vector2(-margin.x, margin.y), size);

        // ขีดสองข้างแบบในสเก็ตช์ |↑↓→|
        Image left = UIBuilder.Panel("EdgeLeft", view.panel.transform, arrowColor, false);
        left.rectTransform.anchorMin = new Vector2(0f, 0f);
        left.rectTransform.anchorMax = new Vector2(0f, 1f);
        left.rectTransform.pivot = new Vector2(0f, 0.5f);
        left.rectTransform.sizeDelta = new Vector2(4f, 0f);
        left.rectTransform.anchoredPosition = Vector2.zero;

        Image right = UIBuilder.Panel("EdgeRight", view.panel.transform, arrowColor, false);
        right.rectTransform.anchorMin = new Vector2(1f, 0f);
        right.rectTransform.anchorMax = new Vector2(1f, 1f);
        right.rectTransform.pivot = new Vector2(1f, 0.5f);
        right.rectTransform.sizeDelta = new Vector2(4f, 0f);
        right.rectTransform.anchoredPosition = Vector2.zero;

        view.arrowRow = UIBuilder.Stretch(UIBuilder.Rect("Arrows", view.panel.transform), 16f, 8f, 16f, 8f);
        HorizontalLayoutGroup row = UIBuilder.Horizontal(view.arrowRow, 6f, 0, TextAnchor.MiddleCenter);
        row.childControlWidth = false;
        row.childControlHeight = false;

        view.arrowTemplate = CreateArrowTemplate(view.arrowRow, 30f, arrowColor);

        view.placeholder = UIBuilder.Text("Placeholder", view.panel.transform, "W A S D  •  Q to confirm", 18f,
            TextAlignmentOptions.Center, UIBuilder.MutedText);
        UIBuilder.Stretch(view.placeholder.rectTransform);

        view.message = UIBuilder.Text("Message", canvas.transform, string.Empty, 18f, TextAlignmentOptions.BottomRight, UIBuilder.TextColor);
        UIBuilder.Place(view.message.rectTransform, corner, corner, new Vector2(-margin.x, margin.y + size.y + 6f), new Vector2(size.x + 120f, 26f));

        return view;
    }

    /// <summary>ลูกศรต้นแบบ (สามเหลี่ยมชี้ลง กล่องจัตุรัส) ปิดไว้ — ใช้ร่วมกับหน้า Logs &gt; Command</summary>
    public static Image CreateArrowTemplate(Transform parent, float size, Color color)
    {
        Image image = UIBuilder.Panel("ArrowTemplate", parent, color, false);
        image.sprite = UIBuilder.TriangleSprite;
        image.preserveAspect = true;
        image.rectTransform.sizeDelta = new Vector2(size, size);
        LayoutElement layout = image.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = size;
        layout.preferredHeight = size;
        image.gameObject.SetActive(false);
        return image;
    }
}
