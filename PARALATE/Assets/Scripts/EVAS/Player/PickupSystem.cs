using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ของที่ผู้เล่นถือ แบ่งเป็น 4 ช่อง (เลือกด้วย WeaponWheel: Tab ค้าง + ลูกกลิ้ง)
///   Unarmed : มือเปล่า
///   Knife   : มีดติดตัวตั้งแต่เริ่ม (Knife Prefab) คลิกซ้ายฟันด้านหน้า (ดู KnifeAction)
///   Weapon  : ปืนที่ถือ 1 กระบอก
///             - ว่างอยู่ = ปืนชิ้นแรกที่เก็บได้เข้าช่องนี้เอง
///             - เปลี่ยนปืน = พิมพ์สูตร CommandLine ของปืนที่เป็นเจ้าของ (ปืนเดิมกลับเข้าคลัง กระสุนในแม็กถูกจำไว้)
///             - ไม่มี Cooldown / ยิงหมดแม็กปืนไม่หาย รีโหลด (R) จากกระสุนในกระเป๋าตามปกติ
///   X-Gear  : แขนกล คลิกซ้ายใช้ Active Chip ที่เรียกด้วย Command (ดู XGearSystem)
///
/// ของใช้ (Medkit, X-Gear Cell): พิมพ์สูตร = ใช้ทันที หรือใช้จากหน้า Esc > Inventory (คลิกขวา > Use)
/// เก็บปืน/มีดที่วางในฉาก (กด E) = เป็นเจ้าของถาวรในคลัง + ปลดล็อกสูตร Command ของอาวุธนั้น (ตัวปืนในฉากหายไป)
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PickupSystem : MonoBehaviour
{
    [Header("Pickup Settings")]
    [Tooltip("จุดที่มือถือปืน (ปืนในช่อง Weapon จะถูกวางเป็นลูกของจุดนี้)")]
    public Transform PickUp;

    public Vector3 holdPositionOffset;
    public Vector3 holdRotationOffset;

    [Header("Weapon Wheel")]
    public WeaponWheelUI weaponWheel;

    [Header("Knife (ติดตัวตั้งแต่เริ่ม)")]
    [Tooltip("Prefab มีดที่ถือในช่อง Knife (Assets/Prefabs/Items/Knife.prefab)")]
    public GameObject knifePrefab;

    /// <summary>ช่องที่ถือเปลี่ยน (ใช้เล่นเสียง/อัปเดต HUD)</summary>
    public event Action<HeldSlot> OnHeldSlotChanged;

    /// <summary>ปืนในช่อง Weapon เปลี่ยน (HUD ใช้รีเฟรช)</summary>
    public event Action OnLoadoutChanged;

    // ปืนในช่อง Weapon ติดตัวข้ามด่าน (PlayerPrefs เหมือนคลังถาวร)
    private const string EquippedWeaponKey = "Weapon_Equipped";

    // กระสุนในแม็กของปืนแต่ละกระบอก จำไว้ตอนสลับปืน (ใช้ได้ตลอดการเปิดเกม)
    private static readonly Dictionary<string, int> loadedAmmo = new Dictionary<string, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => loadedAmmo.Clear();

    private HeldSlot currentSlot = HeldSlot.Unarmed;
    private GameObject knifeItem;
    private CommandRecipe weaponRecipe;
    private GameObject weaponObject;
    private GunAction weaponGun;

    private PlayerInput playerInput;
    private InputAction interactAction;
    private TopDownPlayerController movementController;
    private KnockoutSystem knockoutSystem;
    private PickupDetector pickupDetector;

    public HeldSlot CurrentSlot => currentSlot;
    public GameObject HeldItem => GetItem(currentSlot);
    public bool HasKnifeEquipped => currentSlot == HeldSlot.Knife && knifeItem != null;

    /// <summary>สูตรของปืนในช่อง Weapon (null = ว่าง)</summary>
    public CommandRecipe WeaponRecipe => weaponRecipe;

    /// <summary>ปืนในช่อง Weapon (null = ว่าง)</summary>
    public GunAction WeaponGun => weaponGun;

    /// <summary>ห้ามถืออาวุธทุกชนิด (โหมด Hub ใน Base): ถือได้แค่มือเปล่า</summary>
    public bool WeaponsBlocked { get; private set; }

    public void SetWeaponsBlocked(bool blocked)
    {
        if (WeaponsBlocked == blocked)
            return;

        if (blocked)
            Select(HeldSlot.Unarmed);

        WeaponsBlocked = blocked;
        OnLoadoutChanged?.Invoke();
    }

    /// <summary>เปลี่ยนช่องได้หรือไม่ (ห้ามตอนถูกล็อก/กำลังรัดคอ/โหมด Hub)</summary>
    public bool CanChangeWeapon =>
        !WeaponsBlocked &&
        (movementController == null || !movementController.IsMovementLocked) &&
        (knockoutSystem == null || !knockoutSystem.IsKnockout);

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        interactAction = playerInput.actions["Interact"];
        movementController = GetComponent<TopDownPlayerController>();
        knockoutSystem = GetComponent<KnockoutSystem>();
        pickupDetector = GetComponent<PickupDetector>();

        if (weaponWheel == null)
            weaponWheel = GetComponent<WeaponWheelUI>();
    }

    void Start()
    {
        // มีดติดตัว: สร้างจาก Prefab วางที่มือแล้วซ่อนไว้ จนกว่าจะเลือกช่อง Knife
        if (knifePrefab != null && knifeItem == null)
        {
            knifeItem = SpawnHeld(knifePrefab);
            KnifeAction knife = knifeItem.GetComponent<KnifeAction>();
            if (knife != null)
                knife.playerTransform = transform;
            SetWeaponHeld(knifeItem, false);
        }

        // ปืนในช่อง Weapon จากด่านก่อน / เซฟ
        CommandRecipe saved = FindWeaponRecipe(PlayerPrefs.GetString(EquippedWeaponKey, string.Empty));
        if (saved != null && IsOwned(saved.itemId))
            EquipWeapon(saved, false);

        OnLoadoutChanged?.Invoke();
    }

    void OnDisable()
    {
        RememberLoadedAmmo();
    }

    void Update()
    {
        if (knockoutSystem != null && knockoutSystem.IsKnockout)
            return;

        if (movementController != null && movementController.IsMovementLocked)
        {
            // ล็อกอยู่ (อ่านโน้ต ฯลฯ) ยังกด E ได้ เพื่อปิดโน้ต แต่ห้ามตอนเกมหยุด (เมนู Esc)
            if (Time.timeScale > 0f && pickupDetector != null && interactAction.WasPressedThisFrame())
                pickupDetector.TryInteract();

            return;
        }

        if (pickupDetector != null && interactAction.WasPressedThisFrame())
            pickupDetector.TryInteract();
    }

    // ------------------------------------------------------------ ข้อมูลช่อง

    /// <summary>GameObject ของช่องนั้น (Weapon = ปืน, Knife = มีด)</summary>
    public GameObject GetItem(HeldSlot slot)
    {
        switch (slot)
        {
            case HeldSlot.Weapon: return weaponObject;
            case HeldSlot.Knife: return knifeItem;
            default: return null;
        }
    }

    /// <summary>ช่องนี้เลือกถือได้ไหม (Unarmed และ X-Gear เลือกได้เสมอ, Weapon ต้องมีปืน)</summary>
    public bool IsSlotAvailable(HeldSlot slot)
    {
        switch (slot)
        {
            case HeldSlot.Unarmed:
            case HeldSlot.XGear:
                return true;
            case HeldSlot.Weapon:
                return weaponObject != null;
            case HeldSlot.Knife:
                return knifeItem != null;
            default:
                return false;
        }
    }

    public static string DisplayNameOf(GameObject weapon)
    {
        if (weapon == null)
            return "Empty";

        WeaponInfo info = weapon.GetComponent<WeaponInfo>();
        string display = info != null ? info.DisplayName : weapon.name;
        return display.Replace("(Clone)", string.Empty).Trim();
    }

    private static bool IsOwned(string itemId) =>
        PersistentInventory.Instance != null && PersistentInventory.Instance.Has(itemId);

    /// <summary>สูตร Command ของปืน id นี้ (weapon_ak ฯลฯ)</summary>
    private static CommandRecipe FindWeaponRecipe(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return null;

        IReadOnlyList<CommandRecipe> all = CommandBook.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].kind == CommandKind.Weapon && all[i].itemId == itemId)
                return all[i];
        }

        return null;
    }

    // ------------------------------------------------------------ เก็บอาวุธในฉาก

    /// <summary>
    /// เก็บปืน/มีดที่วางในฉาก: เข้าคลังถาวร (มีชิ้นเดียวพอ) + ปลดล็อกสูตร Command แล้วลบตัวในฉากทิ้ง
    /// ช่อง Weapon ยังว่าง = ปืนชิ้นนี้เข้าช่องทันที คืน false ถ้าไม่ใช่อาวุธ
    /// </summary>
    public bool PickUpObject(GameObject item)
    {
        string weaponId = ItemCatalog.WeaponIdOf(item);
        if (weaponId == null)
            return false;

        PersistentInventory inventory = PersistentInventory.Instance;
        if (inventory != null && !inventory.Has(weaponId))
            inventory.Add(weaponId, 1);

        CommandBook.LearnFromItem(weaponId);

        if (weaponObject == null && !WeaponsBlocked)
        {
            CommandRecipe recipe = FindWeaponRecipe(weaponId);
            if (recipe != null)
                EquipWeapon(recipe, false);
        }

        Destroy(item);
        return true;
    }

    // ------------------------------------------------------------ Command

    /// <summary>
    /// ทำตามสูตร CommandLine (ยกเว้น SaveGame ที่ CommandLineUI จัดการเอง)
    ///   Weapon     : เปลี่ยนปืนในช่อง Weapon แล้วถือขึ้นมา
    ///   Consumable : ใช้ของทันที
    ///   XGearChip  : ใส่ Active Chip เข้าช่อง X-Gear แล้วถือ X-Gear
    /// คืน false พร้อมเหตุผลถ้าทำไม่ได้
    /// </summary>
    public bool TryLoadCommand(CommandRecipe recipe, out string reason)
    {
        reason = null;

        if (recipe == null)
        {
            reason = "Unknown command";
            return false;
        }

        if (WeaponsBlocked)
        {
            reason = "Not here";
            return false;
        }

        switch (recipe.kind)
        {
            case CommandKind.Consumable:
                return UseConsumable(recipe.itemId, out reason);

            case CommandKind.Weapon:
                if (!IsOwned(recipe.itemId))
                {
                    reason = ItemCatalog.NameOf(recipe.itemId) + " not owned";
                    return false;
                }

                if (recipe.weaponPrefab == null)
                {
                    reason = recipe.displayName + " has no weapon prefab (ItemData)";
                    return false;
                }

                if (weaponRecipe != recipe || weaponObject == null)
                    EquipWeapon(recipe, false);

                Select(HeldSlot.Weapon);
                return true;

            case CommandKind.XGearChip:
                XGearSystem gear = GetComponent<XGearSystem>();
                if (gear == null || !gear.LoadChip(recipe.itemId, out reason))
                {
                    reason ??= "No X-Gear";
                    return false;
                }

                Select(HeldSlot.XGear);
                return true;

            default:
                reason = "This command does not load an item";
                return false;
        }
    }

    /// <summary>ใส่ปืนตามสูตรเข้าช่อง Weapon (ปืนเดิมกลับเข้าคลัง กระสุนในแม็กถูกจำไว้)</summary>
    void EquipWeapon(CommandRecipe recipe, bool select)
    {
        bool wasHolding = currentSlot == HeldSlot.Weapon;
        RemoveWeaponObject();

        weaponRecipe = recipe;
        weaponObject = SpawnHeld(recipe.weaponPrefab);
        weaponGun = weaponObject.GetComponent<GunAction>();

        if (weaponGun != null && loadedAmmo.TryGetValue(recipe.itemId, out int ammo))
            weaponGun.SetLoadedAmmo(ammo);

        SetWeaponHeld(weaponObject, false);
        PlayerPrefs.SetString(EquippedWeaponKey, recipe.itemId);
        PlayerPrefs.Save();

        OnLoadoutChanged?.Invoke();

        // ถือช่อง Weapon อยู่: ถือปืนกระบอกใหม่ต่อทันที
        if (wasHolding || select)
        {
            currentSlot = HeldSlot.Unarmed;
            Select(HeldSlot.Weapon);
        }
    }

    void RemoveWeaponObject()
    {
        RememberLoadedAmmo();

        if (weaponObject != null)
        {
            SetWeaponHeld(weaponObject, false);
            Destroy(weaponObject);
        }

        weaponObject = null;
        weaponGun = null;
        weaponRecipe = null;
    }

    void RememberLoadedAmmo()
    {
        if (weaponGun != null && weaponRecipe != null)
            loadedAmmo[weaponRecipe.itemId] = weaponGun.CurrentAmmo;
    }

    // ------------------------------------------------------------ ของใช้ (Command / หน้า Inventory)

    /// <summary>ปริมาณต่อการใช้ 1 ชิ้น (ตั้งใน ItemData ส่วน CommandLine > Effect Amount)</summary>
    public static float EffectAmountOf(string itemId)
    {
        IReadOnlyList<CommandRecipe> all = CommandBook.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].kind == CommandKind.Consumable && all[i].itemId == itemId)
                return all[i].effectAmount;
        }

        return itemId == ItemCatalog.Medkit ? 30f : 1f;
    }

    /// <summary>ใช้ของ 1 ชิ้นจากคลัง (Medkit = ฟื้นเลือด, X-Gear Cell = เติม Charge) คืน false + เหตุผลถ้าใช้ไม่ได้</summary>
    public bool UseConsumable(string itemId, out string reason)
    {
        reason = null;
        PersistentInventory inventory = PersistentInventory.Instance;

        if (inventory == null || !inventory.Has(itemId))
        {
            reason = "No " + ItemCatalog.NameOf(itemId) + " left";
            return false;
        }

        float amount = EffectAmountOf(itemId);
        bool used = false;

        if (itemId == ItemCatalog.Medkit)
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            used = health != null && health.Heal(amount);
            if (!used) reason = "Health is full";
        }
        else if (itemId == ItemCatalog.XGearCell)
        {
            XGearSystem gear = GetComponent<XGearSystem>();
            used = gear != null && gear.AddCharges(Mathf.Max(1, Mathf.RoundToInt(amount)), out reason);
        }
        else
        {
            reason = ItemCatalog.NameOf(itemId) + " can't be used";
        }

        if (!used)
            return false;

        SfxLibrary.Play(itemId == ItemCatalog.Medkit ? "medkit_use" : "xgear_activate");
        inventory.TrySpend(itemId, 1);
        return true;
    }

    // ------------------------------------------------------------ เลือกช่อง

    /// <summary>เลือกช่องที่จะถือ (เรียกจากวงล้อ Tab) ช่องว่างจะถูกปฏิเสธ</summary>
    public bool Select(HeldSlot slot)
    {
        if (!IsSlotAvailable(slot))
            return false;

        if (WeaponsBlocked && slot != HeldSlot.Unarmed)
            return false;

        GameObject target = GetItem(slot);

        // ถือชิ้นนี้อยู่แล้ว ไม่ต้องปิด/เปิดใหม่ (กันเสียง equip เล่นซ้ำ)
        if (slot == currentSlot && (target == null || target.activeSelf))
            return true;

        SetWeaponHeld(weaponObject, false);
        SetWeaponHeld(knifeItem, false);

        currentSlot = slot;

        if (target != null)
            SetWeaponHeld(target, true);

        OnHeldSlotChanged?.Invoke(slot); // Weapon HUD (WeaponWheelUI) ฟัง event นี้
        return true;
    }

    // สร้างของจาก Prefab วางที่มือ: ปิดฟิสิกส์/Collider และปิดการกด E เก็บ (ไม่ใช่ของที่วางในฉาก)
    GameObject SpawnHeld(GameObject prefab)
    {
        Transform parent = PickUp != null ? PickUp : transform;
        GameObject item = Instantiate(prefab, parent);
        item.name = prefab.name;
        item.transform.localPosition = holdPositionOffset;
        item.transform.localEulerAngles = holdRotationOffset;

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;

        foreach (Collider coll in item.GetComponentsInChildren<Collider>())
            coll.enabled = false;

        ItemPickup pickup = item.GetComponent<ItemPickup>();
        if (pickup != null)
            pickup.enabled = false;

        return item;
    }

    void SetWeaponHeld(GameObject item, bool held)
    {
        if (item == null)
            return;

        item.SetActive(held);

        GunAction gun = item.GetComponent<GunAction>();
        if (gun != null)
            gun.SetHeld(held);

        KnifeAction knife = item.GetComponent<KnifeAction>();
        if (knife != null)
            knife.isHeld = held;
    }

    /// <summary>ล้างปืนที่ติดตัวข้ามด่าน (เมนูรีเซ็ตเซฟใช้)</summary>
    public static void ResetSave()
    {
        PlayerPrefs.DeleteKey(EquippedWeaponKey);
        loadedAmmo.Clear();
    }

    /// <summary>id ของปืนในช่อง Weapon ที่บันทึกไว้ (ระบบเซฟใช้)</summary>
    public static string SavedWeaponId
    {
        get => PlayerPrefs.GetString(EquippedWeaponKey, string.Empty);
        set
        {
            PlayerPrefs.SetString(EquippedWeaponKey, value ?? string.Empty);
            PlayerPrefs.Save();
        }
    }
}
