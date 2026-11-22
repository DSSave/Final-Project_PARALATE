using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// X-Gear: แขนกลที่ติดตัวผู้เล่นตั้งแต่เริ่มเกม มี 3 ช่อง (ตั้งค่าที่ Upgrade Corner ใน Base, เก็บใน XGearLoadout)
///   Passive : ใส่แล้วได้ผลอัตโนมัติตลอด (FleetFoot = ย่อเดินเร็วขึ้น 5%)
///   Active  : เปลี่ยนมาถือ X-Gear แล้วคลิกซ้ายเพื่อใช้ ใช้ได้ maxCharges ครั้งต่อด่าน (ขีดสีฟ้ามุมซ้ายบน)
///             เก็บ X-Gear Cell ในด่านเพื่อเติมทีละหน่วย (ไม่เกิน maxCharges)
///   Map     : ใส่ Map Data ของด่านไหน เข้าด่านนั้นจะมี Minimap ทั้งแผนที่ + ศัตรู
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
    public float fleetFootCrouchMultiplier = 1.05f;

    [Header("HUD")]
    public Color chargeColor = new Color(0.25f, 0.7f, 1f, 1f);

    private TopDownPlayerController controller;
    private PickupSystem pickup;
    private WeaponWheelUI wheel;
    private InputAction fireAction;

    private int charges;
    private bool usedThisMission;
    private float invisibleUntil;
    private bool isInvisible;

    private GameObject chargeRoot;
    private Graphic chargeFill;
    private Renderer[] bodyRenderers;

    public int Charges => charges;
    public int MaxCharges => Mathf.Max(1, maxCharges);
    public bool IsInvisible => isInvisible;

    private void Awake()
    {
        controller = GetComponent<TopDownPlayerController>();
        pickup = GetComponent<PickupSystem>();
        wheel = GetComponent<WeaponWheelUI>();

        PlayerInput input = GetComponent<PlayerInput>();
        if (input != null && input.actions != null)
            fireAction = input.actions.FindAction("Fire", false);
    }

    private void OnEnable()
    {
        XGearLoadout.OnChanged += ApplyLoadout;
    }

    private void Start()
    {
        FindOrBuildChargeBar();
        ApplyLoadout();
    }

    private void OnDisable()
    {
        XGearLoadout.OnChanged -= ApplyLoadout;
        SetInvisible(false);
    }

    // เรียกตอนเริ่มด่าน และทุกครั้งที่เปลี่ยนของในช่อง (ที่ Base)
    private void ApplyLoadout()
    {
        // Passive
        if (controller != null)
            controller.CrouchSpeedMultiplier = XGearLoadout.PassiveId == ItemCatalog.ChipFleetFoot ? fleetFootCrouchMultiplier : 1f;

        // Active: เริ่มด่านมาเต็ม maxCharges (คอมโพเนนต์นี้อยู่บนผู้เล่นของ Scene จึงรีเซ็ตเองเมื่อโหลดด่านใหม่)
        bool hasChip = !string.IsNullOrEmpty(XGearLoadout.ActiveId);
        if (!hasChip)
            charges = 0;
        else if (!usedThisMission)
            charges = MaxCharges;
        else
            charges = Mathf.Min(charges, MaxCharges);

        ApplyMap();
        RefreshChargeBar();
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

        if (Time.timeScale <= 0f || (wheel != null && wheel.IsOpen) || (controller != null && controller.IsMovementLocked))
            return;

        TryActivate();
    }

    public bool TryActivate()
    {
        string activeId = XGearLoadout.ActiveId;

        if (string.IsNullOrEmpty(activeId))
        {
            SfxLibrary.Play("empty_click");
            HudToast.Show("No Active Chip installed");
            return false;
        }

        if (charges <= 0 || isInvisible)
        {
            SfxLibrary.Play("empty_click");
            HudToast.Show("X-Gear depleted");
            return false;
        }

        charges--;
        usedThisMission = true;

        if (activeId == ItemCatalog.ChipInvisible)
        {
            invisibleUntil = Time.time + invisibleDuration;
            SetInvisible(true);
        }

        SfxLibrary.Play("xgear_activate");
        RefreshChargeBar();
        return true;
    }

    /// <summary>ตั้งจำนวน Charge ตรงๆ (ใช้ตอนโหลดเซฟ)</summary>
    public void SetCharges(int value)
    {
        usedThisMission = true;
        charges = string.IsNullOrEmpty(XGearLoadout.ActiveId) ? 0 : Mathf.Clamp(value, 0, MaxCharges);
        RefreshChargeBar();
    }

    /// <summary>เติม X-Gear (X-Gear Cell) คืน false ถ้าไม่มี Active Chip หรือเต็มแล้ว (ไอเทมจะไม่ถูกใช้)</summary>
    public bool AddCharges(int amount, out string reason)
    {
        reason = null;

        if (string.IsNullOrEmpty(XGearLoadout.ActiveId))
        {
            reason = "No Active Chip installed";
            return false;
        }

        if (charges >= MaxCharges)
        {
            reason = "X-Gear charges full";
            return false;
        }

        charges = Mathf.Min(charges + Mathf.Max(1, amount), MaxCharges);
        RefreshChargeBar();
        return true;
    }

    private void SetInvisible(bool value)
    {
        if (isInvisible == value)
            return;

        isInvisible = value;
        PlayerStealth.SetInvisible(value);

        // บอกผู้เล่นด้วยภาพ: ตัวละครไม่ทอดเงาและโปร่งแสงขึ้นระหว่างล่องหน
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

        bool hasChip = !string.IsNullOrEmpty(XGearLoadout.ActiveId);
        if (chargeRoot.activeSelf != hasChip)
            chargeRoot.SetActive(hasChip);

        if (!hasChip)
            return;

        // ความยาวขีด = จำนวนครั้งที่เหลือ / สูงสุด, ระหว่างล่องหนครั้งที่กำลังใช้หดตามเวลาที่เหลือ
        float running = isInvisible ? Mathf.Clamp01((invisibleUntil - Time.time) / invisibleDuration) : 0f;
        float fraction = Mathf.Clamp01((charges + running) / MaxCharges);
        chargeFill.color = chargeColor;
        chargeFill.enabled = fraction > 0f;
        chargeFill.transform.localScale = new Vector3(fraction, 1f, 1f);
    }

    // ------------------------------------------------------------ Map Data → Minimap ทั้งแผนที่

    private void ApplyMap()
    {
        MinimapManager manager = FindAnyObjectByType<MinimapManager>(FindObjectsInactive.Include);
        if (manager == null)
            return; // Scene นี้ไม่มี Minimap

        ItemDef map = ItemCatalog.Get(XGearLoadout.MapId);
        bool hasMap = map != null && map.category == ItemCategory.MapData &&
                      map.sceneName == SceneManager.GetActiveScene().name;

        // กลุ่ม "Map" ใน HUD (พ่อของ MinimapManager) เปิดเฉพาะเมื่อใส่ Map Data ของด่านนี้
        Transform mapRoot = manager.transform.parent;
        if (mapRoot != null && mapRoot.GetComponent<Canvas>() == null)
            mapRoot.gameObject.SetActive(hasMap);

        MinimapCameraFollow follow = FindAnyObjectByType<MinimapCameraFollow>(FindObjectsInactive.Include);
        Camera mapCamera = follow != null ? follow.GetComponent<Camera>() : null;
        if (mapCamera == null)
            return;

        // ประหยัดเฟรมเรต: ไม่ได้โชว์แผนที่ = ปิดกล้อง Minimap สนิท / โชว์อยู่ = เรนเดอร์ 10 ครั้งต่อวินาทีพอ
        MinimapRenderThrottle throttle = MinimapRenderThrottle.EnsureOn(mapCamera);
        throttle.SetVisible(hasMap);

        if (!hasMap)
            return;

        // ขอบเขตของด่าน = พื้นที่เดินได้ทั้งหมดของ NavMesh แล้วตั้งกล้อง Minimap ให้เห็นทั้งหมดจากด้านบน
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        if (triangulation.vertices == null || triangulation.vertices.Length == 0)
            return; // ไม่มี NavMesh: ใช้ Minimap แบบตามผู้เล่นตามเดิม

        Bounds bounds = new Bounds(triangulation.vertices[0], Vector3.zero);
        for (int i = 1; i < triangulation.vertices.Length; i++)
            bounds.Encapsulate(triangulation.vertices[i]);

        follow.enabled = false; // หยุดตามผู้เล่น

        float aspect = mapCamera.targetTexture != null
            ? (float)mapCamera.targetTexture.width / mapCamera.targetTexture.height
            : mapCamera.aspect;

        float height = Mathf.Max(follow.height + 5f, bounds.max.y + 15f);

        mapCamera.orthographic = true;
        mapCamera.orthographicSize = Mathf.Max(bounds.extents.z, bounds.extents.x / Mathf.Max(0.01f, aspect)) * 1.1f;
        mapCamera.farClipPlane = Mathf.Max(mapCamera.farClipPlane, height - bounds.min.y + 10f);
        mapCamera.transform.SetPositionAndRotation(
            new Vector3(bounds.center.x, height, bounds.center.z),
            Quaternion.Euler(90f, 0f, 0f));
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
