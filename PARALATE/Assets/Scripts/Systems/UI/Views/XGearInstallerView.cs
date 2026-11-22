using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้าตาหน้า X-Gear Installer (Prefab: Resources/UI/XGearInstaller)
///   cards: ลำดับ Active, Passive, Maps — ย้าย/ปรับขนาดการ์ดใน Editor ได้
/// </summary>
public class XGearInstallerView : MonoBehaviour
{
    public const string PrefabName = "XGearInstaller";

    public TextMeshProUGUI title;
    public TextMeshProUGUI hint;
    public Button closeButton;
    [Tooltip("ลำดับต้องเป็น Active, Passive, Maps")]
    public InstallerCardView[] cards = new InstallerCardView[3];

    [Header("Look")]
    public Color headerColor = new Color(0.22f, 0.24f, 0.28f, 1f);
    public Color headerOpenColor = new Color(0.35f, 0.35f, 0.38f, 1f);

    public static XGearInstallerView BuildDefault()
    {
        Canvas canvas = UIBuilder.CreateCanvas(PrefabName, 210);
        XGearInstallerView view = canvas.gameObject.AddComponent<XGearInstallerView>();
        Transform root = canvas.transform;

        view.title = UIBuilder.Text("Title", root, "X-GEAR INSTALLER", 30f, TextAlignmentOptions.TopLeft);
        UIBuilder.Place(view.title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -40f), new Vector2(700f, 50f));

        view.hint = UIBuilder.Text("Hint", root, "Click a card to change its data  •  Esc to close", 20f, TextAlignmentOptions.TopLeft, UIBuilder.MutedText);
        UIBuilder.Place(view.hint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -90f), new Vector2(900f, 30f));

        view.closeButton = UIBuilder.Button("Close", root, "X", null, 30f, new Color(0.8f, 0.15f, 0.15f, 0.9f));
        UIBuilder.Place((RectTransform)view.closeButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(56f, 56f));

        string[] names = { "Active", "Passive", "Maps" };
        for (int i = 0; i < names.Length; i++)
        {
            // วางการ์ดเรียงแนวนอน ชิดล่างกลางจอ (ย้ายได้อิสระใน Editor)
            float x = (i - 1) * 410f;
            view.cards[i] = BuildCard(view, root, names[i], new Vector2(x, 60f));
        }

        return view;
    }

    private static InstallerCardView BuildCard(XGearInstallerView view, Transform parent, string name, Vector2 position)
    {
        RectTransform card = UIBuilder.Rect("Card_" + name, parent);
        UIBuilder.Place(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), position, new Vector2(380f, 560f));
        VerticalLayoutGroup stack = card.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.childAlignment = TextAnchor.LowerCenter;
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;

        InstallerCardView view2 = card.gameObject.AddComponent<InstallerCardView>();

        // รายการเลือก (เหนือหัวการ์ด)
        view2.dropdown = UIBuilder.Rect("Dropdown", card);
        VerticalLayoutGroup list = UIBuilder.Vertical(view2.dropdown, 0f);
        list.childAlignment = TextAnchor.LowerCenter;

        view2.optionTemplate = UIBuilder.Button("OptionTemplate", view2.dropdown, "Chip  x1", null, 18f, new Color(0.12f, 0.14f, 0.18f, 1f));
        UIBuilder.LabelOf(view2.optionTemplate).alignment = TextAlignmentOptions.MidlineLeft;
        UIBuilder.Size(view2.optionTemplate, -1f, 48f);
        view2.optionTemplate.gameObject.SetActive(false);

        Image empty = UIBuilder.Panel("Empty", view2.dropdown, new Color(0.12f, 0.14f, 0.18f, 1f), false);
        UIBuilder.Size(empty, -1f, 70f);
        TextMeshProUGUI emptyLabel = UIBuilder.Text("Text", empty.transform, "No data in inventory", 18f, TextAlignmentOptions.Center, UIBuilder.MutedText);
        UIBuilder.Stretch(emptyLabel.rectTransform);
        view2.emptyText = empty.gameObject;

        view2.removeButton = UIBuilder.Button("Remove", view2.dropdown, "Remove (back to inventory)", null, 18f, new Color(0.5f, 0.15f, 0.15f, 1f));
        UIBuilder.Size(view2.removeButton, -1f, 48f);
        view2.dropdown.gameObject.SetActive(false);

        // หัวการ์ด "Name ▽"
        view2.header = UIBuilder.Panel("Header", card, view.headerColor);
        UIBuilder.Size(view2.header, -1f, 44f);
        view2.headerButton = view2.header.gameObject.AddComponent<Button>();
        view2.headerButton.targetGraphic = view2.header;
        view2.header.gameObject.AddComponent<UIPointerRelay>().playSounds = true;

        view2.headerText = UIBuilder.Text("Name", view2.header.transform, "Name", 22f, TextAlignmentOptions.MidlineLeft);
        view2.headerText.textWrappingMode = TextWrappingModes.NoWrap;
        view2.headerText.overflowMode = TextOverflowModes.Ellipsis;
        UIBuilder.Stretch(view2.headerText.rectTransform, 14f, 0f, 44f, 0f);

        view2.arrow = UIBuilder.Panel("Arrow", view2.header.transform, UIBuilder.TextColor, false);
        view2.arrow.sprite = UIBuilder.TriangleSprite;
        UIBuilder.Place(view2.arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-24f, 0f), new Vector2(20f, 15f));

        // เนื้อการ์ด: ชนิด / ชื่อ / คำอธิบาย
        Image body = UIBuilder.Panel("Body", card, UIBuilder.WindowColor, false);
        UIBuilder.Size(body, -1f, 150f);
        view2.body = UIBuilder.Text("Text", body.transform, $"<b>{name}</b>\nName\nDescription", 22f, TextAlignmentOptions.TopLeft);
        view2.body.richText = true;
        UIBuilder.Stretch(view2.body.rectTransform, 16f, 10f, 16f, 12f);

        return view2;
    }
}
