using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// หน้าต่าง Upgrade Corner (สร้างด้วยโค้ด) 4 แท็บ:
///   Weapon : อัปเกรดอาวุธด้วย Scrap (แสดงต้นทุน, ผลที่ได้, ปุ่ม Upgrade)
///   Loadout: ช่อง Primary / Secondary (กดแล้วมี Panel ให้เลือกอาวุธที่ใส่ได้ตอนนี้) และ X-Gear (กดแล้วไปแท็บ X Gear)
///   X Gear : 3 ช่อง Passive / Active / Map กดวงกลมแล้วมี Panel เล็กด้านขวาให้เลือก Data ใส่
///   Craft  : คราฟต์กระสุนจาก Scrap / Metal
/// ใช้ของจาก PersistentInventory (ของที่เก็บจากด่านและจบด่านสำเร็จแล้ว)
/// </summary>
public class UpgradeUI : MonoBehaviour
{
    private enum Tab { Weapon, Loadout, XGear, Craft }

    private static UpgradeUI instance;

    public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

    private static readonly Color Red = new Color(1f, 0.35f, 0.35f, 1f);

    private Tab currentTab = Tab.Weapon;
    private readonly Button[] tabButtons = new Button[4];
    private RectTransform content;
    private RectTransform picker;
    private TextMeshProUGUI resourceLabel;
    private TopDownPlayerController lockedPlayer;
    private Transform showcaseTarget;

    /// <param name="showcaseTarget">โมเดลของคนอัปเกรด (กล้องจะหันไปหา) ถ้าเว้นว่าง กล้องจะโชว์ตัวผู้เล่นแทน</param>
    public static void Open(Transform showcaseTarget = null)
    {
        if (instance == null)
        {
            Canvas canvas = UIBuilder.CreateCanvas("UpgradeUI (Auto)", 200);
            instance = canvas.gameObject.AddComponent<UpgradeUI>();
            instance.Build();
        }

        instance.showcaseTarget = showcaseTarget;
        instance.Show();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        UpgradeShowcaseCamera.Hide();

        PersistentInventory.OnChanged -= Refresh;
    }

