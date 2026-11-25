using UnityEngine;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using System;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(AudioSource))]
public class GunAction : MonoBehaviour
{
    [Header("Gun Settings")]
    public Transform firePoint;
    public float range = 100f;
    public float damage = 10f;
    public float fireRate = 0.1f;

    [Header("Weapon Type")]
    [Tooltip("ชนิดกระสุนที่ปืนนี้ใช้ (Auto = เดาจากชื่อ: sniper / usp,pistol / อื่นๆ = Rifle)")]
    public AmmoType ammoType = AmmoType.Auto;
    [Tooltip("ช่องอาวุธ (Auto = ปืนพกเป็น Secondary, อื่นๆ เป็น Primary)")]
    public WeaponSlotType slotType = WeaponSlotType.Auto;
    [Tooltip("ตัวคูณดาเมจเมื่อยิงโดนหัว (0 = ค่าเริ่มต้นตามชนิดปืน: Rifle x2, Pistol x1.5, Sniper x4)")]
    public float headshotMultiplier = 0f;
    [Tooltip("ซูมแล้วขึ้นกรอบกล้อง Sniper (ปืนชนิด Sniper ขึ้นให้อัตโนมัติอยู่แล้ว)")]
    public bool forceScopeOverlay = false;

    public enum AimStyle
    {
        Auto,              // Sniper = ซูมบุคคลที่หนึ่ง / ปืนอื่น = เล็งไหล่ (เฉพาะ Scene ที่มีกล้อง TPS)
        FirstPersonScope,  // คลิกขวา = ซูมบุคคลที่หนึ่งเสมอ
        OverShoulder       // คลิกขวา = เล็งไหล่แบบ RE4 (เฉพาะ Scene ที่มีกล้อง TPS ไม่งั้นใช้ซูมเดิม)
    }

    [Tooltip("วิธีเล็งเมื่อคลิกขวา: Auto = Sniper ซูม / USP, AK เล็งไหล่แบบ RE4 (ต้องมี PlayerTpsCameraRig ใน Scene)")]
    public AimStyle aimStyle = AimStyle.Auto;

    /// <summary>
    /// คลิกขวาปืนนี้ = เล็งไหล่ (PlayerTpsCameraRig คุม) แทนซูมบุคคลที่หนึ่ง
    /// Scene ที่ไม่มีกล้อง TPS (กล้องมุมตายตัว) ได้ false เสมอ = ทำงานแบบเดิมทุกอย่าง
    /// </summary>
    public bool UsesOverShoulderAim
    {
        get
        {
            PlayerTpsCameraRig rig = PlayerTpsCameraRig.Active;
            if (rig == null || movementController == null || rig.player != movementController)
                return false;

            switch (aimStyle)
            {
                case AimStyle.OverShoulder: return true;
                case AimStyle.FirstPersonScope: return false;
                default: return !UsesScopeOverlay; // Auto: Sniper ใช้ซูม
            }
        }
    }

    // เล็งไหล่แบบ RE4 ต้นฉบับ: ต้องคลิกขวาเล็งอยู่จึงยิงได้ (ปืนอื่น/Scene อื่นยิงได้ตามเดิม)
    private bool AimRuleAllowsFire()
    {
        if (!UsesOverShoulderAim)
            return true;

        PlayerTpsCameraRig rig = PlayerTpsCameraRig.Active;
        return !rig.requireAimToFire || rig.IsAimingWith(this);
    }

    [Header("Hit Detection")]
    [Tooltip("เลเยอร์ที่กระสุนชนได้ (ค่าเริ่มต้น = ทุกเลเยอร์ยกเว้น Ignore Raycast) " +
             "ถ้ามี Collider ล่องหนที่ขวางกระสุน ให้ย้ายไปเลเยอร์ Ignore Raycast หรือเอาเลเยอร์นั้นออกจากช่องนี้")]
    public LayerMask hitMask = ~(1 << 2);
    [Tooltip("เปิดเพื่อดู Console ว่ากระสุนแต่ละนัดชนอะไร (ใช้หาว่าอะไรขวางกระสุนอยู่)")]
    public bool logShotHits = false;

    [Header("Ammo & Reload")]
    public int magazineSize = 30;
    public float reloadDuration = 5f;
    [Range(0f, 1f)]
    public float reloadMovementSpeedMultiplier = 0.5f;
    public bool autoReloadWhenEmpty = true;

    /// <summary>
    /// ตั้งกระสุนในแม็กตรงๆ (PickupSystem คืนค่าที่จำไว้ตอนสลับปืนในช่อง Weapon กลับมา)
    /// ปืนเริ่มต้นด้วยแม็กเต็ม รีโหลด (R) จากกระสุนในกระเป๋าตามปกติ (ไม่มี Cooldown / ปืนไม่หายตอนหมดแม็กแล้ว)
    /// </summary>
    public void SetLoadedAmmo(int amount)
    {
        currentAmmo = Mathf.Clamp(amount, 0, magazineSize);
        UpdateAmmoUI();
    }

