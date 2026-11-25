using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// เมนูตอนกด Esc ในเกม (ทุก Scene ที่มีผู้เล่น): เนื้อหาซ้าย / เมนูขวา
///   Inventory : ซ้าย = รูปของที่ชี้ + คำอธิบาย / ขวา = ตาราง Inventory (Keycard, กุญแจ, อาวุธ, กระสุน, วัตถุดิบ)
///               และตาราง Consumable (Medkit, X-Gear Cell) คลิกขวา = Use / Drop
///   X-Gear    : ซ้าย = รูป X-Gear (คงที่) + คำอธิบาย / ขวา = ชิป Active, Passive, Map ที่มี
///   Logs      : แท็บ Log = Memo กดหัวข้อเพื่อเปิดอ่าน (GameLog / Resources/Logs)
///               แท็บ Command = สูตรลูกศรของไอเทมที่เคยเก็บแล้ว (CommandBook)
///   Setting   : Fullscreen + เสียง Main / Music / SFX
///   Exit      : ถามยืนยัน Yes / No
///
/// หน้าตาอยู่ใน Prefab Resources/UI/PauseMenu (PauseMenuView) ซึ่งวางซ้อนอยู่ใน Prefab MainHUD ด้วย
///   แก้ที่ MainHUD > PauseMenu ได้เลย / Scene ที่ไม่มี MainHUD จะสร้างจาก Resources/UI/PauseMenu ให้เอง
/// สคริปต์นี้เติมข้อมูล ผูกปุ่ม และ Clone Template ของรายการที่ซ้ำ
/// เปิดเมนู = หยุดเวลา (Time.timeScale = 0) และล็อกผู้เล่นด้วยระบบล็อกแบบมีเจ้าของ
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    private enum Page { Inventory, XGear, Logs, Setting, Exit }
    private enum LogTab { Log, Command }

    private TopDownPlayerController player;
    private PickupSystem pickup;

    private PauseMenuView view;
    private bool ownsView; // สร้างหน้าต่างเอง (Scene ไม่มี MainHUD) = ต้องลบเองตอนจบ
    private string contextItemId; // ของที่คลิกขวาอยู่ (เมนู Use / Drop)
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
        // หน้าต่างที่วางไว้ใน MainHUD เป็นของ MainHUD ไม่ลบ
        if (view != null && ownsView)
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

        // ใช้หน้าต่างที่วางไว้ใน MainHUD ก่อน ไม่มีค่อยสร้างจาก Resources/UI/PauseMenu
        view = FindAnyObjectByType<PauseMenuView>(FindObjectsInactive.Include);
        ownsView = view == null;
        if (view == null)
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

        BindVolume(view.mainSlider, value =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(value);
            else AudioListener.volume = value;
        });
        BindVolume(view.musicSlider, value => AudioManager.Instance?.SetMusicVolume(value));
        BindVolume(view.sfxSlider, value => AudioManager.Instance?.SetSfxVolume(value));

        if (view.exitYes != null) view.exitYes.onClick.AddListener(ExitToMainMenu);
        if (view.exitNo != null) view.exitNo.onClick.AddListener(() => SelectPage(Page.Inventory));

        // เมนูคลิกขวา: คลิกพื้นที่ว่าง = ปิด / Use / Drop
        if (view.contextMenu != null)
        {
            Button blocker = view.contextMenu.GetComponent<Button>();
            if (blocker != null) blocker.onClick.AddListener(HideContextMenu);
            view.contextMenu.SetActive(false);
        }

        if (view.useButton != null) view.useButton.onClick.AddListener(UseContextItem);
        if (view.dropButton != null) view.dropButton.onClick.AddListener(DropContextItem);

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
        HideContextMenu();

        switch (currentPage)
        {
            case Page.Inventory: RefreshInventory(); break;
            case Page.XGear: RefreshXGear(); break;
            case Page.Logs: RefreshLogs(); break;
            case Page.Setting: RefreshSetting(); break;
        }
    }

    // ------------------------------------------------------------ ช่อง Sprite + Description (แทนกล่องลอยแบบเดิม)

    private void ShowInventoryDetail(string title, string body, Sprite icon, string fallbackLabel, Color labelColor)
    {
        if (view.inventoryDescription != null)
            view.inventoryDescription.text = $"<b>{title}</b>\n<size=17>{body}</size>";

        if (view.inventoryPreview != null)
        {
            view.inventoryPreview.sprite = icon;
            view.inventoryPreview.enabled = icon != null;
        }

        if (view.inventoryPreviewLabel != null)
        {
            view.inventoryPreviewLabel.text = icon == null ? fallbackLabel : string.Empty;
            view.inventoryPreviewLabel.color = labelColor;
        }
    }

    private void ClearInventoryDetail()
    {
        if (view.inventoryDescription != null)
            view.inventoryDescription.text = $"<color=#FFFFFF88>{view.inventoryHint}</color>";

        if (view.inventoryPreview != null)
            view.inventoryPreview.enabled = false;

        if (view.inventoryPreviewLabel != null)
            view.inventoryPreviewLabel.text = string.Empty;
    }

    private void ShowXGearDetail(string title, string body)
    {
        if (view.xgearDescription != null)
            view.xgearDescription.text = $"<b>{title}</b>\n<size=17>{body}</size>";
    }

    private void ClearXGearDetail()
    {
        if (view.xgearDescription != null)
            view.xgearDescription.text = $"<color=#FFFFFF88>{view.xgearHint}</color>";
    }

    // ------------------------------------------------------------ Inventory

    private void RefreshInventory()
    {
        if (view.cellTemplate == null)
            return;

        ClearInventoryDetail();

        // ---- ตาราง Inventory: Keycard, กุญแจ, อาวุธ, กระสุน, วัตถุดิบ
        if (view.inventoryGrid != null)
        {
            ClearGrid(view.inventoryGrid);
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
                    Color color = new Color(1f, 0.85f, 0.3f);
                    string body = $"Opens doors that need Keycard Lv.{level} or lower.\nNot used up when opening a door.\n\nKept for this mission only.";
                    AddCell(view.inventoryGrid, "Lv." + level, "KEY", color, null,
                        () => ShowInventoryDetail("Keycard Lv." + level, body, null, "KEY", color), null);
                    used++;
                }

                // กุญแจเฉพาะประตู (DoorKeyData)
                IReadOnlyList<DoorKeyData> doorKeys = keycards.OwnedKeys;
                for (int i = 0; i < doorKeys.Count && used < max; i++)
                {
                    DoorKeyData key = doorKeys[i];
                    if (key == null)
                        continue;

                    AddCell(view.inventoryGrid, string.Empty, "KEY", key.color, key.icon,
                        () => ShowInventoryDetail(key.displayName, key.description + "\n\nKept for this mission only.", key.icon, "KEY", key.color), null);
                    used++;
                }
            }

            foreach (ItemDef def in OwnedItems())
            {
                if (used >= max)
                    break;

                if (IsChip(def.category) || def.category == ItemCategory.Consumable)
                    continue;

                AddItemCell(view.inventoryGrid, def, false);
                used++;
            }

            FillEmpty(view.inventoryGrid, used, max);
        }

        // ---- ตาราง Consumable: Medkit, X-Gear Cell (คลิกขวา = Use / Drop)
        if (view.consumableGrid != null)
        {
            ClearGrid(view.consumableGrid);
            int max = Mathf.Max(1, view.consumableCellCount);
            int used = 0;

            foreach (ItemDef def in OwnedItems())
            {
                if (used >= max)
                    break;

                if (def.category != ItemCategory.Consumable)
                    continue;

                AddItemCell(view.consumableGrid, def, true);
                used++;
            }

            FillEmpty(view.consumableGrid, used, max);
        }
    }

    // ไอเทมที่มีอยู่ (คลังถาวร + ของที่เก็บในรอบนี้) ตามลำดับใน ItemCatalog
    private static IEnumerable<ItemDef> OwnedItems()
    {
        PersistentInventory persistent = PersistentInventory.Instance;
        RunInventory run = RunInventory.Instance;

        // ให้ id ที่ไม่อยู่ใน Catalog (ResourceData ใหม่ / Map Chip ของโซน) ถูกลงทะเบียนก่อนวน
        if (persistent != null)
            foreach (KeyValuePair<string, int> pair in persistent.Snapshot()) ItemCatalog.Get(pair.Key);
        if (run != null)
            foreach (KeyValuePair<string, int> pair in run.Snapshot()) ItemCatalog.Get(pair.Key);

        IReadOnlyList<ItemDef> all = ItemCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            int owned = persistent != null ? persistent.GetAmount(all[i].id) : 0;
            int pending = run != null ? run.GetAmount(all[i].id) : 0;
            if (owned + pending > 0)
                yield return all[i];
        }
    }

    private static bool IsChip(ItemCategory category) =>
        category == ItemCategory.PassiveChip || category == ItemCategory.ActiveChip || category == ItemCategory.MapData;

    private void AddItemCell(RectTransform grid, ItemDef def, bool consumable)
    {
        PersistentInventory persistent = PersistentInventory.Instance;
        RunInventory run = RunInventory.Instance;
        int owned = persistent != null ? persistent.GetAmount(def.id) : 0;
        int pending = run != null ? run.GetAmount(def.id) : 0;

        string amount = pending > 0 ? $"{owned}<color=#7CFF8A>+{pending}</color>" : owned.ToString();
        string body = def.description + $"\n\nOwned: {owned}" +
                      (pending > 0 ? $"\nThis run: +{pending} (kept when you finish the mission)" : string.Empty) +
                      (consumable ? "\n\n<color=#FFFFFF88>Right-click: Use / Drop</color>" : string.Empty);
        Sprite icon = ItemCatalog.IconOf(def.id);
        string label = ShortLabel(def);

        AddCell(grid, amount, label, def.color, icon,
            () => ShowInventoryDetail(def.displayName, body, icon, label, def.color),
            consumable ? () => ShowContextMenu(def.id) : (System.Action)null);
    }

    private static string ShortLabel(ItemDef def)
    {
        switch (def.category)
        {
            case ItemCategory.Ammo: return def.displayName.Replace(" Ammo", string.Empty);
            case ItemCategory.PassiveChip: return def.displayName.Replace("Passive Chip : ", string.Empty);
            case ItemCategory.ActiveChip: return def.displayName.Replace("Active Chip : ", string.Empty);
            case ItemCategory.MapData: return def.displayName.Replace("Map Data : ", string.Empty);
            default: return def.displayName;
        }
    }

    private void AddCell(RectTransform grid, string amount, string label, Color color, Sprite icon, System.Action onEnter, System.Action onRightClick)
    {
        InventoryCellView cell = UIPrefabs.Clone(view.cellTemplate, grid);

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
        relay.onEnter = onEnter;
        relay.onRightClick = onRightClick;
    }

    // ลบช่องที่ Clone ไว้ในตาราง (เก็บ Template ที่ปิดไว้)
    private void ClearGrid(RectTransform grid)
    {
        UIPrefabs.ClearClones(grid, view.cellTemplate, view.emptyCellTemplate != null ? view.emptyCellTemplate.transform : null);
    }

    // ช่องว่างที่เหลือ ให้เห็นเป็นตาราง
    private void FillEmpty(RectTransform grid, int used, int max)
    {
        if (view.emptyCellTemplate == null)
            return;

        for (; used < max; used++)
        {
            GameObject empty = Instantiate(view.emptyCellTemplate, grid, false);
            empty.SetActive(true);
        }
    }

    // ------------------------------------------------------------ คลิกขวา: Use / Drop (ของใช้)

    private void ShowContextMenu(string itemId)
    {
        if (view.contextMenu == null || view.contextPanel == null)
            return;

        contextItemId = itemId;
        view.contextMenu.SetActive(true);
        view.contextMenu.transform.SetAsLastSibling();

        // วางเมนูที่ตำแหน่งเมาส์ (แปลงพิกัดจอเป็นพิกัดใน Canvas)
        RectTransform area = (RectTransform)view.contextMenu.transform;
        Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
        Canvas canvas = area.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area, mouse, cam, out Vector2 local))
            view.contextPanel.anchoredPosition = local - area.rect.min;

        SfxLibrary.PlayUI("ui_click");
    }

    private void HideContextMenu()
    {
        contextItemId = null;
        if (view != null && view.contextMenu != null)
            view.contextMenu.SetActive(false);
    }

    private void UseContextItem()
    {
        string itemId = contextItemId;
        HideContextMenu();

        if (string.IsNullOrEmpty(itemId) || pickup == null)
            return;

        ItemDef def = ItemCatalog.Get(itemId);
        if (pickup.UseConsumable(itemId, out string reason))
        {
            ShowInventoryDetail(def.displayName, "Used.", ItemCatalog.IconOf(itemId), ShortLabel(def), def.color);
            RefreshInventory();
        }
        else
        {
            SfxLibrary.PlayUI("ui_error");
            ShowInventoryDetail(def.displayName, $"<color=#FF7777>{reason}</color>", ItemCatalog.IconOf(itemId), ShortLabel(def), def.color);
        }
    }

    private void DropContextItem()
    {
        string itemId = contextItemId;
        HideContextMenu();

        PersistentInventory inventory = PersistentInventory.Instance;
        if (string.IsNullOrEmpty(itemId) || inventory == null || player == null || !inventory.TrySpend(itemId, 1))
            return;

        SpawnDrop(itemId, player.transform);
        SfxLibrary.PlayUI("ui_click");
        RefreshInventory();
    }

    // วางของ 1 ชิ้นไว้หน้าผู้เล่น: ใช้ Drop Prefab ใน ItemData ของไอเทม ถ้าไม่มีสร้างกล่องเก็บได้ทั่วไปให้
    private static void SpawnDrop(string itemId, Transform owner)
    {
        CharacterController body = owner.GetComponent<CharacterController>();
        float feetY = body != null ? body.bounds.min.y : owner.position.y;
        Vector3 position = owner.position + owner.forward * 0.9f;
        position.y = feetY + 0.2f;

        GameObject prefab = null;
        foreach (ItemData data in Resources.LoadAll<ItemData>(CommandBook.ItemsFolder))
        {
            if (data != null && data.hasCommand && data.command != null && data.command.itemId == itemId)
            {
                prefab = data.dropPrefab;
                break;
            }
        }

        GameObject drop;
        if (prefab != null)
        {
            drop = Instantiate(prefab, position, Quaternion.identity);
        }
        else
        {
            drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            drop.name = "Drop_" + itemId;
            drop.transform.SetPositionAndRotation(position, Quaternion.identity);
            drop.transform.localScale = Vector3.one * 0.3f;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                Material material = new Material(lit);
                material.SetColor("_BaseColor", ItemCatalog.Get(itemId).color);
                drop.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        // ให้ PickupDetector (กด E) เห็นของที่ทิ้ง: Layer "Items" + ตัวเก็บของตาม id
        int itemsLayer = LayerMask.NameToLayer("Items");
        if (itemsLayer >= 0)
            drop.layer = itemsLayer;

        InventoryPickup pickupComponent = drop.GetComponent<InventoryPickup>();
        if (pickupComponent == null)
            pickupComponent = drop.AddComponent<InventoryPickup>();
        pickupComponent.itemId = itemId;
        pickupComponent.amount = 1;
    }

    // ------------------------------------------------------------ X-Gear

    private void RefreshXGear()
    {
        if (view.cellTemplate == null)
            return;

        ClearXGearDetail();
        FillChipGrid(view.activeGrid, ItemCategory.ActiveChip);
        FillChipGrid(view.passiveGrid, ItemCategory.PassiveChip);
        FillChipGrid(view.mapGrid, ItemCategory.MapData);
    }

    private void FillChipGrid(RectTransform grid, ItemCategory category)
    {
        if (grid == null)
            return;

        ClearGrid(grid);
        int max = Mathf.Max(1, view.chipCellCount);
        int used = 0;

        PersistentInventory inventory = PersistentInventory.Instance;
        XGearSystem gear = player != null ? player.GetComponent<XGearSystem>() : null;

        foreach (ItemDef def in OwnedItems())
        {
            if (used >= max)
                break;

            if (def.category != category)
                continue;

            int count = inventory != null ? inventory.GetAmount(def.id) : 0;
            string body = def.description + $"\n\nOwned: {count}";

            if (category == ItemCategory.ActiveChip)
            {
                CommandRecipe recipe = FindChipRecipe(def.id);
                body += recipe != null ? "\nCommand: " + ArrowText(recipe) : string.Empty;
                if (gear != null && gear.LoadedChip == def.id)
                    body += "\n<color=#7CC8FF>In X-Gear now</color>";
            }
            else if (category == ItemCategory.MapData && MapArea.Current != null && MapArea.Current.mapChipId == def.id)
            {
                body += "\n<color=#7CC8FF>You are in this area</color>";
            }

            Sprite icon = ItemCatalog.IconOf(def.id);
            string label = ShortLabel(def);
            AddCell(grid, count > 1 ? "x" + count : string.Empty, label, def.color, icon,
                () => ShowXGearDetail(def.displayName, body), null);
            used++;
        }

        FillEmpty(grid, used, max);
    }

    private static CommandRecipe FindChipRecipe(string chipId)
    {
        IReadOnlyList<CommandRecipe> all = CommandBook.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && all[i].kind == CommandKind.XGearChip && all[i].itemId == chipId)
                return all[i];
        return null;
    }

    private static string ArrowText(CommandRecipe recipe)
    {
        if (recipe.sequence == null)
            return "-";

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < recipe.sequence.Length; i++)
        {
            if (i > 0) builder.Append(' ');
            switch (recipe.sequence[i])
            {
                case CommandArrow.Up: builder.Append('W'); break;
                case CommandArrow.Down: builder.Append('S'); break;
                case CommandArrow.Left: builder.Append('A'); break;
                default: builder.Append('D'); break;
            }
        }

        return builder.ToString();
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
