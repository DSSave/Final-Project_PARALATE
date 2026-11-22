using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// HUD อาวุธมุมขวาล่าง (แนวตั้ง):
///   - ปกติ: โชว์แถวเดียว = ช่องที่ถืออยู่ (ชื่อช่อง, ชื่อของ, กระสุน)
///   - กด Tab ค้าง: กางเป็น 4 แถว Unarmed / Knife / Command / X-Gear
///     เลื่อนลูกกลิ้งเพื่อเปลี่ยน (มีเสียง + แถวที่เลือกเปลี่ยนสีและยื่นออกมา) ปล่อย Tab เพื่อยืนยัน
///   - Command: ของที่เรียกด้วย CommandLine (Q) ของใช้มีตัวเลขจำนวนเล็กๆ มุมขวาล่าง / ปืนแสดงกระสุนในแม็ก
///
/// หน้าตาอยู่ใน Prefab Resources/UI/WeaponHUD (WeaponHudView) จัดตำแหน่ง/สี/ฟอนต์ใน Editor ได้
/// สคริปต์นี้คุมแค่การทำงาน (ช่อง Wheel Panel / Slot เดิมไม่ใช้แล้ว เก็บไว้เพื่อไม่ให้ค่าใน Scene เดิมหาย)
/// </summary>
public class WeaponWheelUI : MonoBehaviour
{
    [Header("References")]
    public PickupSystem pickupSystem;
    public PlayerInput playerInput;

    [Header("UI เดิม (ไม่ใช้แล้ว จะถูกซ่อน)")]
    public GameObject wheelPanel;
    public Transform slotContainer;
    public WeaponSlotUI slotPrefab;

    [Header("Behaviour")]
    [Tooltip("ชะลอเวลาตอนเปิดแถบ (1 = ปกติ, 0.2 = slow motion)")]
    [Range(0.05f, 1f)] public float timeScaleWhileOpen = 0.2f;
    [Tooltip("ค่า scroll ขั้นต่ำที่นับเป็น 1 คลิก")]
    public float scrollThreshold = 0.1f;

    private static readonly HeldSlot[] Order = { HeldSlot.Unarmed, HeldSlot.Knife, HeldSlot.Command, HeldSlot.XGear };

    private InputAction wheelAction;
    private InputAction scrollAction;
    private int selectedIndex;
    private bool isOpen;
    private float cachedTimeScale = 1f;

    private WeaponHudView view;
    private Vector2[] openPositions;

    // กระสุนของปืนที่ถืออยู่: จำค่าล่าสุดไว้ แล้วแก้ข้อความเฉพาะตอนค่าเปลี่ยน (ไม่สร้าง string ใหม่ทุกเฟรม)
    private GunAction heldGun;
    private int shownAmmo = -1;
    private int shownReserve = -1;
    private bool shownReloading;

    public bool IsOpen => isOpen;

    void Awake()
    {
        if (pickupSystem == null) pickupSystem = GetComponent<PickupSystem>();
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();

        if (playerInput != null)
        {
            wheelAction = playerInput.actions["WeaponWheel"];
            scrollAction = playerInput.actions["ScrollWeapon"];
        }

        if (wheelPanel != null)
            wheelPanel.SetActive(false);
    }

    void Start()
    {
        // กล่อง WeaponHUD เดิมของ MainHUD: ซ่อนถาวร HUD นี้แสดงแทนทั้งหมด
        AmmoUIManager oldHud = pickupSystem != null && pickupSystem.ammoUIManager != null
            ? pickupSystem.ammoUIManager
            : FindAnyObjectByType<AmmoUIManager>(FindObjectsInactive.Include);

        if (oldHud != null)
            oldHud.SetSuppressed(true);

        EnsureUI();
        RefreshAll();
    }

    void OnEnable()
    {
        if (pickupSystem != null)
        {
            pickupSystem.OnHeldSlotChanged += HandleHeldSlotChanged;
            pickupSystem.OnLoadoutChanged += RefreshAll;
        }

        XGearLoadout.OnChanged += RefreshAll;

        if (view != null)
        {
            view.gameObject.SetActive(true);
            RefreshAll();
        }
    }