    [Header("Crosshair & UI")]
    public RectTransform crosshairUI;
    public event Action<int, int> OnAmmoChanged;
    public event Action<bool> OnReloadStatusChanged;
    public event Action<bool> OnHeldChanged;

    [Header("Debug Settings")]
    public bool isHeld = false; // สถานะการถือปืน
    public bool showDebugLine = true; // เปิด/ปิด debug line ของกระสุน

    [Header("Input Actions")]
    public InputActionReference fireAction;
    public InputActionReference pointerAction;
    public InputActionReference aimAction;
    public InputActionReference reloadAction;

    [Header("Scope Settings")]
    public bool enableScope = true; //เปิด/ปิด scope
    public float normalFieldOfView = 50f; //มุมกล้องตอนถือปืน
    public float scopedFieldOfView = 25f; //มุมกล้องตอน scope
    public bool hideCrosshairWhileScoped = false; //เปิด/ปิด crosshair
    [Header("Movement")]
    [Range(0f, 1f)]
    public float scopedMovementSpeedMultiplier = 0.6f;

    [Header("First Person Scope Camera")]

    public bool useFirstPersonScope = true; // ตอนซูมให้กล้องขยับไปมุมมองบุคคลที่หนึ่ง
    public Vector3 firstPersonCameraOffset = new Vector3(0f, 1.55f, 0.15f);
    public int firstPersonCameraPriority = 2000;
    public bool snapScopeCameraTransition = true; // ตัดเข้ากล้อง First Person ทันที
    public bool centerCrosshairInFirstPersonScope = true;
    public bool lockCursorInFirstPersonScope = true;
    public float firstPersonMouseSensitivity = 0.12f;
    public float minFirstPersonPitch = -70f;
    public float maxFirstPersonPitch = 70f;



    private LineRenderer laserLine;
    private float nextFireTime;

    [Header("Audio")]
    public AudioClip fireSound;
    public AudioClip reloadStartSound;   // เสียงตอนเริ่มรีโหลด (ดึงแม็ก/เสียบแม็ก)
    public AudioClip reloadFinishSound;  // เสียงตอนรีโหลดเสร็จ (ปิดแม็ก/ขึ้นลำ) - ใส่หรือไม่ใส่ก็ได้
    public AudioClip equipSound;         // เสียงตอนชักปืนขึ้นมาถือ
    [Range(0f, 1f)] public float fireVolume = 1f;

    [Range(0f, 1f)] public float reloadVolume = 1f;
    [Range(0f, 1f)] public float equipVolume = 1f;
    public bool randomizePitch = true;
    [Range(0f, 0.2f)] public float pitchVariance = 0.05f;
    private AudioSource audioSource;

    [SerializeField] private int currentAmmo;
    private bool isReloading;
    private Coroutine reloadCoroutine;
    private TopDownPlayerController movementController;
    private InputAction resolvedReloadAction;

    public bool IsReloading => isReloading;
    public int CurrentAmmo => currentAmmo;
    public int MagazineSize => magazineSize;

    private AmmoType cachedAmmoType = AmmoType.Auto;
    private bool lastShotHeadshot;

    public AmmoType ResolvedAmmoType
    {
        get
        {
            if (ammoType != AmmoType.Auto)
                return ammoType;

            if (cachedAmmoType == AmmoType.Auto)
            {
                string lower = name.ToLowerInvariant();
                cachedAmmoType = lower.Contains("sniper") ? AmmoType.Sniper
                    : (lower.Contains("usp") || lower.Contains("pistol")) ? AmmoType.Pistol
                    : AmmoType.Rifle;
            }

            return cachedAmmoType;
        }
    }

    public WeaponSlotType ResolvedSlot =>
        slotType != WeaponSlotType.Auto ? slotType
        : ResolvedAmmoType == AmmoType.Pistol ? WeaponSlotType.Secondary : WeaponSlotType.Primary;

    public string AmmoItemId => ItemCatalog.AmmoId(ResolvedAmmoType);
    public string UpgradeKey => WeaponUpgrades.KeyOf(ResolvedAmmoType);

    /// <summary>กระสุนสำรองในกระเป๋า (คลังถาวร) ของชนิดที่ปืนนี้ใช้</summary>
    public int ReserveAmmo => PersistentInventory.Instance != null ? PersistentInventory.Instance.GetAmount(AmmoItemId) : 0;

    public float EffectiveDamage => damage * WeaponUpgrades.DamageMultiplier(UpgradeKey);
    public float EffectiveReloadDuration => reloadDuration / WeaponUpgrades.ReloadSpeedMultiplier(UpgradeKey);