    private void Update()
    {
        bool cancel = PlayerControls.WasPressed(PlayerControls.Pause, Key.Escape) ||
                      (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        if (cancel)
            Close();
    }

    private void Show()
    {
        gameObject.SetActive(true);

        lockedPlayer = FindAnyObjectByType<TopDownPlayerController>();
        if (lockedPlayer != null)
            lockedPlayer.SetMovementLocked(this, true);

        // กล้องเลื่อนไปหาคนอัปเกรด (หรือตัวผู้เล่นถ้าไม่มี) ให้อยู่ครึ่งขวาของจอ
        UpgradeShowcaseCamera.Show(lockedPlayer, showcaseTarget);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        PersistentInventory.OnChanged -= Refresh;
        PersistentInventory.OnChanged += Refresh;

        SfxLibrary.PlayUI("menu_open");
        SelectTab(currentTab);
    }

    public void Close()
    {
        PersistentInventory.OnChanged -= Refresh;
        gameObject.SetActive(false);
        UpgradeShowcaseCamera.Hide();

        if (lockedPlayer != null)
            lockedPlayer.SetMovementLocked(this, false);

        lockedPlayer = null;

        // กลับสู่โหมดเมาส์ของตอนเล่น (เหมือน TopDownPlayerController.Start)
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        SfxLibrary.PlayUI("menu_close");
    }

    // ------------------------------------------------------------ โครงหน้าต่าง

    private void Build()
    {
        // หน้าต่างกินครึ่งซ้ายของจอ ครึ่งขวาเว้นว่างให้เห็นตัวละครจริงในฉาก (UpgradeShowcaseCamera จัดมุมกล้องให้)
        // ไม่มีฉากมืดทับทั้งจอ และไม่มี Panel รูปตัวละครแล้ว
        Image window = UIBuilder.Panel("Window", transform, UIBuilder.WindowColor);
        UIBuilder.Place(window.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(1060f, 940f));

        // แถบแท็บเว้นที่มุมขวาบนไว้ให้ปุ่ม X
        RectTransform tabs = UIBuilder.Rect("Tabs", window.transform);
        tabs.anchorMin = new Vector2(0f, 1f);
        tabs.anchorMax = new Vector2(1f, 1f);
        tabs.pivot = new Vector2(0.5f, 1f);
        tabs.anchoredPosition = new Vector2(-40f, 0f);
        tabs.sizeDelta = new Vector2(-80f, 76f);
        HorizontalLayoutGroup tabRow = UIBuilder.Horizontal(tabs, 4f, 0);
        tabRow.childForceExpandWidth = true;
        tabRow.childForceExpandHeight = true;

        string[] names = { "Weapon", "Loadout", "X Gear", "Craft" };
        for (int i = 0; i < names.Length; i++)
        {
            Tab tab = (Tab)i;
            tabButtons[i] = UIBuilder.Button(names[i] + "Tab", tabs, names[i], () => SelectTab(tab), 30f);
        }

        content = UIBuilder.Rect("Content", window.transform);
        UIBuilder.Stretch(content, 28f, 70f, 28f, 96f);
        UIBuilder.Vertical(content, 14f);

        resourceLabel = UIBuilder.Text("Resources", window.transform, string.Empty, 26f, TextAlignmentOptions.BottomRight);
        resourceLabel.richText = true;
        UIBuilder.Stretch(resourceLabel.rectTransform, 28f, 18f, 28f, 880f);

        Button close = UIBuilder.Button("Close", window.transform, "X", Close, 34f, new Color(0.8f, 0.15f, 0.15f, 0.9f));
        UIBuilder.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(60f, 60f));

        // Panel เล็กสำหรับเลือก Data ใส่ X-Gear (ซ้อนอยู่ด้านขวาของหน้าต่าง)
        Image pickerImage = UIBuilder.Panel("DataPicker", window.transform, new Color(0.12f, 0.14f, 0.18f, 1f));
        picker = pickerImage.rectTransform;
        UIBuilder.Place(picker, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, -10f), new Vector2(360f, 420f));
        UIBuilder.Vertical(picker, 8f, 14);
        picker.gameObject.SetActive(false);
    }

    private void SelectTab(Tab tab)
    {
        currentTab = tab;

        for (int i = 0; i < tabButtons.Length; i++)
            tabButtons[i].targetGraphic.color = i == (int)tab ? UIBuilder.AccentColor : UIBuilder.ButtonColor;

        Refresh();
    }

    private void Refresh()
    {
        if (!gameObject.activeSelf)
            return;

        UIBuilder.Clear(content);

        switch (currentTab)
        {
            case Tab.Weapon: BuildWeaponTab(); break;
            case Tab.Loadout: BuildLoadoutTab(); break;
            case Tab.XGear: BuildXGearTab(); break;
            case Tab.Craft: BuildCraftTab(); break;
        }

        RefreshPicker();

        PersistentInventory inventory = PersistentInventory.Instance;
        int scrap = inventory != null ? inventory.GetAmount(ItemCatalog.Scrap) : 0;
        int metal = inventory != null ? inventory.GetAmount(ItemCatalog.Metal) : 0;
        resourceLabel.text = $"scrap <color=#FF5A5A>{scrap}</color> / metal <color=#FF5A5A>{metal}</color>";
    }

    // แถวมาตรฐาน: [วงกลมไอคอน + ป้ายใต้วงกลม] [ข้อความ] [ปุ่มขวา]
    private Button AddRow(string iconText, string underLabel, Color underColor, string body, Color bodyColor,
        string buttonLabel, System.Action onButton, bool buttonEnabled, System.Action onIconClick = null, Sprite icon = null)
    {
        Image row = UIBuilder.Panel("Row", content, UIBuilder.PanelColor, false);
        UIBuilder.Size(row, -1f, 150f);
        UIBuilder.Horizontal(row.rectTransform, 22f, 16);

        // วงกลม + ป้ายใต้วงกลม
        RectTransform iconColumn = UIBuilder.Rect("IconColumn", row.transform);
        UIBuilder.Size(iconColumn, 130f, 118f);

        Image circle = UIBuilder.Panel("Circle", iconColumn, new Color(1f, 1f, 1f, 0.16f), onIconClick != null);
        circle.sprite = UIBuilder.CircleSprite;
        UIBuilder.Place(circle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(84f, 84f));

        if (icon != null)
        {
            Image iconImage = UIBuilder.Panel("Icon", circle.transform, Color.white, false);
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            UIBuilder.Stretch(iconImage.rectTransform, 14f, 14f, 14f, 14f);
        }
        else
        {
            TextMeshProUGUI iconLabel = UIBuilder.Text("IconText", circle.transform, iconText, 20f, TextAlignmentOptions.Center);
            UIBuilder.Stretch(iconLabel.rectTransform);
        }

        if (onIconClick != null)
        {
            Button iconButton = circle.gameObject.AddComponent<Button>();
            iconButton.targetGraphic = circle;
            iconButton.onClick.AddListener(() => onIconClick());
            circle.gameObject.AddComponent<UIPointerRelay>().playSounds = true;
        }

        TextMeshProUGUI under = UIBuilder.Text("Under", iconColumn, underLabel, 18f, TextAlignmentOptions.Bottom, underColor);
        UIBuilder.Stretch(under.rectTransform, 0f, 0f, 0f, 88f);

        TextMeshProUGUI bodyText = UIBuilder.Text("Body", row.transform, body, 24f, TextAlignmentOptions.MidlineLeft, bodyColor);
        UIBuilder.Size(bodyText, -1f, 118f, 1f);

        if (string.IsNullOrEmpty(buttonLabel))
            return null;

        Button button = UIBuilder.Button("Action", row.transform, buttonLabel, onButton, 24f);
        button.interactable = buttonEnabled;
        UIBuilder.Size(button, 170f, 64f);
        return button;
    }

    // ------------------------------------------------------------ Weapon

    private void BuildWeaponTab()
    {
        PersistentInventory inventory = PersistentInventory.Instance;

        for (int i = 0; i < WeaponUpgrades.WeaponKeys.Length; i++)
        {
            string key = WeaponUpgrades.WeaponKeys[i];
            int level = WeaponUpgrades.GetLevel(key);
            bool maxed = level >= WeaponUpgrades.MaxLevel;
            int cost = WeaponUpgrades.NextCost(key);
            bool affordable = inventory != null && inventory.Has(ItemCatalog.Scrap, cost);

            string current = $"Now: Damage +{level * WeaponUpgrades.DamagePerLevel * 100f:0}%, Reload speed +{level * WeaponUpgrades.ReloadSpeedPerLevel * 100f:0}%";
            string body = maxed
                ? $"<b>{key}</b>   MAX LEVEL\n{current}"
                : $"<b>{key}</b>   x{cost} scrap\n+{WeaponUpgrades.ReloadSpeedPerLevel * 100f:0}% Reload speed, +{WeaponUpgrades.DamagePerLevel * 100f:0}% Damage\n<size=18><color=#FFFFFF88>{current}</color></size>";

            AddRow(key, "LV." + (level + 1), Red, body, UIBuilder.TextColor,
                maxed ? "MAX" : "upgrade",
                () =>
                {
                    if (WeaponUpgrades.TryUpgrade(key))
                    {
                        SfxLibrary.PlayUI("upgrade_success");
                        HudToast.Show(key + " upgraded to LV." + (WeaponUpgrades.GetLevel(key) + 1));
                        Refresh();
                    }
                    else
                    {
                        SfxLibrary.PlayUI("ui_error");
                    }
                },
                !maxed && affordable);
        }
    }

    // ------------------------------------------------------------ Loadout

    // อาวุธไม่ได้ใส่ช่องแล้ว (เรียกด้วย CommandLine) แท็บนี้จึงสรุป "อาวุธที่เป็นเจ้าของ + สูตร" และ X-Gear
    private void BuildLoadoutTab()
    {
        PersistentInventory inventory = PersistentInventory.Instance;
        IReadOnlyList<CommandRecipe> recipes = CommandBook.All;

        for (int i = 0; i < recipes.Count; i++)
        {
            CommandRecipe recipe = recipes[i];
            if (recipe == null || recipe.kind != CommandKind.Weapon)
                continue;

            bool owned = inventory != null && inventory.Has(recipe.itemId);
            string body = owned
                ? $"<b>{recipe.displayName}</b>\n<size=20>Command: {CommandText(recipe)}\n<color=#FFFFFF88>{recipe.description}</color></size>"
                : $"<b>{recipe.displayName}</b>\n<size=20><color=#FFFFFF88>Not found yet</color></size>";

            AddRow(recipe.displayName, owned ? "Owned" : "Locked", owned ? UIBuilder.AccentColor : UIBuilder.MutedText,
                body, owned ? UIBuilder.TextColor : UIBuilder.MutedText, null, null, false);
        }

        // X-Gear: สรุปจำนวนชิปในคลัง กดแล้วไปแท็บ X Gear
        string gear = $"<b>X-Gear</b>\n<size=20>Passive chips: {CountChips(ItemCategory.PassiveChip)}\n" +
                      $"Active chips: {CountChips(ItemCategory.ActiveChip)}\nMap chips: {CountChips(ItemCategory.MapData)}</size>";

        AddRow("X-GEAR", "X-Gear", UIBuilder.AccentColor, gear, UIBuilder.TextColor,
            "open", () => SelectTab(Tab.XGear), true, () => SelectTab(Tab.XGear));
    }

    private static int CountChips(ItemCategory category)
    {
        PersistentInventory inventory = PersistentInventory.Instance;
        if (inventory == null)
            return 0;

        int total = 0;
        foreach (ItemDef def in ItemCatalog.All)
            if (def.category == category) total += inventory.GetAmount(def.id);
        return total;
    }

    /// <summary>สูตรเป็นตัวอักษร (W A S D) ไว้แสดงในข้อความ</summary>
    private static string CommandText(CommandRecipe recipe)
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

    // ------------------------------------------------------------ X Gear

    private static readonly ItemCategory[] GearSlots = { ItemCategory.PassiveChip, ItemCategory.ActiveChip, ItemCategory.MapData };
    private static readonly string[] GearSlotNames = { "Passive", "Active", "Map" };
    private static readonly string[] GearEmptyTexts = { "No Passive skill Data", "No Active skill Data", "No map data" };

    private void BuildXGearTab()
    {
        // ชิปอยู่ในคลังแล้ว (ไม่ต้องติดตั้งทีละช่อง): แท็บนี้แค่แสดงชิปที่มี
        //   Passive ได้ผลอัตโนมัติ (ซ้อนตามจำนวน) / Active เรียกด้วย Command / Map ขึ้นเองเมื่อเข้าโซน
        PersistentInventory inventory = PersistentInventory.Instance;

        for (int i = 0; i < GearSlots.Length; i++)
        {
            ItemCategory category = GearSlots[i];
            int shown = 0;

            foreach (ItemDef def in ItemCatalog.All)
            {
                int amount = inventory != null && def.category == category ? inventory.GetAmount(def.id) : 0;
                if (amount <= 0)
                    continue;

                shown++;
                string body = $"<b>{def.displayName}</b>  x{amount}\n<size=20>{def.description}</size>";
                AddRow(GearSlotNames[i], GearSlotNames[i], UIBuilder.MutedText, body, UIBuilder.TextColor,
                    null, null, false, null, ItemCatalog.IconOf(def.id));
            }

            if (shown == 0)
                AddRow(GearSlotNames[i], GearSlotNames[i], UIBuilder.MutedText, GearEmptyTexts[i], Red, null, null, false);
        }
    }

    // Panel เลือก Data แบบเก่า (ติดตั้งชิป) ไม่ใช้แล้ว: ซ่อนไว้เสมอ
    private void RefreshPicker()
    {
        if (picker != null)
            picker.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------ Craft

    private void BuildCraftTab()
    {
        PersistentInventory inventory = PersistentInventory.Instance;

        for (int i = 0; i < AmmoRecipe.All.Length; i++)
        {
            AmmoRecipe recipe = AmmoRecipe.All[i];
            ItemDef def = ItemCatalog.Get(recipe.ammoId);

            bool affordable = inventory != null &&
                              inventory.Has(ItemCatalog.Scrap, recipe.scrap) &&
                              inventory.Has(ItemCatalog.Metal, recipe.metal);

            string cost = (recipe.metal > 0 ? recipe.metal + "x metal  " : string.Empty) +
                          (recipe.scrap > 0 ? recipe.scrap + "x scrap" : string.Empty);
            int owned = inventory != null ? inventory.GetAmount(recipe.ammoId) : 0;

            string shortName = def.displayName.Replace(" Ammo", string.Empty);

            AddRow(shortName, shortName + " Bullet", UIBuilder.TextColor,
                $"{cost}\n<size=20>Get x{recipe.yield}   <color=#FFFFFF88>(have {owned})</color></size>", UIBuilder.TextColor,
                "craft",
                () =>
                {
                    if (recipe.TryCraft())
                    {
                        SfxLibrary.PlayUI("craft_success");
                        HudToast.Show("+ " + def.displayName + " x" + recipe.yield);
                    }
                    else
                    {
                        SfxLibrary.PlayUI("ui_error");
                    }
                },
                affordable, null, ItemCatalog.IconOf(recipe.ammoId));
        }
    }
}
