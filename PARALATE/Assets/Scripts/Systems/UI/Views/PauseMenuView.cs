using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้าตาของเมนู Esc (Prefab: Resources/UI/PauseMenu) — จัดตำแหน่ง/ขนาด/สี/ฟอนต์ใน Editor ได้ทุกชิ้น
///   pages      : หน้าเนื้อหา 4 หน้า (Inventory, Logs, Setting, Exit) ระบบเปิดทีละหน้า
///   navButtons : ปุ่มเมนูขวา ลำดับเดียวกับ pages
///   *Template  : ต้นแบบของรายการที่ซ้ำ (ช่อง Inventory, หัวข้อ Log, สูตร Command) ปิดไว้ ระบบ Clone ตอนรัน
/// PauseMenuUI เป็นตัวเติมข้อมูลและผูกปุ่มทั้งหมด
/// </summary>
public class PauseMenuView : MonoBehaviour
{
    public const string PrefabName = "PauseMenu";

    [Header("เมนูขวา (ลำดับ: Inventory, Logs, Setting, Exit)")]
    public Button[] navButtons = new Button[4];
    public GameObject[] pages = new GameObject[4];
    public Color navNormalColor = new Color(1f, 1f, 1f, 0.02f);
    public Color navSelectedColor = new Color(0.25f, 0.7f, 1f, 0.35f);

    [Header("กล่องอธิบาย (ชี้เมาส์)")]
    public GameObject tooltip;
    public TextMeshProUGUI tooltipText;

    [Header("Inventory")]
    public RectTransform inventoryGrid;
    public InventoryCellView cellTemplate;
    [Tooltip("ช่องว่างที่เติมให้เต็มตาราง (ปิดไว้)")]
    public GameObject emptyCellTemplate;
    [Min(1)] public int gridCellCount = 30;

    [Header("Logs")]
    public Button logTabButton;
    public Button commandTabButton;
    public TextMeshProUGUI logCount;
    public ScrollRect logScroll;
    public RectTransform logContent;
    public LogItemView logItemTemplate;
    public CommandItemView commandItemTemplate;
    public TextMeshProUGUI logEmptyText;
    public TextMeshProUGUI logHint;
    public Color logHeaderColor = new Color(1f, 1f, 1f, 0.08f);
    public Color logHeaderOpenColor = new Color(0.25f, 0.7f, 1f, 0.3f);

    [Header("Setting")]
    public Button fullscreenButton;
    public TextMeshProUGUI fullscreenMark;
    public Button qualityButton;
    public TextMeshProUGUI qualityLabel;
    public Slider mainSlider;
    public Slider musicSlider;
    public Slider sfxSlider;
    [Tooltip("ข้อความเตือนตอนไม่มี AudioManager (เริ่มเล่นจากด่านโดยตรง)")]
    public GameObject audioNote;

    [Header("Exit")]
    public Button exitYes;
    public Button exitNo;

    // ================================================================ หน้าตาเริ่มต้น (ใช้สร้าง Prefab ครั้งแรก)

