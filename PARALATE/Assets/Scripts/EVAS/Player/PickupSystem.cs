using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ของที่ผู้เล่นถือ แบ่งเป็น 4 ช่อง (เลือกด้วย WeaponWheel: Tab ค้าง + ลูกกลิ้ง)
///   Unarmed : มือเปล่า
///   Knife   : มีดติดตัวตั้งแต่เริ่ม (Knife Prefab) คลิกซ้ายฟันด้านหน้า (ดู KnifeAction)
///   Command : ไอเทม/อาวุธที่เรียกด้วยสูตร CommandLine (Q + WASD)
///             - Consumable (Medkit, X-Gear Cell): คลิกซ้ายเพื่อใช้ 1 ชิ้นจากคลัง หมดแล้วช่องว่าง
///             - Weapon (USP, Sniper, AK): มีแม็กเดียว ยิงหมด = ปืนหายและติด Cooldown ตาม GunAction.commandCooldown
///   X-Gear  : แขนกล คลิกซ้ายใช้ Active skill (ดู XGearSystem)
///
/// เก็บปืน/มีดที่วางในฉาก (กด E) = เป็นเจ้าของถาวรในคลัง + ปลดล็อกสูตร Command ของอาวุธนั้น (ตัวปืนในฉากหายไป)
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PickupSystem : MonoBehaviour
{
    [Header("Pickup Settings")]
    [Tooltip("จุดที่มือถือปืน (ปืนที่เรียกด้วย Command จะถูกวางเป็นลูกของจุดนี้)")]
    public Transform PickUp;

    public Vector3 holdPositionOffset;
    public Vector3 holdRotationOffset;

    [Header("UI")]
    [Tooltip("ไม่บังคับ: ถ้าเว้นว่างจะหา AmmoUIManager ใน Scene เอง")]
    public AmmoUIManager ammoUIManager;

    [Header("Weapon Wheel")]
    public WeaponWheelUI weaponWheel;

    [Header("Knife (ติดตัวตั้งแต่เริ่ม)")]
    [Tooltip("Prefab มีดที่ถือในช่อง Knife (Assets/Prefabs/Items/Knife.prefab)")]
    public GameObject knifePrefab;

    [Header("Command Weapon")]
    [Tooltip("ยิงนัดสุดท้ายแล้วรอกี่วินาทีก่อนปืนหาย (ให้เสียงยิงเล่นจบ)")]
    public float dismissDelayAfterEmpty = 0.35f;

    /// <summary>ช่องที่ถือเปลี่ยน (ใช้เล่นเสียง/อัปเดต HUD)</summary>
    public event Action<HeldSlot> OnHeldSlotChanged;

    /// <summary>ของในช่อง Command เปลี่ยน / จำนวนของใช้เปลี่ยน (HUD ใช้รีเฟรช)</summary>
    public event Action OnLoadoutChanged;

    private HeldSlot currentSlot = HeldSlot.Unarmed;
    private GameObject knifeItem;
    private CommandRecipe commandRecipe;
    private GameObject commandWeapon;
    private GunAction commandGun;
    private Coroutine dismissRoutine;

    private PlayerInput playerInput;
    private InputAction interactAction;
    private InputAction fireAction;
    private TopDownPlayerController movementController;
    private KnockoutSystem knockoutSystem;
    private PickupDetector pickupDetector;
    private bool searchedAmmoUI;

    public HeldSlot CurrentSlot => currentSlot;
    public GameObject HeldItem => GetItem(currentSlot);
    public bool HasEquippedWeapon => HeldItem != null;
    public bool HasKnifeEquipped => currentSlot == HeldSlot.Knife && knifeItem != null;

    /// <summary>สูตรที่อยู่ในช่อง Command (null = ว่าง)</summary>
    public CommandRecipe CommandItem => commandRecipe;

    /// <summary>ปืนที่เรียกมา (null = ไม่ได้เรียกปืน)</summary>
    public GunAction CommandGun => commandGun;

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
        fireAction = playerInput.actions.FindAction("Fire", false);
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
            OnLoadoutChanged?.Invoke();
        }
    }

    void OnEnable()
    {
        PersistentInventory.OnChanged += HandleInventoryChanged;
    }

    void OnDisable()
    {
        PersistentInventory.OnChanged -= HandleInventoryChanged;
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

        // ช่อง Command ที่เป็นของใช้: คลิกซ้ายเพื่อใช้
        if (currentSlot == HeldSlot.Command && commandRecipe != null && commandRecipe.kind == CommandKind.Consumable &&
            fireAction != null && fireAction.WasPressedThisFrame() && Time.timeScale > 0f &&
            (weaponWheel == null || !weaponWheel.IsOpen) && !CommandLineUI.IsTyping)
        {
            UseConsumable();
        }
    }

    // ------------------------------------------------------------ ข้อมูลช่อง

    public static WeaponSlotType SlotTypeOf(GameObject item)
    {
        if (item == null)
            return WeaponSlotType.Auto;

        GunAction gun = item.GetComponent<GunAction>();
        if (gun != null)
            return gun.ResolvedSlot;

        return item.GetComponent<KnifeAction>() != null ? WeaponSlotType.Secondary : WeaponSlotType.Auto;
    }

    /// <summary>GameObject ของช่องนั้น (มีแค่ช่อง Command ที่เป็นปืน)</summary>
    public GameObject GetItem(HeldSlot slot)
    {
        switch (slot)
        {
            case HeldSlot.Command: return commandWeapon;
            case HeldSlot.Knife: return knifeItem;
            default: return null;
        }
    }

    /// <summary>ช่องนี้เลือกถือได้ไหม (Unarmed และ X-Gear เลือกได้เสมอ, Command ต้องมีของ)</summary>
    public bool IsSlotAvailable(HeldSlot slot)
    {
        switch (slot)
        {
            case HeldSlot.Unarmed:
            case HeldSlot.XGear:
                return true;
            case HeldSlot.Command:
                return commandRecipe != null;
            case HeldSlot.Knife:
                return knifeItem != null;
            default:
                return false;
        }
    }

    /// <summary>จำนวนที่เหลือของของใช้ในช่อง Command (ปืน = -1)</summary>
    public int CommandItemCount
    {
        get
        {
            if (commandRecipe == null || commandRecipe.kind != CommandKind.Consumable)
                return -1;

            return PersistentInventory.Instance != null ? PersistentInventory.Instance.GetAmount(commandRecipe.itemId) : 0;
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

    public static string DescribeWeapon(GameObject weapon)
    {
        if (weapon == null)
            return string.Empty;

        GunAction gun = weapon.GetComponent<GunAction>();
        if (gun != null)
        {
            return $"Damage {gun.EffectiveDamage:0.#}  (Headshot x{gun.HeadshotMultiplier:0.#})\n" +
                   $"Fire rate {gun.fireRate:0.##}s  |  Magazine {gun.MagazineSize}\n" +
                   $"Cooldown after the magazine: {gun.commandCooldown:0}s\n" +
                   $"Upgrade Lv.{WeaponUpgrades.GetLevel(gun.UpgradeKey)}";
        }

        KnifeAction knife = weapon.GetComponent<KnifeAction>();
        if (knife != null)
            return "Makes stealth kills from a strangle faster.";

        return string.Empty;
    }

    // ------------------------------------------------------------ เก็บอาวุธในฉาก

    /// <summary>
    /// เก็บปืน/มีดที่วางในฉาก: เข้าคลังถาวร (มีชิ้นเดียวพอ) + ปลดล็อกสูตร Command แล้วลบตัวในฉากทิ้ง
    /// คืน false ถ้าไม่ใช่อาวุธ
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
        Destroy(item);
        return true;
    }

    // ------------------------------------------------------------ Command

    /// <summary>
    /// ใส่ของตามสูตรลงช่อง Command แล้วถือขึ้นมาทันที
    /// คืน false พร้อมเหตุผล ถ้าไม่มีของในคลัง / ปืนติด Cooldown / ไม่มี Prefab
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

        PersistentInventory inventory = PersistentInventory.Instance;
        bool owned = inventory != null && inventory.Has(recipe.itemId);

        if (recipe.kind == CommandKind.Consumable)
        {
            if (!owned)
            {
                reason = "No " + ItemCatalog.NameOf(recipe.itemId) + " left";
                return false;
            }

            ClearCommand();
            commandRecipe = recipe;
        }
        else if (recipe.kind == CommandKind.Weapon)
        {
            if (!owned)
            {
                reason = ItemCatalog.NameOf(recipe.itemId) + " not owned";
                return false;
            }

            float cooldown = CommandBook.CooldownRemaining(recipe);
            if (cooldown > 0f)
            {
                reason = $"{recipe.displayName} cooldown {cooldown:0}s";
                return false;
            }

            // เรียกปืนกระบอกเดิมซ้ำ (ยังมีกระสุน) = ถือขึ้นมาเฉยๆ ไม่เติมแม็ก
            if (commandRecipe == recipe && commandWeapon != null)
            {
                Select(HeldSlot.Command);
                return true;
            }

            if (recipe.weaponPrefab == null)
            {
                reason = recipe.displayName + " has no weapon prefab (Resources/CommandBook)";
                return false;
            }

            ClearCommand();
            commandRecipe = recipe;
            SpawnCommandWeapon(recipe.weaponPrefab);
        }
        else
        {
            reason = "This command does not load an item";
            return false;
        }

        OnLoadoutChanged?.Invoke();

        // ถืออยู่ช่อง Command อยู่แล้ว: บังคับเปิดปืนใหม่ให้ถือ
        if (currentSlot == HeldSlot.Command)
        {
            currentSlot = HeldSlot.Unarmed;
        }

        Select(HeldSlot.Command);
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

    void SpawnCommandWeapon(GameObject prefab)
    {
        commandWeapon = SpawnHeld(prefab);

        commandGun = commandWeapon.GetComponent<GunAction>();
        if (commandGun != null)
        {
            commandGun.SetCommandMode(true);
            commandGun.OnMagazineEmptied += HandleMagazineEmptied;
        }

        SetWeaponHeld(commandWeapon, false);
    }

    void HandleMagazineEmptied()
    {
        if (dismissRoutine != null)
            StopCoroutine(dismissRoutine);

        dismissRoutine = StartCoroutine(DismissAfterDelay());
    }

    IEnumerator DismissAfterDelay()
    {
        yield return new WaitForSeconds(dismissDelayAfterEmpty);
        dismissRoutine = null;
        ClearCommand();
    }

    /// <summary>
    /// ล้างช่อง Command: ปืนที่เรียกมาหายไป ถ้ายิงไปแล้วอย่างน้อย 1 นัด จะเริ่ม Cooldown ของปืนนั้น
    /// (กันเรียกซ้ำเพื่อเติมแม็กฟรี)
    /// </summary>
    public void ClearCommand()
    {
        if (dismissRoutine != null)
        {
            StopCoroutine(dismissRoutine);
            dismissRoutine = null;
        }

        bool wasHolding = currentSlot == HeldSlot.Command;

        if (commandGun != null)
        {
            commandGun.OnMagazineEmptied -= HandleMagazineEmptied;

            if (commandGun.CurrentAmmo < commandGun.MagazineSize)
                CommandBook.StartCooldown(commandRecipe, commandGun.commandCooldown);
        }

        if (commandWeapon != null)
        {
            SetWeaponHeld(commandWeapon, false);
            Destroy(commandWeapon);
        }

        commandWeapon = null;
        commandGun = null;
        commandRecipe = null;

        if (wasHolding)
            Select(HeldSlot.Unarmed);

        OnLoadoutChanged?.Invoke();
    }

    void UseConsumable()
    {
        PersistentInventory inventory = PersistentInventory.Instance;
        if (commandRecipe == null || inventory == null || !inventory.Has(commandRecipe.itemId))
        {
            ClearCommand();
            return;
        }

        string reason = null;
        bool used = false;

        if (commandRecipe.itemId == ItemCatalog.Medkit)
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            used = health != null && health.Heal(commandRecipe.effectAmount);
            if (!used) reason = "Health is full";
        }
        else if (commandRecipe.itemId == ItemCatalog.XGearCell)
        {
            XGearSystem gear = GetComponent<XGearSystem>();
            used = gear != null && gear.AddCharges(Mathf.Max(1, Mathf.RoundToInt(commandRecipe.effectAmount)), out reason);
        }

        if (!used)
        {
            SfxLibrary.Play("ui_error");
            if (!string.IsNullOrEmpty(reason))
                HudToast.Show(reason);
            return;
        }

        SfxLibrary.Play(commandRecipe.itemId == ItemCatalog.Medkit ? "medkit_use" : "xgear_activate");
        inventory.TrySpend(commandRecipe.itemId, 1); // OnChanged → HandleInventoryChanged เคลียร์ช่องเองถ้าหมด
    }

    void HandleInventoryChanged()
    {
        if (commandRecipe != null && commandRecipe.kind == CommandKind.Consumable && CommandItemCount <= 0)
            ClearCommand();
        else
            OnLoadoutChanged?.Invoke();
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

        SetWeaponHeld(commandWeapon, false);
        SetWeaponHeld(knifeItem, false);

        currentSlot = slot;

        if (target != null)
            SetWeaponHeld(target, true);

        RefreshWeaponHud();
        OnHeldSlotChanged?.Invoke(slot);
        return true;
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

    void RefreshWeaponHud()
    {
        if (ammoUIManager == null && !searchedAmmoUI)
        {
            searchedAmmoUI = true;
            ammoUIManager = FindAnyObjectByType<AmmoUIManager>(FindObjectsInactive.Include);
        }

        if (ammoUIManager == null)
            return;

        if (currentSlot == HeldSlot.XGear)
            ammoUIManager.SetLabelOnly("X-GEAR");
        else
            ammoUIManager.SetActiveWeapon(HeldItem);
    }

    // ------------------------------------------------------------ ของเดิม (ยังมีสคริปต์อื่นเรียกใช้)

    /// <summary>ปืนที่ถืออยู่ตอนนี้ (มีได้แค่ปืนที่เรียกด้วย Command)</summary>
    public List<GameObject> GetOwnedWeapons()
    {
        List<GameObject> weapons = new List<GameObject>();
        if (commandWeapon != null) weapons.Add(commandWeapon);
        return weapons;
    }

    public void UnequipWeaponPublic()
    {
        Select(HeldSlot.Unarmed);
    }
}
