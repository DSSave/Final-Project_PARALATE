using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// X-Gear (Universal): แขนกลที่ติดตัวผู้เล่น ชิปทุกชิ้นอยู่ในคลัง (PersistentInventory) ไม่ต้องติดตั้งทีละช่องแล้ว
///   Passive Chip : ได้ผลอัตโนมัติ และซ้อนตามจำนวนที่เก็บได้ (FleetFoot 2 ชิ้น = ย่อเดินเร็วขึ้น 10%)
///   Active Chip  : พิมพ์สูตร CommandLine ของชิป → ชิปเข้าช่อง X-Gear และถือ X-Gear ขึ้นมา → คลิกซ้ายเพื่อใช้
///                  ใช้ครั้งละ 1 Charge (เริ่มด่านเต็ม maxCharges / X-Gear Cell เติมทีละหน่วย)
///   Map Chip     : ดู MapArea (เข้าโซนที่มีชิปของโซนนั้น = Minimap ขึ้นเอง)
/// คอมโพเนนต์นี้ถูกเพิ่มให้ผู้เล่นอัตโนมัติ (PlayerSystems) ไม่ต้องแก้ Prefab
/// </summary>
[DisallowMultipleComponent]
public class XGearSystem : MonoBehaviour
{
    [Header("Charges")]
    [Tooltip("จำนวนครั้งสูงสุดที่ใช้ Active Chip ได้ (เริ่มด่านมาเต็มเท่านี้ / X-Gear Cell เติมได้ไม่เกินนี้)")]
    [Min(1)] public int maxCharges = 1;

    [Header("Active Chip : Invisible")]
    public float invisibleDuration = 5f;

    [Header("Passive Chip : FleetFoot")]
    [Tooltip("ความเร็วตอนย่อที่เพิ่มต่อชิป 1 ชิ้น (0.05 = +5%) ซ้อนตามจำนวนชิปในคลัง")]
    public float fleetFootBonusPerChip = 0.05f;

    [Header("HUD")]
    public Color chargeColor = new Color(0.25f, 0.7f, 1f, 1f);

    /// <summary>Active Chip ในช่อง X-Gear เปลี่ยน / Charge เปลี่ยน (WeaponHUD ใช้รีเฟรช)</summary>
    public static event Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnChanged = null;

    private TopDownPlayerController controller;
    private PickupSystem pickup;
    private WeaponWheelUI wheel;
    private InputAction fireAction;

    private string loadedChip = string.Empty;
    private int charges;
    private float invisibleUntil;
    private bool isInvisible;

    private GameObject chargeRoot;
    private Graphic chargeFill;
    private Renderer[] bodyRenderers;

    public int Charges => charges;
    public int MaxCharges => Mathf.Max(1, maxCharges);
    public bool IsInvisible => isInvisible;

    /// <summary>Active Chip ที่อยู่ในช่อง X-Gear ตอนนี้ (ว่าง = ยังไม่ได้เรียกชิป)</summary>
    public string LoadedChip => loadedChip;

    private void Awake()
    {
        controller = GetComponent<TopDownPlayerController>();
        pickup = GetComponent<PickupSystem>();
        wheel = GetComponent<WeaponWheelUI>();
        charges = MaxCharges; // เริ่มด่านเต็ม (คอมโพเนนต์อยู่บนผู้เล่นของแต่ละ Scene จึงรีเซ็ตเองทุกด่าน)

        PlayerInput input = GetComponent<PlayerInput>();
        if (input != null && input.actions != null)
            fireAction = input.actions.FindAction("Fire", false);
    }

    private void OnEnable()
    {
        PersistentInventory.OnChanged += ApplyPassives;
    }

    private void Start()
    {
        MigrateOldLoadout();
        FindOrBuildChargeBar();
        ApplyPassives();
        RefreshChargeBar();
        MapArea.RefreshMinimap(); // Scene ที่ไม่มีโซน Minimap เลย = ซ่อน Minimap
    }

    private void OnDisable()
    {
        PersistentInventory.OnChanged -= ApplyPassives;
        SetInvisible(false);
    }

    // ------------------------------------------------------------ Passive: ซ้อนตามจำนวนชิปในคลัง

    private void ApplyPassives()
    {
        PersistentInventory inventory = PersistentInventory.Instance;
        int fleetFoot = inventory != null ? inventory.GetAmount(ItemCatalog.ChipFleetFoot) : 0;

        if (controller != null)
            controller.CrouchSpeedMultiplier = 1f + fleetFoot * fleetFootBonusPerChip;

        // ชิปที่เรียกไว้ถูกทิ้ง/หายจากคลัง: ล้างช่อง
        if (!string.IsNullOrEmpty(loadedChip) && (inventory == null || !inventory.Has(loadedChip)))
            SetLoadedChip(string.Empty);
    }