    public static PauseMenuView BuildDefault()
    {
        Canvas canvas = UIBuilder.CreateCanvas(PrefabName, 300);
        PauseMenuView view = canvas.gameObject.AddComponent<PauseMenuView>();
        Transform root = canvas.transform;

        Image dim = UIBuilder.Panel("Dim", root, UIBuilder.DimColor);
        UIBuilder.Stretch(dim.rectTransform);

        Image window = UIBuilder.Panel("Content", root, UIBuilder.WindowColor);
        UIBuilder.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-155f, 0f), new Vector2(780f, 720f));
        RectTransform content = UIBuilder.Stretch(UIBuilder.Rect("Pages", window.transform), 30f, 30f, 30f, 30f);

        // เมนูขวา
        Image nav = UIBuilder.Panel("Nav", root, UIBuilder.WindowColor);
        UIBuilder.Place(nav.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(395f, 0f), new Vector2(300f, 720f));

        Image divider = UIBuilder.Panel("Divider", nav.transform, new Color(1f, 1f, 1f, 0.5f), false);
        divider.rectTransform.anchorMin = new Vector2(0f, 0f);
        divider.rectTransform.anchorMax = new Vector2(0f, 1f);
        divider.rectTransform.offsetMin = Vector2.zero;
        divider.rectTransform.offsetMax = new Vector2(2f, 0f);

        RectTransform list = UIBuilder.Stretch(UIBuilder.Rect("List", nav.transform), 14f, 20f, 14f, 20f);
        UIBuilder.Vertical(list, 6f);

        string[] names = { "inventory", "logs", "setting", "exit" };
        for (int i = 0; i < names.Length; i++)
        {
            Button button = UIBuilder.Button(names[i], list, names[i], null, 30f, view.navNormalColor);
            UIBuilder.LabelOf(button).alignment = TextAlignmentOptions.MidlineLeft;
            UIBuilder.Size(button, -1f, 70f);
            view.navButtons[i] = button;

            RectTransform page = UIBuilder.Stretch(UIBuilder.Rect("Page_" + names[i], content));
            view.pages[i] = page.gameObject;
        }

        TextMeshProUGUI hint = UIBuilder.Text("Hint", nav.transform, "Esc to resume  •  B inventory", 18f, TextAlignmentOptions.Bottom, UIBuilder.MutedText);
        UIBuilder.Stretch(hint.rectTransform, 0f, 16f, 0f, 0f);

        BuildInventory(view, view.pages[0].transform);
        BuildLogs(view, view.pages[1].transform);
        BuildSetting(view, view.pages[2].transform);
        BuildExit(view, view.pages[3].transform);

        // กล่องอธิบายด้านขวาของเนื้อหา (วาดทับทุกหน้า)
        Image tooltip = UIBuilder.Panel("Tooltip", window.transform, new Color(0.13f, 0.15f, 0.2f, 1f), false);
        UIBuilder.Place(tooltip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(330f, 260f));
        view.tooltipText = UIBuilder.Text("Text", tooltip.transform, string.Empty, 20f, TextAlignmentOptions.TopLeft);
        view.tooltipText.richText = true;
        UIBuilder.Stretch(view.tooltipText.rectTransform, 14f, 12f, 14f, 12f);
        view.tooltip = tooltip.gameObject;
        tooltip.gameObject.SetActive(false);

        return view;
    }

    private static TextMeshProUGUI Header(Transform page, string title)
    {
        TextMeshProUGUI header = UIBuilder.Text("Header", page, title, 34f, TextAlignmentOptions.TopLeft);
        UIBuilder.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(400f, 50f));
        return header;
    }

    private static TextMeshProUGUI Hint(Transform page, string text)
    {
        TextMeshProUGUI hint = UIBuilder.Text("Hint", page, text, 18f, TextAlignmentOptions.BottomLeft, UIBuilder.MutedText);
        UIBuilder.Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(700f, 30f));
        return hint;
    }

    private static void BuildInventory(PauseMenuView view, Transform page)
    {
        Header(page, "Inventory");

        RectTransform grid = UIBuilder.Rect("Grid", page);
        UIBuilder.Place(grid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -70f), new Vector2(380f, 460f));
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(72f, 72f);
        layout.spacing = new Vector2(4f, 4f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;
        view.inventoryGrid = grid;

        Image cell = UIBuilder.Panel("CellTemplate", grid, new Color(1f, 1f, 1f, 0.10f));
        InventoryCellView cellView = cell.gameObject.AddComponent<InventoryCellView>();
        cellView.background = cell;
        cellView.icon = UIBuilder.Panel("Icon", cell.transform, Color.white, false);
        cellView.icon.preserveAspect = true;
        UIBuilder.Stretch(cellView.icon.rectTransform, 8f, 16f, 8f, 6f);
        cellView.label = UIBuilder.Text("Label", cell.transform, "Item", 15f, TextAlignmentOptions.Center);
        UIBuilder.Stretch(cellView.label.rectTransform, 2f, 16f, 2f, 2f);
        cellView.amount = UIBuilder.Text("Amount", cell.transform, "0", 15f, TextAlignmentOptions.BottomRight);
        cellView.amount.richText = true;
        cellView.amount.textWrappingMode = TextWrappingModes.NoWrap;
        cellView.amount.overflowMode = TextOverflowModes.Ellipsis;
        UIBuilder.Stretch(cellView.amount.rectTransform, 2f, 1f, 4f, 2f);
        cell.gameObject.SetActive(false);
        view.cellTemplate = cellView;

        Image empty = UIBuilder.Panel("EmptyCellTemplate", grid, new Color(1f, 1f, 1f, 0.04f), false);
        empty.gameObject.SetActive(false);
        view.emptyCellTemplate = empty.gameObject;

        Hint(page, "Hover for details  •  Call items and weapons with Q (Command)");
    }

    private static void BuildLogs(PauseMenuView view, Transform page)
    {
        // แท็บ "Log | Command"
        RectTransform tabs = UIBuilder.Rect("Tabs", page);
        UIBuilder.Place(tabs, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(420f, 48f));
        HorizontalLayoutGroup row = UIBuilder.Horizontal(tabs, 0f);
        row.childControlWidth = true;

        view.logTabButton = UIBuilder.Button("LogTab", tabs, "Log", null, 34f, new Color(1f, 1f, 1f, 0f));
        UIBuilder.Size(view.logTabButton, 90f, 48f);
        TextMeshProUGUI bar = UIBuilder.Text("Bar", tabs, "|", 34f, TextAlignmentOptions.Center, UIBuilder.MutedText);
        UIBuilder.Size(bar, 30f, 48f);
        view.commandTabButton = UIBuilder.Button("CommandTab", tabs, "Command", null, 34f, new Color(1f, 1f, 1f, 0f));
        UIBuilder.Size(view.commandTabButton, 190f, 48f);

        view.logCount = UIBuilder.Text("Count", page, "0 / 0", 20f, TextAlignmentOptions.TopRight, UIBuilder.MutedText);
        UIBuilder.Place(view.logCount.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -10f), new Vector2(200f, 30f));

        // พื้นที่เลื่อน
        Image area = UIBuilder.Panel("LogArea", page, new Color(0f, 0f, 0f, 0.001f));
        UIBuilder.Stretch(area.rectTransform, 0f, 34f, 0f, 60f);
        ScrollRect scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        RectTransform viewport = UIBuilder.Stretch(UIBuilder.Rect("Viewport", area.transform), 0f, 0f, 16f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll.viewport = viewport;

        RectTransform logContent = UIBuilder.Rect("Content", viewport);
        logContent.anchorMin = new Vector2(0f, 1f);
        logContent.anchorMax = new Vector2(1f, 1f);
        logContent.pivot = new Vector2(0.5f, 1f);
        logContent.anchoredPosition = Vector2.zero;
        logContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup column = logContent.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(0, 0, 0, 12);
        column.spacing = 10f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        logContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = logContent;

        Image track = UIBuilder.Panel("Scrollbar", area.transform, new Color(1f, 1f, 1f, 0.05f));
        track.rectTransform.anchorMin = new Vector2(1f, 0f);
        track.rectTransform.anchorMax = new Vector2(1f, 1f);
        track.rectTransform.pivot = new Vector2(1f, 0.5f);
        track.rectTransform.sizeDelta = new Vector2(10f, 0f);
        track.rectTransform.anchoredPosition = Vector2.zero;
        Image handle = UIBuilder.Panel("Handle", track.transform, new Color(1f, 1f, 1f, 0.35f));
        UIBuilder.Stretch(handle.rectTransform);
        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        view.logScroll = scroll;
        view.logContent = logContent;

        view.logEmptyText = UIBuilder.Text("Empty", logContent, "No logs yet.", 22f, TextAlignmentOptions.Center, UIBuilder.MutedText);
        view.logEmptyText.gameObject.AddComponent<LayoutElement>().minHeight = 80f;

        view.logItemTemplate = BuildLogItemTemplate(view, logContent);
        view.commandItemTemplate = BuildCommandItemTemplate(logContent);

        view.logHint = Hint(page, "Click a title to open / close");
    }

    private static LogItemView BuildLogItemTemplate(PauseMenuView view, Transform parent)
    {
        RectTransform item = UIBuilder.Rect("LogItemTemplate", parent);
        VerticalLayoutGroup stack = item.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;
        LogItemView log = item.gameObject.AddComponent<LogItemView>();

        log.header = UIBuilder.Panel("Header", item, view.logHeaderColor);
        log.header.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
        log.headerButton = log.header.gameObject.AddComponent<Button>();
        log.headerButton.targetGraphic = log.header;
        log.header.gameObject.AddComponent<UIPointerRelay>().playSounds = true;

        Image outline = UIBuilder.Panel("Accent", log.header.transform, new Color(1f, 1f, 1f, 0.5f), false);
        outline.rectTransform.anchorMin = new Vector2(0f, 0f);
        outline.rectTransform.anchorMax = new Vector2(0f, 1f);
        outline.rectTransform.offsetMin = Vector2.zero;
        outline.rectTransform.offsetMax = new Vector2(4f, 0f);

        log.title = UIBuilder.Text("Title", log.header.transform, "Title", 28f, TextAlignmentOptions.MidlineLeft);
        log.title.textWrappingMode = TextWrappingModes.NoWrap;
        log.title.overflowMode = TextOverflowModes.Ellipsis;
        UIBuilder.Stretch(log.title.rectTransform, 22f, 0f, 130f, 0f);

        TextMeshProUGUI badge = UIBuilder.Text("New", log.header.transform, "NEW", 16f, TextAlignmentOptions.MidlineRight, UIBuilder.AccentColor);
        badge.fontStyle = FontStyles.Bold;
        UIBuilder.Place(badge.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-64f, 0f), new Vector2(60f, 30f));
        log.newBadge = badge.gameObject;

        log.arrow = UIBuilder.Panel("Arrow", log.header.transform, UIBuilder.TextColor, false);
        log.arrow.sprite = UIBuilder.TriangleSprite;
        UIBuilder.Place(log.arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-32f, 0f), new Vector2(22f, 16f));

        Image info = UIBuilder.Panel("Info", item, new Color(0f, 0f, 0f, 0.3f), false);
        VerticalLayoutGroup infoLayout = info.gameObject.AddComponent<VerticalLayoutGroup>();
        infoLayout.padding = new RectOffset(26, 22, 16, 18);
        infoLayout.childControlWidth = true;
        infoLayout.childControlHeight = true;
        infoLayout.childForceExpandWidth = true;
        infoLayout.childForceExpandHeight = false;
        log.body = UIBuilder.Text("Body", info.transform, "Body", 22f, TextAlignmentOptions.TopLeft);
        log.body.richText = true;
        log.info = info.gameObject;
        info.gameObject.SetActive(false);

        item.gameObject.SetActive(false);
        return log;
    }

    private static CommandItemView BuildCommandItemTemplate(Transform parent)
    {
        RectTransform item = UIBuilder.Rect("CommandItemTemplate", parent);
        VerticalLayoutGroup stack = item.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;
        CommandItemView command = item.gameObject.AddComponent<CommandItemView>();

        Image header = UIBuilder.Panel("Header", item, new Color(1f, 1f, 1f, 0.08f), false);
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
        command.title = UIBuilder.Text("Title", header.transform, "Command", 24f, TextAlignmentOptions.MidlineLeft);
        UIBuilder.Stretch(command.title.rectTransform, 16f, 0f, 120f, 0f);
        command.amount = UIBuilder.Text("Amount", header.transform, "x0", 18f, TextAlignmentOptions.MidlineRight, UIBuilder.MutedText);
        UIBuilder.Stretch(command.amount.rectTransform, 16f, 0f, 16f, 0f);

        Image arrows = UIBuilder.Panel("Arrows", item, new Color(0f, 0f, 0f, 0.3f), false);
        arrows.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
        command.arrowRow = UIBuilder.Stretch(UIBuilder.Rect("Row", arrows.transform), 18f, 6f, 18f, 6f);
        HorizontalLayoutGroup row = UIBuilder.Horizontal(command.arrowRow, 8f);
        row.childControlWidth = false;
        row.childControlHeight = false;
        command.arrowTemplate = CommandLineView.CreateArrowTemplate(command.arrowRow, 30f, UIBuilder.AccentColor);

        item.gameObject.SetActive(false);
        return command;
    }

    private static void BuildSetting(PauseMenuView view, Transform page)
    {
        Header(page, "Setting");

        TextMeshProUGUI fullscreenLabel = UIBuilder.Text("FullscreenLabel", page, "FullScreen", 26f);
        UIBuilder.Place(fullscreenLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -90f), new Vector2(220f, 44f));
        view.fullscreenButton = UIBuilder.Button("FullscreenCheck", page, "X", null, 26f, new Color(1f, 1f, 1f, 0.18f));
        UIBuilder.Place((RectTransform)view.fullscreenButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(240f, -90f), new Vector2(44f, 44f));
        view.fullscreenMark = UIBuilder.LabelOf(view.fullscreenButton);

        TextMeshProUGUI soundHeader = UIBuilder.Text("SoundHeader", page, "Sound", 26f);
        UIBuilder.Place(soundHeader.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -170f), new Vector2(220f, 44f));
        view.mainSlider = VolumeRow(page, "Main", -230f);
        view.musicSlider = VolumeRow(page, "Music", -290f);
        view.sfxSlider = VolumeRow(page, "SFX", -350f);

        TextMeshProUGUI note = UIBuilder.Text("AudioNote", page, "Music / SFX sliders need the AudioManager\n(start the game from MainMenu).", 18f,
            TextAlignmentOptions.TopLeft, UIBuilder.MutedText);
        UIBuilder.Place(note.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -290f), new Vector2(560f, 60f));
        note.gameObject.SetActive(false);
        view.audioNote = note.gameObject;

        TextMeshProUGUI graphicsLabel = UIBuilder.Text("GraphicsLabel", page, "Graphics", 26f);
        UIBuilder.Place(graphicsLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -430f), new Vector2(220f, 44f));
        view.qualityButton = UIBuilder.Button("GraphicsQuality", page, "Quality", null, 24f, new Color(1f, 1f, 1f, 0.18f));
        UIBuilder.Place((RectTransform)view.qualityButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(180f, -430f), new Vector2(380f, 44f));
        view.qualityLabel = UIBuilder.LabelOf(view.qualityButton);
    }

    private static Slider VolumeRow(Transform page, string label, float y)
    {
        TextMeshProUGUI text = UIBuilder.Text(label, page, label, 24f);
        UIBuilder.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), new Vector2(140f, 40f));

        Slider slider = UIBuilder.Slider(label + "Slider", page, 1f, null);
        UIBuilder.Place((RectTransform)slider.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(180f, y), new Vector2(380f, 40f));
        return slider;
    }

    private static void BuildExit(PauseMenuView view, Transform page)
    {
        TextMeshProUGUI question = UIBuilder.Text("Question", page, "exit game?", 34f, TextAlignmentOptions.Center);
        UIBuilder.Place(question.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(600f, 60f));

        TextMeshProUGUI note = UIBuilder.Text("Note", page, "Return to the main menu. Items collected this run will be lost.", 18f,
            TextAlignmentOptions.Center, UIBuilder.MutedText);
        UIBuilder.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(640f, 40f));

        view.exitYes = UIBuilder.Button("Yes", page, "YES", null, 28f, new Color(0.8f, 0.2f, 0.2f, 0.6f));
        UIBuilder.Place((RectTransform)view.exitYes.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -270f), new Vector2(300f, 64f));

        view.exitNo = UIBuilder.Button("No", page, "NO", null, 28f);
        UIBuilder.Place((RectTransform)view.exitNo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(300f, 64f));
    }
}
