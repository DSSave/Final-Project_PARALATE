using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// เมนูตอนกด Esc ในเกม (ทุก Scene ที่มีผู้เล่น): เนื้อหาซ้าย / เมนูขวา
///   Inventory : ตารางช่องของ (อาวุธที่เป็นเจ้าของ, ของใช้, กระสุน, Scrap, Metal, ชิป, Keycard)
///   Logs      : แท็บ Log = Memo กดหัวข้อเพื่อเปิดอ่าน (GameLog / Resources/Logs)
///               แท็บ Command = สูตรลูกศรของไอเทมที่เคยเก็บแล้ว (CommandBook)
///   Setting   : Fullscreen + เสียง Main / Music / SFX + คุณภาพกราฟิก
///   Exit      : ถามยืนยัน Yes / No
///
/// หน้าตาทั้งหมดอยู่ใน Prefab Resources/UI/PauseMenu (PauseMenuView) จัดตำแหน่ง/สี/ฟอนต์ใน Editor ได้
/// สคริปต์นี้เติมข้อมูล ผูกปุ่ม และ Clone Template ของรายการที่ซ้ำ
/// เปิดเมนู = หยุดเวลา (Time.timeScale = 0) และล็อกผู้เล่นด้วยระบบล็อกแบบมีเจ้าของ
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    private enum Page { Inventory, Logs, Setting, Exit }
    private enum LogTab { Log, Command }

    private TopDownPlayerController player;
    private PickupSystem pickup;

    private PauseMenuView view;
    private Page currentPage = Page.Inventory;
    private LogTab currentLogTab = LogTab.Log;
    private bool isOpen;
    private bool lockedLastFrame;
    private float timeScaleBeforePause = 1f;

    public bool IsOpen => isOpen;

    public static void EnsureInScene(TopDownPlayerController player)
    {
        if (player == null || FindAnyObjectByType<PauseMenuUI>() != null)
            return;

        GameObject go = new GameObject("PauseMenu (Auto)");
        PauseMenuUI menu = go.AddComponent<PauseMenuUI>();
        menu.player = player;
        menu.pickup = player.GetComponent<PickupSystem>();
    }

    private void OnEnable()
    {
        PersistentInventory.OnChanged += RefreshIfOpen;
        RunInventory.OnChanged += RefreshIfOpen;
        GameLog.OnUnlocked += HandleLogUnlocked;
        CommandBook.OnLearned += HandleCommandLearned;
    }

    private void OnDisable()
    {
        PersistentInventory.OnChanged -= RefreshIfOpen;
        RunInventory.OnChanged -= RefreshIfOpen;
        GameLog.OnUnlocked -= HandleLogUnlocked;
        CommandBook.OnLearned -= HandleCommandLearned;

        if (isOpen)
            Close();
    }

    private void OnDestroy()
    {
        if (view != null)
            Destroy(view.gameObject);
    }

    private void Update()
    {
        // ปุ่มอยู่ใน PlayerInput.inputactions: Pause (Esc) / Inventory (B)
        bool escape = PlayerControls.WasPressed(PlayerControls.Pause, Key.Escape);
        bool inventoryKey = PlayerControls.WasPressed(PlayerControls.Inventory, Key.B);

        if (!escape && !inventoryKey)
            return;

        if (isOpen)
        {
            // B ตอนเมนูเปิดอยู่หน้าอื่น = สลับไปหน้า Inventory / กดซ้ำที่หน้า Inventory = ปิด
            if (inventoryKey && currentPage != Page.Inventory)
                SelectPage(Page.Inventory);
            else
                Close();

            return;
        }

        // เปิดได้เฉพาะตอนผู้เล่นไม่ได้ถูกล็อกโดยอย่างอื่น (โน้ต / หน้าเลือกด่าน / Upgrade / Eye Wide / ตาย)
        // เช็กค่าของ "เฟรมก่อน" ด้วย เพราะหน้าต่างอื่นใช้ Esc ปิดตัวเองในเฟรมเดียวกันนี้
        if (player == null || player.IsMovementLocked || lockedLastFrame)
            return;

        if (LevelManager.IsTransitionRunning)
            return;

        Open(Page.Inventory);
    }

    private void LateUpdate()
    {
        lockedLastFrame = player != null && player.IsMovementLocked && !isOpen;
    }

    public void Open()
    {
        Open(Page.Inventory);
    }

    private void Open(Page page)
    {
        if (isOpen)
            return;

        EnsureUI();
        isOpen = true;
        view.gameObject.SetActive(true);

        timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        player.SetMovementLocked(this, true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SfxLibrary.PlayUI("menu_open");
        SelectPage(page);
    }

    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;

        if (view != null)
            view.gameObject.SetActive(false);

        Time.timeScale = timeScaleBeforePause;

        if (player != null)
            player.SetMovementLocked(this, false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        if (AudioManager.Instance != null)
            AudioManager.Instance.SaveSettings();

        SfxLibrary.PlayUI("menu_close");
    }

    // ------------------------------------------------------------ โครงหน้าต่าง

    private void EnsureUI()
    {
        if (view != null)
            return;

        view = UIPrefabs.Spawn(PauseMenuView.PrefabName, PauseMenuView.BuildDefault);

        // ผูกปุ่มครั้งเดียว (ปุ่มอยู่ใน Prefab แก้ตำแหน่ง/หน้าตาได้ แต่การทำงานมาจากที่นี่)
        for (int i = 0; i < view.navButtons.Length; i++)
        {
            Page page = (Page)i;
            if (view.navButtons[i] != null)
                view.navButtons[i].onClick.AddListener(() => SelectPage(page));
        }

        if (view.logTabButton != null) view.logTabButton.onClick.AddListener(() => SelectLogTab(LogTab.Log));
        if (view.commandTabButton != null) view.commandTabButton.onClick.AddListener(() => SelectLogTab(LogTab.Command));

        if (view.fullscreenButton != null)
        {
            view.fullscreenButton.onClick.AddListener(() =>
            {
                bool value = !Screen.fullScreen;
                Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                Screen.fullScreen = value;
                if (view.fullscreenMark != null) view.fullscreenMark.text = value ? "X" : string.Empty;
            });
        }

        if (view.qualityButton != null)
        {
            view.qualityButton.onClick.AddListener(() =>
            {
                string name = GraphicsQuality.Cycle();
                if (view.qualityLabel != null) view.qualityLabel.text = name;
            });
        }

        BindVolume(view.mainSlider, value =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(value);
            else AudioListener.volume = value;
        });
        BindVolume(view.musicSlider, value => AudioManager.Instance?.SetMusicVolume(value));
        BindVolume(view.sfxSlider, value => AudioManager.Instance?.SetSfxVolume(value));

        if (view.exitYes != null) view.exitYes.onClick.AddListener(ExitToMainMenu);
        if (view.exitNo != null) view.exitNo.onClick.AddListener(() => SelectPage(Page.Inventory));

        if (view.tooltip != null)
            view.tooltip.SetActive(false);

        view.gameObject.SetActive(false);
    }

    private static void BindVolume(Slider slider, System.Action<float> onChanged)
    {
        if (slider != null)
            slider.onValueChanged.AddListener(value => onChanged(value));
    }

    private void SelectPage(Page page)
    {
        currentPage = page;

        for (int i = 0; i < view.navButtons.Length; i++)
        {
            Button button = view.navButtons[i];
            if (button == null)
                continue;

            bool selected = i == (int)page;
            button.targetGraphic.color = selected ? view.navSelectedColor : view.navNormalColor;
            TextMeshProUGUI label = UIBuilder.LabelOf(button);
            if (label != null)
                label.fontStyle = selected ? FontStyles.Underline | FontStyles.Bold : FontStyles.Normal;
        }

        for (int i = 0; i < view.pages.Length; i++)
        {
            if (view.pages[i] != null)
                view.pages[i].SetActive(i == (int)page);
        }

        Rebuild();
    }

    private void RefreshIfOpen()
    {
        if (isOpen)
            Rebuild();
    }

    private void Rebuild()
    {
        HideTooltip();

        switch (currentPage)
        {
            case Page.Inventory: RefreshInventory(); break;
            case Page.Logs: RefreshLogs(); break;
            case Page.Setting: RefreshSetting(); break;
        }
    }

    private void ShowTooltip(string title, string body)
    {
        if (view.tooltip == null || view.tooltipText == null)
            return;

        view.tooltipText.text = $"<b>{title}</b>\n<size=18>{body}</size>";
        view.tooltip.SetActive(true);
        view.tooltip.transform.SetAsLastSibling();
    }

    private void HideTooltip()
    {
        if (view != null && view.tooltip != null)
            view.tooltip.SetActive(false);
    }

    // ------------------------------------------------------------ Inventory

    private void RefreshInventory()
    {
        if (view.inventoryGrid == null || view.cellTemplate == null)
            return;

        UIPrefabs.ClearClones(view.inventoryGrid, view.cellTemplate, view.emptyCellTemplate != null ? view.emptyCellTemplate.transform : null);
        int max = Mathf.Max(1, view.gridCellCount);
        int used = 0;

        // Keycard ที่เก็บได้ในด่านนี้ (ใช้แล้วไม่หมด บัตรระดับสูงเปิดประตูระดับต่ำกว่าได้ด้วย)
        PlayerKeycards keycards = PlayerKeycards.Instance;
        if (keycards != null)
        {
            IReadOnlyList<int> levels = keycards.OwnedLevels;
            for (int i = 0; i < levels.Count && used < max; i++)
            {
                int level = levels[i];
                AddCell("Lv." + level, "KEY", new Color(1f, 0.85f, 0.3f), null, "Keycard Lv." + level,
                    $"Opens doors that need Keycard Lv.{level} or lower.\nNot used up when opening a door.\n\nKept for this mission only.");
                used++;
            }
        }

        // ไอเทมตามลำดับใน ItemCatalog: ของถาวร + ของที่เก็บในรอบนี้ (ยังไม่ถาวรจนกว่าจะจบด่าน)
        PersistentInventory persistent = PersistentInventory.Instance;
        RunInventory run = RunInventory.Instance;

        // ให้ id ที่ไม่อยู่ใน Catalog (ResourceData ใหม่) ถูกลงทะเบียนก่อนวน
        if (persistent != null)
            foreach (KeyValuePair<string, int> pair in persistent.Snapshot()) ItemCatalog.Get(pair.Key);
        if (run != null)
            foreach (KeyValuePair<string, int> pair in run.Snapshot()) ItemCatalog.Get(pair.Key);

        IReadOnlyList<ItemDef> all = ItemCatalog.All;
        for (int i = 0; i < all.Count && used < max; i++)
        {
            ItemDef def = all[i];
            int owned = persistent != null ? persistent.GetAmount(def.id) : 0;
            int pending = run != null ? run.GetAmount(def.id) : 0;

            if (owned + pending <= 0)
                continue;

            string amount = pending > 0 ? $"{owned}<color=#7CFF8A>+{pending}</color>" : owned.ToString();
            string body = def.description + $"\n\nOwned: {owned}" +
                          (pending > 0 ? $"\nThis run: +{pending} (kept when you finish the mission)" : string.Empty);

            AddCell(amount, ShortLabel(def), def.color, ItemCatalog.IconOf(def.id), def.displayName, body);
            used++;
        }

        // ช่องว่างที่เหลือ ให้เห็นเป็นตาราง
        if (view.emptyCellTemplate != null)
        {
            for (; used < max; used++)
            {
                GameObject empty = Instantiate(view.emptyCellTemplate, view.inventoryGrid, false);
                empty.SetActive(true);
            }
        }
    }

    private static string ShortLabel(ItemDef def)
    {
        switch (def.category)
        {
            case ItemCategory.Ammo: return def.displayName.Replace(" Ammo", string.Empty);
            case ItemCategory.PassiveChip: return "P-Chip";
            case ItemCategory.ActiveChip: return "A-Chip";
            case ItemCategory.MapData: return "Map";
            default: return def.displayName;
        }
    }

    private void AddCell(string amount, string label, Color color, Sprite icon, string tooltipTitle, string tooltipBody)
    {
        InventoryCellView cell = UIPrefabs.Clone(view.cellTemplate, view.inventoryGrid);

        if (cell.icon != null)
        {
            cell.icon.sprite = icon;
            cell.icon.gameObject.SetActive(icon != null);
        }

        if (cell.label != null)
        {
            cell.label.text = label;
            cell.label.color = color;
            cell.label.gameObject.SetActive(icon == null);
        }

        if (cell.amount != null)
            cell.amount.text = amount;

        UIPointerRelay relay = UIPrefabs.Relay(cell);
        relay.onEnter = () => ShowTooltip(tooltipTitle, tooltipBody);
        relay.onExit = HideTooltip;
    }

    // ------------------------------------------------------------ Logs (แท็บ Log | Command)

    private void SelectLogTab(LogTab tab)
    {
        if (currentLogTab == tab)
            return;

        currentLogTab = tab;
        RefreshLogs();
    }

    private void RefreshLogs()
    {
        if (view.logContent == null)
            return;

        StyleTab(view.logTabButton, currentLogTab == LogTab.Log);
        StyleTab(view.commandTabButton, currentLogTab == LogTab.Command);

        UIPrefabs.ClearClones(view.logContent, view.logItemTemplate, view.commandItemTemplate, view.logEmptyText);

        int shown;
        if (currentLogTab == LogTab.Log)
        {
            IReadOnlyList<LogEntryData> all = GameLog.All;
            shown = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (!GameLog.IsUnlocked(all[i]))
                    continue;

                AddLogItem(all[i]);
                shown++;
            }

            if (view.logCount != null)
            {
                view.logCount.gameObject.SetActive(true);
                view.logCount.text = $"{shown} / {all.Count}";
            }

            SetEmptyText(shown == 0, "No logs yet.");
            SetHint("Click a title to open / close");
        }
        else
        {
            IReadOnlyList<CommandRecipe> all = CommandBook.All;
            shown = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (!CommandBook.IsKnown(all[i]))
                    continue;

                AddCommandItem(all[i]);
                shown++;
            }

            if (view.logCount != null)
                view.logCount.gameObject.SetActive(false);

            SetEmptyText(shown == 0, "No commands yet.\nPick up items to learn their commands.");
            SetHint("Q to open  •  W A S D = arrows  •  Q to confirm");
        }

        if (view.logScroll != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(view.logContent);
            view.logScroll.verticalNormalizedPosition = 1f; // เริ่มที่หัวข้อแรก
        }
    }

    private static void StyleTab(Button tab, bool selected)
    {
        TextMeshProUGUI label = tab != null ? UIBuilder.LabelOf(tab) : null;
        if (label == null)
            return;

        label.fontStyle = selected ? FontStyles.Bold | FontStyles.Underline : FontStyles.Normal;
        label.color = selected ? UIBuilder.TextColor : UIBuilder.MutedText;
    }

    private void SetEmptyText(bool visible, string text)
    {
        if (view.logEmptyText == null)
            return;

        view.logEmptyText.text = text;
        view.logEmptyText.gameObject.SetActive(visible);
        view.logEmptyText.transform.SetAsFirstSibling();
    }

    private void SetHint(string text)
    {
        if (view.logHint != null)
            view.logHint.text = text;
    }

    private void AddLogItem(LogEntryData entry)
    {
        if (view.logItemTemplate == null)
            return;

        LogItemView item = UIPrefabs.Clone(view.logItemTemplate, view.logContent);
        if (item.title != null) item.title.text = entry.title;
        if (item.body != null) item.body.text = entry.body;
        if (item.newBadge != null) item.newBadge.SetActive(GameLog.IsNew(entry));
        SetLogItemOpen(item, false);

        if (item.headerButton == null)
            return;

        item.headerButton.onClick.AddListener(() =>
        {
            bool open = item.info != null && !item.info.activeSelf;

            // เปิดได้ทีละหัวข้อ: ปิดอันอื่นก่อน
            if (open)
            {
                foreach (LogItemView other in view.logContent.GetComponentsInChildren<LogItemView>())
                    SetLogItemOpen(other, false);
            }

            SetLogItemOpen(item, open);

            if (open)
            {
                GameLog.MarkRead(entry);
                if (item.newBadge != null) item.newBadge.SetActive(false);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.logContent);
        });
    }

    private void SetLogItemOpen(LogItemView item, bool open)
    {
        if (item.info != null) item.info.SetActive(open);
        if (item.header != null) item.header.color = open ? view.logHeaderOpenColor : view.logHeaderColor;
        if (item.arrow != null) item.arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, open ? 180f : 0f);
    }

    private void AddCommandItem(CommandRecipe recipe)
    {
        if (view.commandItemTemplate == null)
            return;

        CommandItemView item = UIPrefabs.Clone(view.commandItemTemplate, view.logContent);
        if (item.title != null) item.title.text = recipe.displayName;

        if (item.amount != null)
        {
            string amount = string.Empty;
            if (recipe.kind != CommandKind.OpenSaveMenu && PersistentInventory.Instance != null)
            {
                int have = PersistentInventory.Instance.GetAmount(recipe.itemId);
                amount = recipe.kind == CommandKind.Weapon ? (have > 0 ? "owned" : string.Empty) : "x" + have;
            }

            item.amount.text = amount;
        }

        if (item.arrowRow != null && recipe.sequence != null)
        {
            UIPrefabs.ClearClones(item.arrowRow, item.arrowTemplate);
            for (int i = 0; i < recipe.sequence.Length; i++)
                CommandLineUI.AddArrow(item.arrowTemplate, item.arrowRow, recipe.sequence[i]);
        }
    }

    private void HandleCommandLearned(CommandRecipe recipe)
    {
        if (isOpen && currentPage == Page.Logs && currentLogTab == LogTab.Command)
            Rebuild();
    }

    private void HandleLogUnlocked(LogEntryData entry)
    {
        if (isOpen && currentPage == Page.Logs)
            Rebuild();
    }

    // ------------------------------------------------------------ Setting

    private void RefreshSetting()
    {
        if (view.fullscreenMark != null)
            view.fullscreenMark.text = Screen.fullScreen ? "X" : string.Empty;

        if (view.qualityLabel != null)
            view.qualityLabel.text = GraphicsQuality.CurrentName;

        AudioManager audio = AudioManager.Instance;
        bool hasAudio = audio != null;

        if (view.mainSlider != null)
            view.mainSlider.SetValueWithoutNotify(hasAudio ? audio.MasterVolume : AudioListener.volume);
        if (view.musicSlider != null)
        {
            view.musicSlider.gameObject.SetActive(hasAudio);
            if (hasAudio) view.musicSlider.SetValueWithoutNotify(audio.MusicVolume);
        }
        if (view.sfxSlider != null)
        {
            view.sfxSlider.gameObject.SetActive(hasAudio);
            if (hasAudio) view.sfxSlider.SetValueWithoutNotify(audio.SfxVolume);
        }

        // เริ่ม Play จากด่านโดยตรง (ไม่มี AudioManager จาก MainMenu): คุมได้แค่เสียงรวม
        if (view.audioNote != null)
            view.audioNote.SetActive(!hasAudio);
    }

    // ------------------------------------------------------------ Exit

    private void ExitToMainMenu()
    {
        Close(); // คืน timeScale ก่อนเปลี่ยน Scene

        if (LevelManager.Instance != null)
            LevelManager.Instance.LoadMainMenu();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.MainMenu);
    }
}