    void OnDisable()
    {
        if (pickupSystem != null)
        {
            pickupSystem.OnHeldSlotChanged -= HandleHeldSlotChanged;
            pickupSystem.OnLoadoutChanged -= RefreshAll;
        }

        XGearLoadout.OnChanged -= RefreshAll;

        // กัน timeScale ค้างตอน disable กลางคัน
        if (isOpen)
        {
            Time.timeScale = cachedTimeScale;
            isOpen = false;
        }

        if (view != null)
            view.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (view != null)
            Destroy(view.gameObject);
    }

    void Update()
    {
        if (pickupSystem == null || view == null)
            return;

        // โหมด Hub (Base): ไม่มีการใช้อาวุธ → ซ่อนแถบทั้งหมด และไม่รับปุ่มเปิดแถบ/ลูกกลิ้ง
        bool blocked = pickupSystem.WeaponsBlocked;
        if (view.gameObject.activeSelf == blocked)
            view.gameObject.SetActive(!blocked);

        if (blocked)
            return;

        UpdateHeldAmmo();

        if (wheelAction == null)
            return;

        if (wheelAction.WasPressedThisFrame() && !isOpen && Time.timeScale > 0f && pickupSystem.CanChangeWeapon && !CommandLineUI.IsTyping)
            Open();

        if (!isOpen)
            return;

        HandleScroll();

        if (!wheelAction.IsPressed())
            CloseAndEquip();
    }

    void Open()
    {
        isOpen = true;
        selectedIndex = IndexOf(pickupSystem.CurrentSlot);

        cachedTimeScale = Time.timeScale;
        Time.timeScale = timeScaleWhileOpen;

        RefreshAll();
        SfxLibrary.PlayUI("wheel_open");
    }

    void HandleScroll()
    {
        if (scrollAction == null)
            return;

        float scroll = scrollAction.ReadValue<float>();
        if (Mathf.Abs(scroll) < scrollThreshold)
            return;

        // เลื่อนลง = ไปแถวถัดไป (ล่าง) ข้ามช่องที่ยังไม่มีของ
        int direction = scroll > 0f ? -1 : 1;
        int index = selectedIndex;

        for (int i = 0; i < Order.Length; i++)
        {
            index = (index + direction + Order.Length) % Order.Length;
            if (pickupSystem.IsSlotAvailable(Order[index]))
                break;
        }

        if (index == selectedIndex)
            return;

        selectedIndex = index;
        RefreshAll();
        SfxLibrary.PlayUI("wheel_tick", true);
    }

    void CloseAndEquip()
    {
        isOpen = false;
        Time.timeScale = cachedTimeScale;

        if (pickupSystem.CanChangeWeapon)
            pickupSystem.Select(Order[selectedIndex]);

        RefreshAll();
    }

    void HandleHeldSlotChanged(HeldSlot slot)
    {
        SfxLibrary.PlayUI(slot == HeldSlot.Unarmed ? "weapon_unequip" : "weapon_switch");
        RefreshAll();
    }

    static int IndexOf(HeldSlot slot)
    {
        int index = System.Array.IndexOf(Order, slot);
        return index < 0 ? 0 : index;
    }

    string NameOfSlot(HeldSlot slot)
    {
        switch (slot)
        {
            case HeldSlot.XGear:
                string active = XGearLoadout.ActiveId;
                return string.IsNullOrEmpty(active) ? "No active chip" : ItemCatalog.NameOf(active);
            case HeldSlot.Command:
                CommandRecipe recipe = pickupSystem.CommandItem;
                return recipe != null ? recipe.displayName : "Empty  (Q)";
            case HeldSlot.Knife:
                GameObject knife = pickupSystem.GetItem(HeldSlot.Knife);
                return knife != null ? PickupSystem.DisplayNameOf(knife) : "No knife";
            default:
                return "No weapon";
        }
    }

    // ------------------------------------------------------------ UI

    void EnsureUI()
    {
        if (view != null)
            return;

        view = UIPrefabs.Spawn(WeaponHudView.PrefabName, WeaponHudView.BuildDefault);

        // จำตำแหน่งของแต่ละแถวที่จัดไว้ใน Prefab = ตำแหน่งตอนกางออก
        openPositions = new Vector2[view.rows.Length];
        for (int i = 0; i < view.rows.Length; i++)
            openPositions[i] = view.rows[i] != null ? view.rows[i].Rect.anchoredPosition : Vector2.zero;
    }