    // ------------------------------------------------------------ Active: เรียกด้วย Command แล้วคลิกซ้าย

    /// <summary>ใส่ Active Chip เข้าช่อง X-Gear (CommandLine เรียก) ต้องมีชิปในคลัง</summary>
    public bool LoadChip(string chipId, out string reason)
    {
        reason = null;
        ItemDef def = ItemCatalog.Get(chipId);

        if (def == null || def.category != ItemCategory.ActiveChip)
        {
            reason = "Not an Active Chip";
            return false;
        }

        if (PersistentInventory.Instance == null || !PersistentInventory.Instance.Has(chipId))
        {
            reason = def.displayName + " not owned";
            return false;
        }

        SetLoadedChip(chipId);
        SfxLibrary.PlayUI("chip_insert");
        return true;
    }

    /// <summary>ตั้งชิปในช่องตรงๆ (ใช้ตอนโหลดเซฟ) ไม่เช็กคลัง</summary>
    public void SetLoadedChip(string chipId)
    {
        loadedChip = chipId ?? string.Empty;
        OnChanged?.Invoke();
    }

    private void Update()
    {
        if (isInvisible)
        {
            float remaining = invisibleUntil - Time.time;
            if (remaining <= 0f)
            {
                SetInvisible(false);
                SfxLibrary.Play("xgear_deactivate");
                HudToast.Show("Visible again");
            }
            else
            {
                HudToast.Show($"INVISIBLE  {remaining:0.0}s", 0.3f);
            }

            RefreshChargeBar();
        }

        if (pickup == null || pickup.CurrentSlot != HeldSlot.XGear)
            return;

        if (fireAction == null || !fireAction.WasPressedThisFrame())
            return;

        if (Time.timeScale <= 0f || (wheel != null && wheel.IsOpen) || (controller != null && controller.IsMovementLocked) || CommandLineUI.IsTyping)
            return;

        TryActivate();
    }

    public bool TryActivate()
    {
        if (string.IsNullOrEmpty(loadedChip))
        {
            SfxLibrary.Play("empty_click");
            HudToast.Show("No Active Chip — call one with its Command (Q)");
            return false;
        }

        if (charges <= 0 || isInvisible)
        {
            SfxLibrary.Play("empty_click");
            HudToast.Show("X-Gear depleted");
            return false;
        }

        charges--;

        if (loadedChip == ItemCatalog.ChipInvisible)
        {
            invisibleUntil = Time.time + invisibleDuration;
            SetInvisible(true);
        }

        SfxLibrary.Play("xgear_activate");
        RefreshChargeBar();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>ตั้งจำนวน Charge ตรงๆ (ใช้ตอนโหลดเซฟ)</summary>
    public void SetCharges(int value)
    {
        charges = Mathf.Clamp(value, 0, MaxCharges);
        RefreshChargeBar();
        OnChanged?.Invoke();
    }

    /// <summary>เติม Charge (X-Gear Cell) คืน false ถ้าเต็มแล้ว (ไอเทมจะไม่ถูกใช้)</summary>
    public bool AddCharges(int amount, out string reason)
    {
        reason = null;

        if (charges >= MaxCharges)
        {
            reason = "X-Gear charges full";
            return false;
        }

        charges = Mathf.Min(charges + Mathf.Max(1, amount), MaxCharges);
        RefreshChargeBar();
        OnChanged?.Invoke();
        return true;
    }

    private void SetInvisible(bool value)
    {
        if (isInvisible == value)
            return;

        isInvisible = value;
        PlayerStealth.SetInvisible(value);

        // บอกผู้เล่นด้วยภาพ: ตัวละครไม่ทอดเงาระหว่างล่องหน
        if (bodyRenderers == null)
            bodyRenderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] == null || bodyRenderers[i] is LineRenderer)
                continue;

            bodyRenderers[i].shadowCastingMode = value
                ? UnityEngine.Rendering.ShadowCastingMode.Off
                : UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    // ------------------------------------------------------------ ระบบติดตั้งชิปแบบเก่า (3 ช่อง) → คืนชิปเข้าคลัง

    private static readonly string[] OldLoadoutKeys = { "XGear_Passive", "XGear_Active", "XGear_Map" };

