using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้าตาหน้าต่างเซฟ (Prefab: Resources/UI/SaveSlots) ใช้ร่วมกัน 2 ที่:
///   ในเกม (สูตร SaveGame)      : ปุ่ม Save / Load / ลบ
///   Main Menu (ปุ่ม Load Game) : กดแถวเพื่อโหลด / X ลบ (Yes / No) / X มุมขวาบน = กลับเมนู
/// ช่องเซฟเป็นไฟล์เดียวกัน (SaveSystem) จึงเห็นเซฟเดียวกันทั้งสองที่
/// </summary>
public class SaveSlotsView : MonoBehaviour
{
    public const string PrefabName = "SaveSlots";

    public GameObject window;
    public TextMeshProUGUI title;
    public Button closeButton;
    [Tooltip("ที่วางช่องเซฟ (มี VerticalLayoutGroup)")]
    public RectTransform list;
    public SaveSlotView slotTemplate;
    public TextMeshProUGUI status;

    [Header("กล่องยืนยัน (Delete / Overwrite)")]
    public GameObject confirmDialog;
    public TextMeshProUGUI confirmText;
    public Button confirmYes;
    public Button confirmNo;

    [Header("ข้อความหัวหน้าต่าง")]
    public string inGameTitle = "SAVE & LOAD";
    public string mainMenuTitle = "Load Game";

    public static SaveSlotsView BuildDefault()
    {
        Canvas canvas = UIBuilder.CreateCanvas(PrefabName, 310);
        SaveSlotsView view = canvas.gameObject.AddComponent<SaveSlotsView>();

        Image dim = UIBuilder.Panel("Dim", canvas.transform, UIBuilder.DimColor);
        UIBuilder.Stretch(dim.rectTransform);

        Image window = UIBuilder.Panel("Window", canvas.transform, UIBuilder.WindowColor);
        UIBuilder.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 600f));
        view.window = window.gameObject;

        view.title = UIBuilder.Text("Title", window.transform, "Load Game", 34f, TextAlignmentOptions.TopLeft);
        UIBuilder.Stretch(view.title.rectTransform, 30f, 0f, 90f, 26f);

        view.closeButton = UIBuilder.Button("Close", window.transform, "X", null, 30f, new Color(0.8f, 0.15f, 0.15f, 0.9f));
        UIBuilder.Place((RectTransform)view.closeButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(56f, 56f));

        view.list = UIBuilder.Stretch(UIBuilder.Rect("Slots", window.transform), 30f, 80f, 30f, 90f);
        UIBuilder.Vertical(view.list, 14f);

        view.slotTemplate = BuildSlotTemplate(view.list);

        view.status = UIBuilder.Text("Status", window.transform, string.Empty, 20f, TextAlignmentOptions.BottomLeft, UIBuilder.MutedText);
        UIBuilder.Stretch(view.status.rectTransform, 30f, 26f, 30f, 520f);

        // กล่องยืนยัน
        Image confirm = UIBuilder.Panel("ConfirmDialog", canvas.transform, new Color(0f, 0f, 0f, 0.6f));
        UIBuilder.Stretch(confirm.rectTransform);
        Image box = UIBuilder.Panel("Box", confirm.transform, new Color(0.12f, 0.14f, 0.18f, 1f));
        UIBuilder.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 240f));
        view.confirmText = UIBuilder.Text("Text", box.transform, "Delete this save?", 28f, TextAlignmentOptions.Center);
        UIBuilder.Stretch(view.confirmText.rectTransform, 20f, 100f, 20f, 20f);
        view.confirmYes = UIBuilder.Button("Yes", box.transform, "Yes", null, 26f, new Color(0.8f, 0.2f, 0.2f, 0.6f));
        UIBuilder.Place((RectTransform)view.confirmYes.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-110f, 24f), new Vector2(180f, 60f));
        view.confirmNo = UIBuilder.Button("No", box.transform, "No", null, 26f);
        UIBuilder.Place((RectTransform)view.confirmNo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(110f, 24f), new Vector2(180f, 60f));
        view.confirmDialog = confirm.gameObject;
        confirm.gameObject.SetActive(false);

        return view;
    }

    private static SaveSlotView BuildSlotTemplate(Transform parent)
    {
        Image row = UIBuilder.Panel("SlotTemplate", parent, UIBuilder.PanelColor);
        UIBuilder.Size(row, -1f, 110f);
        SaveSlotView slot = row.gameObject.AddComponent<SaveSlotView>();

        slot.rowButton = row.gameObject.AddComponent<Button>();
        slot.rowButton.targetGraphic = row;
        row.gameObject.AddComponent<UIPointerRelay>().playSounds = true;

        slot.info = UIBuilder.Text("Info", row.transform, "Scene, Time Save", 24f, TextAlignmentOptions.MidlineLeft);
        slot.info.richText = true;
        UIBuilder.Stretch(slot.info.rectTransform, 20f, 0f, 400f, 0f);

        slot.emptyText = UIBuilder.Text("Empty", row.transform, "- No Data -", 26f, TextAlignmentOptions.Center, UIBuilder.MutedText);
        UIBuilder.Stretch(slot.emptyText.rectTransform);

        slot.saveButton = UIBuilder.Button("Save", row.transform, "Save", null, 22f);
        UIBuilder.Place((RectTransform)slot.saveButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-230f, 0f), new Vector2(150f, 56f));

        slot.loadButton = UIBuilder.Button("Load", row.transform, "Load", null, 22f);
        UIBuilder.Place((RectTransform)slot.loadButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(150f, 56f));

        slot.deleteButton = UIBuilder.Button("Delete", row.transform, "X", null, 20f, new Color(0.8f, 0.15f, 0.15f, 0.7f));
        UIBuilder.Place((RectTransform)slot.deleteButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(36f, 36f));

        row.gameObject.SetActive(false);
        return slot;
    }
}