    public float HeadshotMultiplier
    {
        get
        {
            if (headshotMultiplier > 0f)
                return headshotMultiplier;

            switch (ResolvedAmmoType)
            {
                case AmmoType.Pistol: return 1.5f;
                case AmmoType.Sniper: return 4f;
                default: return 2f;
            }
        }
    }

    public bool UsesScopeOverlay => forceScopeOverlay || ResolvedAmmoType == AmmoType.Sniper;

    private Camera mainCam;


    private bool isScoped;
    private InputAction resolvedAimAction;
    private InputAction resolvedFireAction;
    private Transform ownerTransform;
    private PlayerNoiseEmitter playerNoiseEmitter;
    private CinemachineCamera firstPersonScopeCamera;
    private GameObject firstPersonScopeCameraObject;
    private CinemachineBrain cinemachineBrain;
    private CinemachineBlendDefinition savedBrainBlend;
    private Coroutine restoreBrainBlendCoroutine;
    private bool hasSavedBrainBlend;
    private bool isFirstPersonScopeCameraActive;
    private float firstPersonYaw;
    private float firstPersonPitch;
    private bool savedCursorVisible;
    private CursorLockMode savedCursorLockState;
    private bool hasSavedCursorState;

    private void Awake()
    {
        laserLine = GetComponent<LineRenderer>();
        laserLine.enabled = false;
        mainCam = Camera.main;
        if (mainCam != null)
            cinemachineBrain = mainCam.GetComponent<CinemachineBrain>();
        movementController = GetComponentInParent<TopDownPlayerController>();
        ownerTransform = movementController != null ? movementController.transform : transform.root;
        ResolveNoiseEmitter();
        EnsureFirstPersonScopeCamera();

        audioSource = GetComponent<AudioSource>(); // เพิ่มบรรทัดนี้
        audioSource.playOnAwake = false;            // กันไม่ให้เล่นเองตอนเริ่มเกม
        AudioManager.RouteToSfx(audioSource);

        if (mainCam != null)
            normalFieldOfView = mainCam.fieldOfView;

        currentAmmo = magazineSize;
    }
    private void OnDisable()
    {
        CancelReload();
        SetScoped(false, true);
        SetMovementAimCameraOverride(false);
        RestoreCinemachineBrainBlend();
        SetCrosshairVisible(false);
    }

    private void OnDestroy()
    {
        PersistentInventory.OnChanged -= UpdateAmmoUI;
        RestoreCinemachineBrainBlend();

        if (firstPersonScopeCameraObject != null)
            Destroy(firstPersonScopeCameraObject);
    }

    public void SetHeld(bool held)
    {
        isHeld = held;
        movementController = GetComponentInParent<TopDownPlayerController>();
        ownerTransform = movementController != null ? movementController.transform : transform.root;
        ResolveNoiseEmitter();
        EnsureFirstPersonScopeCamera();
        SetCrosshairVisible(held && isScoped && !hideCrosshairWhileScoped);
        OnHeldChanged?.Invoke(held);

        // กระสุนสำรองอยู่ในคลัง: เก็บกล่องกระสุน/คราฟต์แล้วตัวเลขบน HUD ต้องอัปเดตตาม
        PersistentInventory.OnChanged -= UpdateAmmoUI;
        if (held)
            PersistentInventory.OnChanged += UpdateAmmoUI;

        if (held)
        {
            UpdateAmmoUI();
            PlaySound(equipSound, equipVolume, "weapon_equip");
        }

        if (!held)
        {
            CancelReload();
            SetScoped(false, true);
        }
    }
    void Update()
    {
        HandleScope();
        HandleCrosshair();
        HandleReload();

        bool aimAllowsFire = !isHeld || AimRuleAllowsFire();

        if (isHeld && !isReloading && currentAmmo > 0 && !IsOwnerLocked() && aimAllowsFire
            && IsFirePressed() && Time.time > nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Shoot();
        }
        else if (isHeld && !isReloading && currentAmmo <= 0 && !IsOwnerLocked() && aimAllowsFire && WasFirePressedThisFrame())
        {
            // แม็กว่าง: เสียงไกเปล่า + บอกผู้เล่นว่าต้องทำอะไร
            SfxLibrary.Play("empty_click");
            HudToast.Show(ReserveAmmo > 0 ? "Press R to reload" : "Out of " + ItemCatalog.NameOf(AmmoItemId));
        }
    }

    private bool WasFirePressedThisFrame()
    {
        if (fireAction != null)
            return fireAction.action.WasPressedThisFrame();

        return resolvedFireAction != null && resolvedFireAction.WasPressedThisFrame();
    }

