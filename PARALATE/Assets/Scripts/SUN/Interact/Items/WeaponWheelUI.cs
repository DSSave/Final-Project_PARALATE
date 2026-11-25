using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// HUD อาวุธมุมขวาล่าง (แนวตั้ง):
///   - ปกติ: ซ่อนอยู่ (Auto Hide) / เปลี่ยนอาวุธแล้วโชว์แถวเดียว = ช่องที่ถืออยู่ ค้างไว้ 1 วิ แล้วจางหาย
///   - กด Tab ค้าง: กางเป็น 4 แถว (บนลงล่าง) X-Gear / Weapon / Knife / Unarmed
///     เลื่อนลูกกลิ้งเพื่อเปลี่ยน (มีเสียง + แถวที่เลือกเปลี่ยนสีและยื่นออกมา) ปล่อย Tab เพื่อยืนยัน
///     ลำดับการเลื่อนตามตำแหน่งแถวบนจอ (ย้ายแถวใน Prefab ได้ ลูกกลิ้งเลื่อนตามลำดับใหม่เอง)
///   - Weapon: ปืนที่ถือ แสดง กระสุนในแม็ก / กระสุนในกระเป๋า (เปลี่ยนปืนด้วย Command)
///   - X-Gear: Active Chip ที่เรียกด้วย Command และจำนวน Charge ที่เหลือ
///
/// หน้าตา = WeaponHudView ที่วางอยู่ใน Prefab MainHUD (ตัวเดียวกับ Resources/UI/WeaponHUD แบบ Nested Prefab)
///   ปรับตำแหน่ง/สี/ฟอนต์/เวลาซ่อนอัตโนมัติ ได้ที่ MainHUD > WeaponHUD หรือเปิด Prefab WeaponHUD แก้ทุกที่พร้อมกัน
///   Scene ที่ไม่มี MainHUD จะสร้างจาก Resources/UI/WeaponHUD ให้เอง
/// สคริปต์นี้คุมแค่การทำงาน
/// </summary>
public class WeaponWheelUI : MonoBehaviour
{
    [Header("References")]
    public PickupSystem pickupSystem;
    public PlayerInput playerInput;

    [Header("Behaviour")]
    [Tooltip("ชะลอเวลาตอนเปิดแถบ (1 = ปกติ, 0.2 = slow motion)")]
    [Range(0.05f, 1f)] public float timeScaleWhileOpen = 0.2f;
    [Tooltip("ค่า scroll ขั้นต่ำที่นับเป็น 1 คลิก")]
    public float scrollThreshold = 0.1f;

    // ค่าซ่อน/แสดงอัตโนมัติอยู่ที่ WeaponHudView (ปรับที่ตัว HUD ใน MainHUD ได้เลย)
    private CanvasGroup hudGroup;
    private bool ownsView; // สร้าง HUD เอง (Scene ไม่มี MainHUD) = ต้องลบเองตอนจบ
    private float hideAt; // เวลา (unscaled) ที่เริ่มจาง