    // เซฟเก่าที่ติดตั้งชิปไว้: ตอนติดตั้งชิปถูกหักออกจากคลัง จึงต้องคืนกลับ (ทำครั้งเดียว แล้วลบค่าเดิมทิ้ง)
    private static void MigrateOldLoadout()
    {
        PersistentInventory inventory = PersistentInventory.Instance;
        if (inventory == null)
            return;

        for (int i = 0; i < OldLoadoutKeys.Length; i++)
        {
            string id = PlayerPrefs.GetString(OldLoadoutKeys[i], string.Empty);
            if (!string.IsNullOrEmpty(id))
            {
                inventory.Add(id, 1);
                CommandBook.LearnFromItem(id);
            }

            PlayerPrefs.DeleteKey(OldLoadoutKeys[i]);
        }
    }

    /// <summary>ลบค่าการติดตั้งแบบเก่า (เมนูรีเซ็ตเซฟใช้)</summary>
    public static void ResetOldLoadout()
    {
        for (int i = 0; i < OldLoadoutKeys.Length; i++)
            PlayerPrefs.DeleteKey(OldLoadoutKeys[i]);
    }

    // ------------------------------------------------------------ HUD: ขีดสีฟ้ามุมซ้ายบน

    private void FindOrBuildChargeBar()
    {
        // ใช้แถบ XN(Mana) ที่มีอยู่ใน MainHUD ถ้ามี
        Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].name == "XN(Mana)_Filled")
            {
                chargeFill = images[i];
                chargeRoot = images[i].transform.parent != null ? images[i].transform.parent.gameObject : images[i].gameObject;
                break;
            }
        }

        if (chargeFill != null)
            return;

        // Scene ที่ไม่มี MainHUD: สร้างขีดเองที่ตำแหน่งเดียวกัน
        Canvas canvas = UIBuilder.CreateCanvas("XGearHUD (Auto)", 20);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;
        canvas.transform.SetParent(transform, false);

        Image back = UIBuilder.Panel("XGearCharge", canvas.transform, new Color(0f, 0f, 0f, 0.5f), false);
        UIBuilder.Place(back.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(60f, -125f), new Vector2(78f, 12f));

        Image fill = UIBuilder.Panel("Fill", back.transform, chargeColor, false);
        UIBuilder.Stretch(fill.rectTransform, 1f, 1f, 1f, 1f);

        chargeRoot = back.gameObject;
        chargeFill = fill;
    }

    private void RefreshChargeBar()
    {
        if (chargeRoot == null || chargeFill == null)
            return;

        if (!chargeRoot.activeSelf)
            chargeRoot.SetActive(true);

        // ความยาวขีด = จำนวนครั้งที่เหลือ / สูงสุด, ระหว่างล่องหนครั้งที่กำลังใช้หดตามเวลาที่เหลือ
        float running = isInvisible ? Mathf.Clamp01((invisibleUntil - Time.time) / invisibleDuration) : 0f;
        float fraction = Mathf.Clamp01((charges + running) / MaxCharges);
        chargeFill.color = chargeColor;
        chargeFill.enabled = fraction > 0f;
        chargeFill.transform.localScale = new Vector3(fraction, 1f, 1f);
    }
}

/// <summary>ติดระบบเสริมให้ผู้เล่นตอนเริ่ม Scene (เรียกจาก TopDownPlayerController.Start)</summary>
public static class PlayerSystems
{
    public static void EnsureOn(TopDownPlayerController player)
    {
        if (player == null)
            return;

        GameObject go = player.gameObject;

        if (go.GetComponent<XGearSystem>() == null) go.AddComponent<XGearSystem>();
        if (go.GetComponent<CommandLineUI>() == null) go.AddComponent<CommandLineUI>();

        // Eye Wide อยู่บนวัตถุลูกชื่อ "EyeWide" ตัวเดียว: ถ้าใน Prefab/Scene วางไว้แล้ว (ปรับค่าใน Inspector ได้) ใช้ตัวนั้น
        // ถ้ายังไม่มีค่อยสร้างให้ จึงไม่มีทางเกิดสองตัวซ้อนกัน
        if (go.GetComponentInChildren<EyeWideController>(true) == null)
        {
            GameObject eyeWide = new GameObject("EyeWide");
            eyeWide.transform.SetParent(go.transform, false);
            eyeWide.AddComponent<EyeWideController>();
        }

        PauseMenuUI.EnsureInScene(player);
        UpgradeTerminal.EnsureInBase();
    }
}