    // ผู้เล่นถูกล็อก (อ่านโน้ต, รัดคอ, ตาย ฯลฯ) ห้ามยิง/เล็ง/รีโหลด
    private bool IsOwnerLocked()
    {
        return movementController != null && movementController.IsMovementLocked;
    }

    private bool IsFirePressed()
    {
        if (fireAction != null)
            return fireAction.action.IsPressed();

        if (resolvedFireAction == null)
        {
            PlayerInput playerInput = GetComponentInParent<PlayerInput>();
            if (playerInput != null)
                resolvedFireAction = playerInput.actions["Fire"];
        }

        return resolvedFireAction != null && resolvedFireAction.IsPressed();
    }

    private void LateUpdate()
    {
        HandleFirstPersonScopeLook();
        UpdateFirstPersonScopeCamera();
    }

    void HandleReload()
    {
        if (!isHeld || isReloading || IsOwnerLocked())
            return;

        if (currentAmmo >= magazineSize)
            return;

        ResolveReloadAction();

        if (resolvedReloadAction != null && resolvedReloadAction.WasPressedThisFrame())
            StartReload();
    }

    void ResolveReloadAction()
    {
        if (reloadAction != null)
        {
            resolvedReloadAction = reloadAction.action;
            return;
        }

        if (resolvedReloadAction != null)
            return;

        PlayerInput playerInput = GetComponentInParent<PlayerInput>();
        if (playerInput == null)
            return;

        resolvedReloadAction = playerInput.actions["Reload"];
    }

    public void StartReload()
    {
        if (!isHeld || isReloading || currentAmmo >= magazineSize)
            return;

        // ไม่มีกระสุนชนิดนี้ในกระเป๋า = รีโหลดไม่ได้
        if (ReserveAmmo <= 0)
        {
            SfxLibrary.Play("empty_click");
            HudToast.Show("No " + ItemCatalog.NameOf(AmmoItemId));
            return;
        }

        if (reloadCoroutine != null)
            StopCoroutine(reloadCoroutine);

        reloadCoroutine = StartCoroutine(ReloadRoutine());
    }