    // ลำดับตรงกับ WeaponHudView.rows (ห้ามสลับ) — ลำดับบนจอดูจากตำแหน่งแถว (visualOrder)
    private static readonly HeldSlot[] Order = { HeldSlot.Unarmed, HeldSlot.Knife, HeldSlot.Weapon, HeldSlot.XGear };
    private int[] visualOrder = { 0, 1, 2, 3 }; // index ของแถว เรียงจากบนลงล่างตามตำแหน่งใน Prefab
    private XGearSystem xgear;

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
    }

    void Start()
    {
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

        XGearSystem.OnChanged += RefreshAll;

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

        XGearSystem.OnChanged -= RefreshAll;

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
        // HUD ที่วางไว้ใน MainHUD เป็นของ MainHUD ไม่ลบ
        if (view != null && ownsView)
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
        UpdateAutoHide();

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

        // เลื่อนลง = ไปแถวที่อยู่ล่างลงไปบนจอ / เลื่อนขึ้น = แถวบน ข้ามช่องที่ยังไม่มีของ
        int direction = scroll > 0f ? -1 : 1;
        int visual = System.Array.IndexOf(visualOrder, selectedIndex);
        int index = selectedIndex;

        for (int i = 0; i < visualOrder.Length; i++)
        {
            visual = (visual + direction + visualOrder.Length) % visualOrder.Length;
            index = visualOrder[visual];
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

        ShowForAWhile(); // ปล่อย Tab: ค้างไว้ให้เห็นอาวุธที่เลือก แล้วค่อยหาย
        RefreshAll();
    }

    void HandleHeldSlotChanged(HeldSlot slot)
    {
        SfxLibrary.PlayUI(slot == HeldSlot.Unarmed ? "weapon_unequip" : "weapon_switch");
        ShowForAWhile(); // เปลี่ยนอาวุธด้วยวิธีอื่น (Command, เก็บปืน) ก็ขึ้นให้เห็นสักครู่
        RefreshAll();
    }

    // ------------------------------------------------------------ ซ่อน HUD อัตโนมัติ
    // หลักการ: ใช้ CanvasGroup.alpha ของ HUD (ไม่ปิด GameObject) แถวต่างๆ จึงยังอยู่ที่เดิมและอัปเดตค่ากระสุนต่อได้
    // เวลาใช้ unscaledTime เพราะตอนกด Tab เกมถูกชะลอ (timeScale 0.2)

    void ShowForAWhile()
    {
        hideAt = Time.unscaledTime + (view != null ? view.showAfterChangeSeconds : 1f);
    }

    void UpdateAutoHide()
    {
        if (hudGroup == null)
            return;

        bool visible = !view.autoHide || isOpen || Time.unscaledTime < hideAt;
        float target = visible ? 1f : 0f;

        if (visible || view.fadeOutSeconds <= 0f)
            hudGroup.alpha = target; // ขึ้นทันที
        else
            hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, target, Time.unscaledDeltaTime / view.fadeOutSeconds);
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
                string chip = xgear != null ? xgear.LoadedChip : string.Empty;
                return string.IsNullOrEmpty(chip) ? "No chip  (Q)" : ItemCatalog.NameOf(chip).Replace("Active Chip : ", string.Empty);
            case HeldSlot.Weapon:
                GameObject weapon = pickupSystem.GetItem(HeldSlot.Weapon);
                return weapon != null ? PickupSystem.DisplayNameOf(weapon) : "Empty  (Q)";
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

        // ใช้ HUD ที่วางไว้ใน MainHUD ก่อน (ปรับตำแหน่ง/สีใน Prefab MainHUD ได้) ไม่มีค่อยสร้างจาก Resources/UI/WeaponHUD
        view = FindAnyObjectByType<WeaponHudView>(FindObjectsInactive.Include);
        ownsView = view == null;
        if (view == null)
            view = UIPrefabs.Spawn(WeaponHudView.PrefabName, WeaponHudView.BuildDefault);
        view.gameObject.SetActive(true);

        // ซ่อน/แสดงทั้ง HUD ด้วย CanvasGroup (มีใน Prefab แล้วใช้ตัวนั้น ไม่มีก็เพิ่มให้ตอนรัน)
        hudGroup = view.GetComponent<CanvasGroup>();
        if (hudGroup == null)
            hudGroup = view.gameObject.AddComponent<CanvasGroup>();
        hudGroup.blocksRaycasts = false;
        hudGroup.interactable = false;
        hudGroup.alpha = view.autoHide ? 0f : 1f; // เริ่มฉาก: ซ่อนไว้จนกว่าจะกด Tab

        // จำตำแหน่งของแต่ละแถวที่จัดไว้ใน Prefab = ตำแหน่งตอนกางออก
        openPositions = new Vector2[view.rows.Length];
        for (int i = 0; i < view.rows.Length; i++)
            openPositions[i] = view.rows[i] != null ? view.rows[i].Rect.anchoredPosition : Vector2.zero;

        // ลำดับบนจอ (บนลงล่าง) จากตำแหน่ง Y ของแต่ละแถว ใช้กับลูกกลิ้ง
        int count = Mathf.Min(Order.Length, view.rows.Length);
        visualOrder = new int[count];
        for (int i = 0; i < count; i++) visualOrder[i] = i;
        System.Array.Sort(visualOrder, (a, b) => openPositions[b].y.CompareTo(openPositions[a].y));
    }

    // จัดแถว + ข้อความทั้งหมดใหม่ (เรียกเฉพาะตอนมีอะไรเปลี่ยน ไม่ได้เรียกทุกเฟรม)
    void RefreshAll()
    {
        if (view == null || pickupSystem == null)
            return;

        // XGearSystem ถูกเพิ่มให้ผู้เล่นตอน Start (PlayerSystems) จึงหาทีหลัง
        if (xgear == null)
            xgear = GetComponent<XGearSystem>();

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
                // X-Gear: Charge ที่เหลือ / สูงสุด
                row.count.text = slot == HeldSlot.XGear && xgear != null ? xgear.Charges + "/" + xgear.MaxCharges : string.Empty;
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
        // กระสุนในแม็ก / กระสุนในกระเป๋า
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