    // จัดแถว + ข้อความทั้งหมดใหม่ (เรียกเฉพาะตอนมีอะไรเปลี่ยน ไม่ได้เรียกทุกเฟรม)
    void RefreshAll()
    {
        if (view == null || pickupSystem == null)
            return;

        int heldIndex = IndexOf(pickupSystem.CurrentSlot);
        if (!isOpen)
            selectedIndex = heldIndex;

        if (view.hint != null)
            view.hint.SetActive(isOpen);

        for (int i = 0; i < Order.Length && i < view.rows.Length; i++)
        {
            WeaponHudRowView row = view.rows[i];
            if (row == null)
                continue;

            HeldSlot slot = Order[i];
            bool available = pickupSystem.IsSlotAvailable(slot);
            bool selected = i == selectedIndex;

            // ปิด: เหลือแถวเดียวที่ตำแหน่ง Closed Slot / เปิด: ทุกแถวอยู่ตำแหน่งที่จัดไว้ใน Prefab
            row.gameObject.SetActive(isOpen || i == heldIndex);

            Vector2 position = isOpen || view.closedSlot == null ? openPositions[i] : view.closedSlot.anchoredPosition;
            if (isOpen && selected)
                position.x -= view.selectedShift;
            row.Rect.anchoredPosition = position;

            if (row.background != null)
                row.background.color = !isOpen ? view.idleColor : selected ? view.selectedColor : available ? view.normalColor : view.emptyColor;

            if (row.accent != null)
            {
                row.accent.enabled = !isOpen || i == heldIndex; // ตอนกางออก ขีดสีบอกว่าช่องไหนคือช่องที่ถืออยู่
                row.accent.color = isOpen && selected ? Color.white : UIBuilder.AccentColor;
            }

            Color textColor = available ? UIBuilder.TextColor : UIBuilder.MutedText;
            if (row.itemName != null)
            {
                row.itemName.text = NameOfSlot(slot);
                row.itemName.color = textColor;
            }

            if (row.title != null)
                row.title.color = isOpen && selected ? UIBuilder.TextColor : UIBuilder.MutedText;

            GameObject item = pickupSystem.GetItem(slot);

            if (row.count != null)
            {
                int count = slot == HeldSlot.Command ? pickupSystem.CommandItemCount : -1;
                row.count.text = count > 0 ? "x" + count : string.Empty;
                row.count.color = textColor;
            }

            // ไอคอนอาวุธ (ถ้า ItemData มี)
            WeaponInfo info = item != null ? item.GetComponent<WeaponInfo>() : null;
            Sprite sprite = info != null ? info.Icon : null;
            if (row.icon != null)
            {
                row.icon.sprite = sprite;
                row.icon.enabled = sprite != null;
            }

            GunAction gun = item != null ? item.GetComponent<GunAction>() : null;
            if (row.ammo != null)
            {
                row.ammo.text = gun != null ? AmmoLabel(gun) : string.Empty;
                row.ammo.color = textColor;
            }
        }

        // ปืนที่ถืออยู่: ให้ UpdateHeldAmmo คอยตามค่ากระสุนต่อ
        GameObject held = pickupSystem.HeldItem;
        heldGun = held != null ? held.GetComponent<GunAction>() : null;
        shownAmmo = -1;
    }

    static string AmmoLabel(GunAction gun)
    {
        // ปืนจาก Command มีแม็กเดียว: แสดง กระสุนที่เหลือ / ขนาดแม็ก
        if (gun.CommandMode)
            return gun.CurrentAmmo + " / " + gun.MagazineSize;

        return gun.IsReloading ? "RELOAD" : gun.CurrentAmmo + " / " + gun.ReserveAmmo;
    }

    void UpdateHeldAmmo()
    {
        if (heldGun == null || view == null)
            return;

        int ammo = heldGun.CurrentAmmo;
        int reserve = heldGun.ReserveAmmo;
        bool reloading = heldGun.IsReloading;

        if (ammo == shownAmmo && reserve == shownReserve && reloading == shownReloading)
            return;

        shownAmmo = ammo;
        shownReserve = reserve;
        shownReloading = reloading;

        int index = IndexOf(pickupSystem.CurrentSlot);
        WeaponHudRowView row = index < view.rows.Length ? view.rows[index] : null;
        if (row == null || row.ammo == null)
            return;

        row.ammo.text = AmmoLabel(heldGun);

        // แม็กว่าง = ตัวเลขแดง
        row.ammo.color = ammo <= 0 && !reloading ? new Color(1f, 0.35f, 0.35f, 1f) : UIBuilder.TextColor;
    }
}