    void CancelReload()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }

        if (!isReloading)
            return;

        isReloading = false;
        SetReloadMovementSpeed(false);
        OnReloadStatusChanged?.Invoke(false);
    }

    IEnumerator ReloadRoutine()
    {
        isReloading = true;
        OnReloadStatusChanged?.Invoke(true);
        SetReloadMovementSpeed(true);
        PlaySound(reloadStartSound, reloadVolume, "reload_start");

        yield return new WaitForSeconds(EffectiveReloadDuration);

        // เติมแม็กจากกระสุนในกระเป๋า: ได้เท่าที่มี (อาจไม่เต็มแม็ก)
        int taken = Mathf.Min(magazineSize - currentAmmo, ReserveAmmo);
        currentAmmo += taken;
        isReloading = false;
        SetReloadMovementSpeed(false);
        reloadCoroutine = null;

        if (taken > 0 && PersistentInventory.Instance != null)
            PersistentInventory.Instance.Add(AmmoItemId, -taken);

        PlaySound(reloadFinishSound, reloadVolume, "reload_finish");

        OnReloadStatusChanged?.Invoke(false);
        UpdateAmmoUI();
    }

    void SetReloadMovementSpeed(bool reloading)
    {
        if (movementController == null)
            movementController = GetComponentInParent<TopDownPlayerController>();

        if (movementController == null)
            return;

        movementController.SetMovementSpeedMultiplier(
            reloading ? reloadMovementSpeedMultiplier : 1f);

        movementController.SetSprintLocked(reloading);
    }

    // ค่าที่สองคือ "กระสุนสำรองในกระเป๋า" (เดิมเป็นขนาดแม็ก) HUD แสดงเป็น ในแม็ก / สำรอง
    void UpdateAmmoUI()
    {
        OnAmmoChanged?.Invoke(currentAmmo, ReserveAmmo);
    }
    // ฟังก์ชันนี้ใช้จัดการ UI เป้าเล็งและเคอร์เซอร์เมาส์
    void HandleCrosshair()
    {
        // เล็งไหล่: PlayerTpsCameraRig แสดงเป้า (จุดที่กระสุนจะไปโดน) เอง
        if (isHeld && UsesOverShoulderAim)
        {
            SetCrosshairVisible(false);
            return;
        }

        if (crosshairUI != null)
        {
            // ตอนถือปืนปกติจะไม่มีเป้าเล็ง เป้าเล็งจะแสดงเฉพาะตอนซูมเท่านั้น
            SetCrosshairVisible(isHeld && isScoped && !hideCrosshairWhileScoped);

            if (isHeld && isScoped)
            {
                // ในโหมด FPS crosshair อยู่กลางจอ และเมาส์ใช้หมุนกล้อง
                crosshairUI.position = GetCrosshairScreenPosition();

                // ซ่อนเคอร์เซอร์เมาส์ของ Windows (เอาออกได้ถ้าไม่ชอบ)
                Cursor.visible = false;
            }
            else
            {
            }
        }
    }
    // ฟังก์ชันนี้ใช้จัดการการเล็ง (Scope) ของปืน
    void HandleScope()
    {
        ResolveAimAction();

        // CanAim = false ตอนพิงกำแพง: คลิกขวาไม่ทำอะไร (ไม่ซูม ไม่หันตามเมาส์)
        bool canAim = movementController == null || movementController.CanAim;
        bool shouldScope = enableScope && isHeld && canAim && !IsOwnerLocked() && resolvedAimAction != null && resolvedAimAction.IsPressed()
                           && !UsesOverShoulderAim; // เล็งไหล่ใช้กล้องของ PlayerTpsCameraRig ไม่ซูมบุคคลที่หนึ่ง
        SetScoped(shouldScope, false);
    }
    // ฟังก์ชันนี้ใช้เพื่อหาค่า InputAction ของการเล็ง (Scope) จาก InputActionReference หรือจาก PlayerInput
    void ResolveAimAction()
    {
        if (aimAction != null)
        {
            resolvedAimAction = aimAction.action;
            return;
        }

        if (resolvedAimAction != null)
            return;

        PlayerInput playerInput = GetComponentInParent<PlayerInput>();
        if (playerInput == null)
            return;

        resolvedAimAction = playerInput.actions["Aiming"];
    }

    void SetScoped(bool scoped, bool instant)
    {
        if (useFirstPersonScope)
        {
            if (isScoped == scoped)
                return;

            isScoped = scoped;
            SetScopedMovementSpeed(scoped);
            SetFirstPersonScopeCameraActive(scoped);
            return;
        }

        if (isFirstPersonScopeCameraActive)
            SetFirstPersonScopeCameraActive(false);

        SetMovementAimCameraOverride(false);
        RestoreFirstPersonCursorState();
    }
    void SetScopedMovementSpeed(bool scoped)
    {
        if (movementController == null)
            movementController = GetComponentInParent<TopDownPlayerController>();

        if (movementController != null)
            movementController.SetMovementSpeedMultiplier(
                scoped ? scopedMovementSpeedMultiplier : 1f);
    }

    // ฟังก์ชันนี้ใช้เปิด/ปิด UI เป้าเล็ง
    void SetCrosshairVisible(bool visible)
    {
        if (crosshairUI != null && crosshairUI.gameObject.activeSelf != visible)
            crosshairUI.gameObject.SetActive(visible);
    }
    void Shoot()
    {
        if (currentAmmo <= 0 || isReloading)
            return;

        currentAmmo--;
        UpdateAmmoUI();

        // เล่นเสียงยิงปืนและปล่อยเสียงให้ AI ได้ยิน
        PlaySound(fireSound, fireVolume, "shot_" + UpgradeKey.ToLowerInvariant());
        PlayerNoiseEmitter noiseEmitter = ResolveNoiseEmitter();
        if (noiseEmitter != null)
            noiseEmitter.EmitShooting(firePoint.position);

        if (showDebugLine) StartCoroutine(ShotEffect());

        laserLine.SetPosition(0, firePoint.position);

        Vector3 shootDirection;
        PlayerTpsCameraRig rig = PlayerTpsCameraRig.Active;
        if (isScoped)
            shootDirection = GetScopedShootDirection();
        else if (rig != null && rig.IsAimingWith(this) && rig.TryGetShotDirection(firePoint.position, out Vector3 aimDirection))
            shootDirection = aimDirection; // เล็งไหล่: ยิงจากปากกระบอกไปยังจุดกลางจอ (จุดเดียวกับเป้าที่เห็น)
        else
            shootDirection = GetForwardShootDirection();

        if (TryRaycastShot(firePoint.position, shootDirection, out RaycastHit hit))
        {
            laserLine.SetPosition(1, hit.point);

            if (logShotHits)
                Debug.Log($"[{name}] กระสุนชน '{hit.collider.name}' (layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}) " +
                          $"ที่ระยะ {hit.distance:0.0} ม.", hit.collider);

            // ใช้ GetComponentInParent เหมือนมีด เผื่อ collider อยู่บน child ของศัตรู
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                // Headshot: กระสุนผ่าน HeadHitbox ของศัตรูตัวเดียวกัน ตัวคูณต่างกันตามปืน
                float dealt = EffectiveDamage * (lastShotHeadshot ? HeadshotMultiplier : 1f);
                enemy.TakeDamage(dealt, lastShotHeadshot);
            }
            else
            {
                // ของที่ยิงพังได้ (กล้องวงจรปิด ฯลฯ)
                IShootable shootable = hit.collider.GetComponentInParent<IShootable>();
                if (shootable != null)
                    shootable.TakeShot(EffectiveDamage, hit.point);

                if (noiseEmitter != null)
                    noiseEmitter.EmitBulletHitWall(hit.point);
            }

        }
        else
        {
            laserLine.SetPosition(1, firePoint.position + (shootDirection * range));
        }

        if (currentAmmo <= 0 && autoReloadWhenEmpty)
            StartReload();
    }

    private static readonly RaycastHit[] shotHits = new RaycastHit[64];

    // ยิง Raycast หาสิ่งแรกที่ขวางกระสุน โดยข้าม Trigger, Collider ของตัวผู้เล่นเอง และตัวปืนเอง
    // (เดิมสิ่งแรกที่ชนถือเป็นเป้าหมายเลย ถ้ามี Collider ของผู้เล่นอยู่ข้างหน้า กระสุนจะหยุดตรงนั้น)
    private bool TryRaycastShot(Vector3 origin, Vector3 direction, out RaycastHit result)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, shotHits, range, hitMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(shotHits, 0, count, RaycastHitDistanceComparer.Instance);

        Transform ownerRoot = ownerTransform != null
            ? ownerTransform
            : (movementController != null ? movementController.transform : null);

        for (int i = 0; i < count; i++)
        {
            Collider collider = shotHits[i].collider;

            bool isOwn = collider.transform.IsChildOf(transform) ||
                         (ownerRoot != null && collider.transform.IsChildOf(ownerRoot));

            if (isOwn)
            {
                if (logShotHits)
                    Debug.Log($"[{name}] ข้าม Collider ของตัวเอง '{collider.name}'", collider);

                continue;
            }

            result = shotHits[i];

            // ลำตัว (Capsule) มักถูกชนก่อนหัว จึงไล่ดูจุดชนถัดไปบนเส้นเดียวกันว่าผ่าน HeadHitbox ของศัตรูตัวนี้ไหม
            lastShotHeadshot = false;
            EnemyHealth hitEnemy = collider.GetComponentInParent<EnemyHealth>();
            if (hitEnemy != null)
            {
                for (int j = i; j < count; j++)
                {
                    HeadHitbox head = shotHits[j].collider.GetComponent<HeadHitbox>();
                    if (head != null && head.owner == hitEnemy)
                    {
                        lastShotHeadshot = true;
                        break;
                    }
                }
            }

            return true;
        }

        lastShotHeadshot = false;
        result = default;
        return false;
    }

    private PlayerNoiseEmitter ResolveNoiseEmitter()
    {
        Transform sourceTransform = ownerTransform != null ? ownerTransform : transform.root;

        if (sourceTransform == null)
            return null;

        if (playerNoiseEmitter == null || playerNoiseEmitter.gameObject != sourceTransform.gameObject)
        {
            playerNoiseEmitter = sourceTransform.GetComponent<PlayerNoiseEmitter>();
            if (playerNoiseEmitter == null)
                playerNoiseEmitter = sourceTransform.gameObject.AddComponent<PlayerNoiseEmitter>();
        }

        return playerNoiseEmitter;
    }

    private Vector3 GetForwardShootDirection()
    {
        Transform facingTransform = ownerTransform != null ? ownerTransform : firePoint;
        Vector3 direction = facingTransform.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = firePoint.forward;

        return direction.normalized;
    }

    private Vector3 GetScopedShootDirection()
    {
        if (mainCam == null)
            mainCam = Camera.main;

        if (mainCam == null)
            return firePoint.forward;

        Ray aimRay = GetScopedAimRay();
        Vector3 aimPoint = aimRay.GetPoint(range);

        if (Physics.Raycast(aimRay, out RaycastHit cameraHit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            aimPoint = cameraHit.point;

        Vector3 direction = aimPoint - firePoint.position;

        if (direction.sqrMagnitude < 0.001f)
            direction = aimRay.direction;

        return direction.normalized;
    }

    private Ray GetScopedAimRay()
    {
        if (useFirstPersonScope && firstPersonScopeCameraObject != null)
        {
            Vector2 screenPosition = GetCrosshairScreenPosition();
            float safeScreenHeight = Mathf.Max(1f, Screen.height);
            float safeScreenWidth = Mathf.Max(1f, Screen.width);
            float halfVerticalFov = scopedFieldOfView * Mathf.Deg2Rad * 0.5f;
            float halfHeight = Mathf.Tan(halfVerticalFov);
            float halfWidth = halfHeight * (safeScreenWidth / safeScreenHeight);

            float normalizedX = (screenPosition.x / safeScreenWidth - 0.5f) * 2f;
            float normalizedY = (screenPosition.y / safeScreenHeight - 0.5f) * 2f;
            Vector3 localDirection = new Vector3(normalizedX * halfWidth, normalizedY * halfHeight, 1f).normalized;
            Vector3 worldDirection = firstPersonScopeCameraObject.transform.TransformDirection(localDirection);

            return new Ray(firstPersonScopeCameraObject.transform.position, worldDirection);
        }

        if (mainCam == null)
            mainCam = Camera.main;

        if (mainCam != null)
            return mainCam.ScreenPointToRay(GetCrosshairScreenPosition());

        return new Ray(firePoint.position, firePoint.forward);
    }

    private Vector2 GetCrosshairScreenPosition()
    {
        if (isScoped && useFirstPersonScope && centerCrosshairInFirstPersonScope)
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        if (pointerAction == null || pointerAction.action == null)
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Vector2 pointerPosition = pointerAction.action.ReadValue<Vector2>();
        return new Vector2(
            Mathf.Clamp(pointerPosition.x, 0f, Screen.width),
            Mathf.Clamp(pointerPosition.y, 0f, Screen.height));
    }

    private void EnsureFirstPersonScopeCamera()
    {
        if (!useFirstPersonScope || ownerTransform == null || firstPersonScopeCamera != null)
            return;

        firstPersonScopeCameraObject = new GameObject($"{name}_FirstPersonScopeCamera");
        firstPersonScopeCameraObject.transform.SetParent(ownerTransform, false);
        firstPersonScopeCamera = firstPersonScopeCameraObject.AddComponent<CinemachineCamera>();
        firstPersonScopeCamera.Priority.Value = 0;
        firstPersonScopeCamera.Lens.FieldOfView = scopedFieldOfView;
        UpdateFirstPersonScopeCamera();
    }

    private void SetFirstPersonScopeCameraActive(bool active)
    {
        if (isFirstPersonScopeCameraActive == active)
        {
            if (firstPersonScopeCamera != null)
                firstPersonScopeCamera.Priority.Value = active ? firstPersonCameraPriority : 0;

            return;
        }

        if (active)
        {
            EnsureFirstPersonScopeCamera();
            PrepareFirstPersonScopeCameraPose();
            ApplyInstantCinemachineCut();
        }
        else
        {
            ApplyInstantCinemachineCut();
        }

        if (firstPersonScopeCamera == null)
            return;

        isFirstPersonScopeCameraActive = active;
        firstPersonScopeCamera.Priority.Value = active ? firstPersonCameraPriority : 0;
        firstPersonScopeCamera.Lens.FieldOfView = scopedFieldOfView;
        SetMovementAimCameraOverride(active);

        // ปืน Sniper: ขึ้นกรอบกล้องเล็งตอนซูม
        if (UsesScopeOverlay)
        {
            ScopeOverlayUI.SetVisible(active);
            SfxLibrary.Play(active ? "scope_in" : "scope_out");
        }

        if (!active)
        {
            RestoreFirstPersonCursorState();
            StartRestoreBrainBlendNextFrame();
        }
    }

    private void UpdateFirstPersonScopeCamera()
    {
        if (firstPersonScopeCamera == null || ownerTransform == null)
            return;

        Quaternion cameraRotation = Quaternion.Euler(firstPersonPitch, firstPersonYaw, 0f);

        firstPersonScopeCameraObject.transform.position = ownerTransform.position + cameraRotation * firstPersonCameraOffset;
        firstPersonScopeCameraObject.transform.rotation = cameraRotation;
        firstPersonScopeCamera.Lens.FieldOfView = scopedFieldOfView;
    }

    private void PrepareFirstPersonScopeCameraPose()
    {
        PlayerTpsCameraRig rig = PlayerTpsCameraRig.Active;
        if (mainCam == null)
            mainCam = Camera.main;

        if (rig != null && movementController != null && rig.player == movementController && mainCam != null)
        {
            // กล้อง TPS: หันซ้าย/ขวาตามทิศที่กล้อง (เมาส์) มองอยู่ แต่ก้ม/เงยเริ่มที่ระดับสายตาเสมอ (มองตรงระดับตัว–หัว)
            // ตัวละครหันตามในเฟรมถัดไป (HandleFirstPersonScopeLook) ระหว่างซูมยังก้ม/เงยด้วยเมาส์ได้ตามปกติ
            Vector3 forward = mainCam.transform.forward;
            firstPersonYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            firstPersonPitch = 0f;
        }
        else
        {
            // กล้องมุมตายตัว: ซูมไปทางที่ตัวละครหันอยู่ (แบบเดิม)
            Quaternion startRotation = ownerTransform != null ? ownerTransform.rotation : transform.rotation;
            Vector3 euler = startRotation.eulerAngles;
            firstPersonYaw = euler.y;
            firstPersonPitch = NormalizePitch(euler.x);
        }

        SaveAndApplyFirstPersonCursorState();
        UpdateFirstPersonScopeCamera();
    }

    /// <summary>มุมที่กล้องซูมบุคคลที่หนึ่งมองอยู่ (false = ไม่ได้ซูม) กล้อง TPS ใช้ตั้งมุมต่อหลังเลิกซูม</summary>
    public bool TryGetScopeLook(out float yaw, out float pitch)
    {
        yaw = firstPersonYaw;
        pitch = firstPersonPitch;
        return isFirstPersonScopeCameraActive;
    }

    private float NormalizePitch(float pitch)
    {
        return pitch > 180f ? pitch - 360f : pitch;
    }

    private void HandleFirstPersonScopeLook()
    {
        if (!isScoped || !useFirstPersonScope || firstPersonScopeCameraObject == null)
            return;

        // CameraLook ใน PlayerInput.inputactions (เมาส์ขยับ / right stick) ค่าที่ได้เป็นองศาแล้ว
        Vector2 lookDelta = PlayerControls.ReadLook(firstPersonMouseSensitivity, 90f);
        firstPersonYaw += lookDelta.x;
        firstPersonPitch = Mathf.Clamp(
            firstPersonPitch - lookDelta.y,
            minFirstPersonPitch,
            maxFirstPersonPitch);

        if (ownerTransform != null)
            ownerTransform.rotation = Quaternion.Euler(0f, firstPersonYaw, 0f);
    }

    private void SaveAndApplyFirstPersonCursorState()
    {
        if (!lockCursorInFirstPersonScope || hasSavedCursorState)
            return;

        savedCursorVisible = Cursor.visible;
        savedCursorLockState = Cursor.lockState;
        hasSavedCursorState = true;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void RestoreFirstPersonCursorState()
    {
        if (!hasSavedCursorState)
            return;

        Cursor.visible = savedCursorVisible;
        Cursor.lockState = savedCursorLockState;
        hasSavedCursorState = false;
    }

    private void ApplyInstantCinemachineCut()
    {
        if (!snapScopeCameraTransition)
            return;

        if (cinemachineBrain == null && mainCam != null)
            cinemachineBrain = mainCam.GetComponent<CinemachineBrain>();

        if (cinemachineBrain == null)
            return;

        if (restoreBrainBlendCoroutine != null)
        {
            StopCoroutine(restoreBrainBlendCoroutine);
            restoreBrainBlendCoroutine = null;
        }

        if (!hasSavedBrainBlend)
        {
            savedBrainBlend = cinemachineBrain.DefaultBlend;
            hasSavedBrainBlend = true;
        }

        cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        cinemachineBrain.ActiveBlend = null;
    }

    private void StartRestoreBrainBlendNextFrame()
    {
        if (!snapScopeCameraTransition || !hasSavedBrainBlend || cinemachineBrain == null)
            return;

        if (restoreBrainBlendCoroutine != null)
            StopCoroutine(restoreBrainBlendCoroutine);

        restoreBrainBlendCoroutine = StartCoroutine(RestoreBrainBlendNextFrame());
    }

    private IEnumerator RestoreBrainBlendNextFrame()
    {
        yield return new WaitForEndOfFrame();
        RestoreCinemachineBrainBlend();
        restoreBrainBlendCoroutine = null;
    }

    private void RestoreCinemachineBrainBlend()
    {
        if (!hasSavedBrainBlend || cinemachineBrain == null)
            return;

        cinemachineBrain.DefaultBlend = savedBrainBlend;
        hasSavedBrainBlend = false;
    }

    private void SetMovementAimCameraOverride(bool active)
    {
        if (movementController == null)
            movementController = GetComponentInParent<TopDownPlayerController>();

        if (movementController == null)
            return;

        Transform aimCameraTransform = active && firstPersonScopeCameraObject != null
            ? firstPersonScopeCameraObject.transform
            : null;

        movementController.SetAimCameraOverride(aimCameraTransform, scopedFieldOfView, active);
    }

    // ฟังก์ชันนี้ใช้แสดงเอฟเฟกต์การยิงกระสุน (เปิด LineRenderer ชั่วคราว)
    private IEnumerator ShotEffect()
    {
        laserLine.enabled = true;
        yield return new WaitForSeconds(0.05f);
        laserLine.enabled = false;
    }

    // fallbackName: ถ้าปืนไม่ได้ใส่คลิปไว้ใน Inspector จะใช้เสียงจาก SfxLibrary (Resources/Audio/SFX) แทน
    private void PlaySound(AudioClip clip, float volume, string fallbackName = null)
    {
        if (clip == null && fallbackName != null)
            clip = SfxLibrary.Pick(fallbackName);

        if (clip == null || audioSource == null)
            return;

        if (randomizePitch)
            audioSource.pitch = 1f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
        else
            audioSource.pitch = 1f;

        audioSource.PlayOneShot(clip, volume * SfxLibrary.GainOf(clip));
    }
}
