using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// การทำงานของหน้า X-Gear Installer (เปิดจาก XGearInstaller ใน Safe Room)
/// หน้าตาอยู่ใน Prefab Resources/UI/XGearInstaller (XGearInstallerView) ปรับใน Editor ได้
///   กล้อง: ถ้า Installer มีกล้องของตัวเอง (View Camera ที่วางในฉาก) ใช้ตัวนั้น ไม่งั้นใช้กล้องโชว์ตัวผู้เล่นอัตโนมัติ
///   การ์ด Active / Passive / Maps: กดหัวการ์ด (ชื่อ ▽) = กางรายการ Data ในคลังให้เลือก / Remove = ถอดคืนคลัง
/// ใช้ระบบเดียวกับ Upgrade Corner (XGearLoadout: ใส่ = หักจากคลัง 1 ชิ้น, ถอด = คืนคลัง)
/// </summary>
public class XGearInstallerUI : MonoBehaviour
{
    private static XGearInstallerUI instance;

    public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

    private static readonly ItemCategory[] Slots = { ItemCategory.ActiveChip, ItemCategory.PassiveChip, ItemCategory.MapData };
    private static readonly string[] SlotNames = { "Active", "Passive", "Maps" };

    private XGearInstallerView view;
    private XGearInstaller source;
    private TopDownPlayerController lockedPlayer;
    private ItemCategory? openSlot;
    private float savedScreenOffset;

    public static void Open(XGearInstaller from = null)
    {
        if (instance == null)
        {
            XGearInstallerView view = UIPrefabs.Spawn(XGearInstallerView.PrefabName, XGearInstallerView.BuildDefault);
            instance = view.gameObject.AddComponent<XGearInstallerUI>();
            instance.Bind(view);
        }

        instance.source = from;
        instance.Show();
    }

    private void Bind(XGearInstallerView target)
    {
        view = target;

        if (view.closeButton != null)
            view.closeButton.onClick.AddListener(Close);

        for (int i = 0; i < view.cards.Length && i < Slots.Length; i++)
        {
            InstallerCardView card = view.cards[i];
            if (card == null || card.headerButton == null)
                continue;

            ItemCategory slot = Slots[i];
            card.headerButton.onClick.AddListener(() =>
            {
                openSlot = openSlot == slot ? (ItemCategory?)null : slot;
                Refresh();
            });

            if (card.removeButton != null)
            {
                card.removeButton.onClick.AddListener(() =>
                {
                    XGearLoadout.Equip(slot, string.Empty);
                    SfxLibrary.PlayUI("chip_remove");
                    openSlot = null;
                    Refresh();
                });
            }
        }

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        XGearLoadout.OnChanged -= Refresh;
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
        openSlot = null;

        lockedPlayer = FindAnyObjectByType<TopDownPlayerController>();
        if (lockedPlayer != null)
            lockedPlayer.SetMovementLocked(this, true);

        if (source != null && source.viewCamera != null)
        {
            source.SetCameraActive(true);
        }
        else
        {
            // ไม่มีกล้องที่วางไว้: กล้องโชว์ตัวผู้เล่นกลางจอ (การ์ดอยู่ล่างจอ ไม่บังตัวละคร)
            savedScreenOffset = UpgradeShowcaseCamera.ScreenOffset;
            UpgradeShowcaseCamera.ScreenOffset = 0f;
            UpgradeShowcaseCamera.Show(lockedPlayer);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        XGearLoadout.OnChanged -= Refresh;
        XGearLoadout.OnChanged += Refresh;
        PersistentInventory.OnChanged -= Refresh;
        PersistentInventory.OnChanged += Refresh;

        SfxLibrary.PlayUI("menu_open");
        Refresh();
    }

    public void Close()
    {
        XGearLoadout.OnChanged -= Refresh;
        PersistentInventory.OnChanged -= Refresh;

        gameObject.SetActive(false);

        if (source != null && source.viewCamera != null)
        {
            source.SetCameraActive(false);
        }
        else
        {
            UpgradeShowcaseCamera.Hide();
            UpgradeShowcaseCamera.ScreenOffset = savedScreenOffset;
        }

        if (lockedPlayer != null)
            lockedPlayer.SetMovementLocked(this, false);
        lockedPlayer = null;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
        SfxLibrary.PlayUI("menu_close");
    }

    // ------------------------------------------------------------ เติมข้อมูลการ์ด

    private void Refresh()
    {
        if (!gameObject.activeSelf)
            return;

        for (int i = 0; i < view.cards.Length && i < Slots.Length; i++)
        {
            if (view.cards[i] != null)
                RefreshCard(view.cards[i], Slots[i], SlotNames[i]);
        }
    }

    private void RefreshCard(InstallerCardView card, ItemCategory slot, string slotName)
    {
        bool expanded = openSlot == slot;
        string equippedId = XGearLoadout.Get(slot);
        ItemDef equipped = string.IsNullOrEmpty(equippedId) ? null : ItemCatalog.Get(equippedId);

        if (card.header != null)
            card.header.color = expanded ? view.headerOpenColor : view.headerColor;
        if (card.headerText != null)
            card.headerText.text = equipped != null ? equipped.displayName : "Name";
        if (card.arrow != null)
            card.arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, expanded ? 180f : 0f);

        if (card.body != null)
        {
            card.body.text = $"<b>{slotName}</b>\n" +
                             (equipped != null
                                 ? $"{equipped.displayName}\n<size=18><color=#FFFFFFAA>{equipped.description}</color></size>"
                                 : "<color=#FF5A5A>No data installed</color>");
        }

        if (card.dropdown == null)
            return;

        card.dropdown.gameObject.SetActive(expanded);
        if (!expanded)
            return;

        UIPrefabs.ClearClones(card.dropdown, card.optionTemplate,
            card.removeButton, card.emptyText != null ? card.emptyText.transform : null);

        PersistentInventory inventory = PersistentInventory.Instance;
        List<ItemDef> options = new List<ItemDef>();
        IReadOnlyList<ItemDef> all = ItemCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].category == slot && inventory != null && inventory.Has(all[i].id))
                options.Add(all[i]);
        }

        if (card.emptyText != null)
            card.emptyText.SetActive(options.Count == 0);

        for (int i = 0; i < options.Count && card.optionTemplate != null; i++)
        {
            ItemDef def = options[i];
            UnityEngine.UI.Button option = UIPrefabs.Clone(card.optionTemplate, card.dropdown);
            TMPro.TextMeshProUGUI label = UIBuilder.LabelOf(option);
            if (label != null)
                label.text = $"{def.displayName}  x{inventory.GetAmount(def.id)}";

            option.onClick.AddListener(() =>
            {
                if (XGearLoadout.Equip(slot, def.id))
                {
                    SfxLibrary.PlayUI("chip_insert");
                    openSlot = null;
                    Refresh();
                }
                else
                {
                    SfxLibrary.PlayUI("ui_error");
                }
            });
        }

        if (card.removeButton != null)
        {
            card.removeButton.gameObject.SetActive(equipped != null);
            card.removeButton.transform.SetAsLastSibling();
        }
    }
}
