using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// กล้อง TPS ของด่าน (พัฒนาต่อจาก ThirdPersonFollowCamera ของ Base แต่ใช้ Cinemachine ทั้งหมด จึงปรับใน Inspector ได้):
///
/// 1) มุมมองปกติ (Normal Camera) — ผู้เล่นอยู่กลางจอแบบ Persona 3 Reload / MGS3
///    CinemachineCamera + OrbitalFollow (โคจรรอบผู้เล่น) + RotationComposer (เล็งผู้เล่นไว้กลางจอ) + Deoccluder (กันทะลุกำแพง)
///    สคริปต์นี้อ่านเมาส์เองแล้วเขียนค่าลงแกน Horizontal/Vertical ของ OrbitalFollow (ไม่ใช้ InputAxisController)
///    เพื่อหยุดหมุนได้ตอนพิงกำแพง / อยู่ในท่อ / เปิดเมนู / Eye Wide / เล็ง
///    ผู้เล่นเดินตามทิศกล้องเสมอ (TopDownPlayerController.SetForceCameraRelative)
///
/// 2) เล็งไหล่แบบ RE4 ต้นฉบับ (Aim Camera) — ถือปืนที่ AimStyle = OverShoulder (USP / AK) แล้วคลิกขวาค้าง:
///    ตัวละครหยุดเดิน เมาส์ซ้าย/ขวาหมุนตัวละคร เมาส์ขึ้น/ลงก้มเงยปืน กล้องอยู่หลังไหล่ขวา
///    CinemachineCamera + ThirdPersonFollow + RotateWithFollowTarget ตาม Aim Pivot (สคริปต์หมุน Pivot ตามมุมเล็ง)
///    เป้า (จุด) แสดง "ตำแหน่งที่กระสุนจะไปโดนจริง" = ยิงเส้นจากปากกระบอกไปยังจุดกลางจอ แล้วดูว่าชนอะไรก่อน
///    Sniper ยังใช้กล้องซูมบุคคลที่หนึ่งเดิมของ GunAction
///
/// ลำดับ Priority (สูงกว่าชนะ): กล้องห้อง 20–900 &lt; Normal 950 &lt; WallHug 1000 &lt; Vent 1500 &lt; Aim 1800 &lt; Eye Wide 1900 &lt; Sniper Scope 2000
/// </summary>
[DefaultExecutionOrder(-50)] // อัปเดตก่อน GunAction: ปืนรู้ทันทีว่ากำลังเล็งอยู่หรือไม่ และยิงไปทางไหน
public class PlayerTpsCameraRig : MonoBehaviour
{
    /// <summary>กล้อง TPS ที่ทำงานอยู่ใน Scene นี้ (null = Scene นี้ใช้กล้องมุมตายตัวแบบเดิม)</summary>
    public static PlayerTpsCameraRig Active { get; private set; }

    [Header("ผู้เล่น")]
    public TopDownPlayerController player;

    [Header("กล้องปกติ (ผู้เล่นกลางจอ)")]
    [Tooltip("CinemachineCamera ที่มี OrbitalFollow + RotationComposer (ปรับระยะ/ความสูง/ความนุ่มได้ที่ตัวกล้อง)")]
    public CinemachineCamera normalCamera;
    [Tooltip("ต้องสูงกว่ากล้องห้อง (สูงสุด 900) แต่ต่ำกว่า WallHug (1000) / Vent (1500)")]
    public int normalPriority = 950;
    [Tooltip("องศาต่อการขยับเมาส์ 1 พิกเซล")]
    public float mouseSensitivity = 0.12f;
    [Tooltip("องศาต่อวินาทีเมื่อใช้ก้านขวาของจอย")]
    public float gamepadSensitivity = 140f;
    public bool invertY = false;
    [Tooltip("มุมก้มของกล้องตอนเริ่มฉาก (องศา)")]
    public float startPitch = 12f;
    [Tooltip("ออกจากโหมดเล็งแล้วให้กล้องกลับมาอยู่ข้างหลังตัวละคร")]
    public bool recenterBehindAfterAim = true;

    [Header("เล็งไหล่แบบ RE4")]
    [Tooltip("CinemachineCamera ที่มี ThirdPersonFollow + RotateWithFollowTarget (ปรับ Shoulder Offset / ระยะได้ที่ตัวกล้อง)")]
    public CinemachineCamera aimCamera;
    [Tooltip("จุดหมุนกล้องเล็ง (ระดับไหล่) สคริปต์หมุนจุดนี้ตามมุมเล็ง")]
    public Transform aimPivot;
    public int aimPriority = 1800;
    public float aimMouseSensitivity = 0.08f;
    public float aimGamepadSensitivity = 90f;
    [Range(-89f, 0f)] public float minAimPitch = -55f;
    [Range(0f, 89f)] public float maxAimPitch = 60f;
    [Tooltip("ความเร็วหันตัวตอนเริ่มเล็ง (องศา/วินาที) ระหว่างเล็งตัวละครหันตามเมาส์ทันที")]
    public float aimTurnSpeed = 900f;
    [Tooltip("RE4 ต้นฉบับ: เล็งแล้วเดินไม่ได้")]
    public bool freezeMovementWhileAiming = true;
    [Tooltip("RE4 ต้นฉบับ: ต้องคลิกขวาเล็งก่อนจึงยิงได้")]
    public bool requireAimToFire = true;
    [Tooltip("เริ่มเล็งไปทางที่กล้องหันอยู่ (ปิด = เล็งไปทางที่ตัวละครหันอยู่)")]
    public bool aimFromCameraDirection = true;
    [Tooltip("เวลาเปลี่ยนจากกล้องปกติเป็นกล้องเล็ง (วินาที) 0 = ตัดทันที")]
    [Min(0f)] public float aimBlendTime = 0.18f;

    [Header("เป้าเล็ง (จุดที่กระสุนจะไปโดน)")]
    [Tooltip("รูปเป้าที่จะแสดง (ว่าง = สร้างจุดแดงให้อัตโนมัติ) ใช้ Canvas แบบ Screen Space - Overlay")]
    public RectTransform crosshair;
    [Tooltip("ระยะไกลสุดที่ใช้หาจุดเล็ง")]
    public float aimRange = 100f;
    [Tooltip("เลเยอร์ที่เส้นเล็งชนได้ (ควรตรงกับ Hit Mask ของปืน)")]
    public LayerMask aimMask = ~(1 << 2);
    public Color autoDotColor = new Color(1f, 0.12f, 0.08f, 0.95f);
    public float autoDotSize = 9f;

    private PlayerInput playerInput;
    private InputAction aimAction;
    private PickupSystem pickup;
    private WeaponWheelUI wheel;
    private WallHugController wallHug;
    private CinemachineOrbitalFollow orbital;

    private GameObject cachedHeldItem;
    private GunAction heldGun;

    private bool isAiming;
    private float aimYaw;
    private float aimPitch;
    private bool hadControlLastFrame;

    private Vector3 crosshairWorldPoint;
    private bool hasCrosshairPoint;

    private CinemachineBrain brain;
    private CinemachineBlenderSettings ownBlends;

    private static readonly RaycastHit[] Hits = new RaycastHit[32];

    /// <summary>กำลังเล็งไหล่อยู่ (ใช้กล้องเล็ง)</summary>
    public bool IsOverShoulderAiming => isAiming;

    /// <summary>ปืนนี้คือปืนที่กำลังเล็งไหล่อยู่หรือไม่</summary>
    public bool IsAimingWith(GunAction gun) => isAiming && gun != null && gun == heldGun;

    // ------------------------------------------------------------ เปิด/ปิด

    private void Awake()
    {
        if (player == null)
            player = GetComponentInParent<TopDownPlayerController>();

        if (player != null)
        {
            playerInput = player.GetComponent<PlayerInput>();
            pickup = player.GetComponent<PickupSystem>();
            wheel = player.GetComponent<WeaponWheelUI>();
            wallHug = player.GetComponent<WallHugController>();
        }

        if (playerInput != null && playerInput.actions != null)
            aimAction = playerInput.actions.FindAction("Aiming", false);

        if (normalCamera != null)
            orbital = normalCamera.GetComponent<CinemachineOrbitalFollow>();
    }

    private void OnEnable()
    {
        Active = this;
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);

        if (normalCamera != null)
            normalCamera.Priority.Value = normalPriority;
        if (aimCamera != null)
            aimCamera.Priority.Value = 0;

        if (player != null)
        {
            player.SetForceCameraRelative(this, true);

            // เริ่มฉาก: กล้องอยู่ข้างหลังตัวละคร
            if (orbital != null)
            {
                orbital.HorizontalAxis.Value = player.transform.eulerAngles.y;
                orbital.VerticalAxis.Value = Mathf.Clamp(startPitch, orbital.VerticalAxis.Range.x, orbital.VerticalAxis.Range.y);
            }
        }

        hadControlLastFrame = false;
    }

    private void OnDisable()
    {
        if (Active == this)
            Active = null;

        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        SetAiming(false);

        if (normalCamera != null)
            normalCamera.Priority.Value = 0;

        if (player != null)
            player.SetForceCameraRelative(this, false);
    }

    private void OnDestroy()
    {
        if (brain != null && ownBlends != null && brain.CustomBlends == ownBlends)
            brain.CustomBlends = null;

        if (ownBlends != null)
            Destroy(ownBlends);
    }

    private void Start()
    {
        EnsureCrosshair();
        SetCrosshairVisible(false);
        SetupAimBlend();
    }

    // ------------------------------------------------------------ ทุกเฟรม

    private void Update()
    {
        if (player == null)
            return;

        RefreshHeldGun();
        TrackScope();

        bool wantAim = CanOverShoulderAim() && aimAction != null && aimAction.IsPressed();
        if (wantAim != isAiming)
            SetAiming(wantAim);

        bool hasControl = Time.timeScale > 0f && Application.isFocused && !player.IsMovementLocked;
        if (hasControl)
            LockCursor();

        if (isAiming)
        {
            UpdateAimLook();
            hadControlLastFrame = true;
            return;
        }

        UpdateOrbitLook(hasControl);
    }

    private void UpdateOrbitLook(bool hasControl)
    {
        // หยุดหมุนกล้องตอนที่กล้องอื่นคุมจออยู่ หรือเมาส์ใช้ทำอย่างอื่น
        bool canLook = hasControl && orbital != null &&
                       !player.isInVentMode &&                         // กล้อง Vent
                       !(wallHug != null && wallHug.IsHugging) &&      // เมาส์ใช้เลื่อนกล้อง WallHug
                       !player.IsAiming &&                             // ซูม Sniper (GunAction หมุนตัวเอง)
                       !(wheel != null && wheel.IsOpen);               // กำลังเลือกอาวุธ

        if (!canLook)
        {
            hadControlLastFrame = false;
            return;
        }

        // เฟรมแรกหลังได้การควบคุมคืน ข้ามค่าเมาส์ (กันกล้องสะบัดจากระยะที่เมาส์ขยับตอนอยู่ในเมนู)
        if (!hadControlLastFrame)
        {
            hadControlLastFrame = true;
            return;
        }

        Vector2 look = PlayerControls.ReadLook(mouseSensitivity, gamepadSensitivity, playerInput);
        orbital.HorizontalAxis.Value = WrapAngle(orbital.HorizontalAxis.Value + look.x);

        Vector2 range = orbital.VerticalAxis.Range;
        orbital.VerticalAxis.Value = Mathf.Clamp(orbital.VerticalAxis.Value + (invertY ? look.y : -look.y), range.x, range.y);
    }

    // ------------------------------------------------------------ เล็งไหล่

    private void RefreshHeldGun()
    {
        GameObject held = pickup != null ? pickup.HeldItem : null;
        if (held == cachedHeldItem)
            return;

        cachedHeldItem = held;
        heldGun = held != null ? held.GetComponent<GunAction>() : null;
    }

    // ซูม Sniper (กล้องบุคคลที่หนึ่งของ GunAction): จำมุมที่มองล่าสุด เลิกซูมแล้วกล้อง TPS มองต่อจากมุมนั้น
    private bool wasScoped;
    private float scopeYaw;
    private float scopePitch;

    private void TrackScope()
    {
        bool scoped = heldGun != null && heldGun.TryGetScopeLook(out scopeYaw, out scopePitch);

        if (wasScoped && !scoped && recenterBehindAfterAim)
        {
            SyncOrbitToLook(scopeYaw, scopePitch);
            hadControlLastFrame = false; // ไม่เอาเมาส์ของเฟรมนี้ไปหมุนซ้ำ
        }

        wasScoped = scoped;
    }

    private bool CanOverShoulderAim()
    {
        if (heldGun == null || !heldGun.isHeld || !heldGun.UsesOverShoulderAim || aimCamera == null)
            return false;

        // ห้ามเล็ง: ถูกล็อก (เมนู/รัดคอ/ตาย/Eye Wide) พิงกำแพง ปีน ข้ามสิ่งกีดขวาง อยู่ในท่อ หรือกำลังเลือกอาวุธ
        return Time.timeScale > 0f && player.CanAim && !player.IsMovementLocked &&
               !player.IsClimbing && !player.IsVaulting && !player.isInVentMode &&
               !(wheel != null && wheel.IsOpen) && !CommandLineUI.IsTyping;
    }

    private void SetAiming(bool aiming)
    {
        if (isAiming == aiming)
            return;

        isAiming = aiming;

        if (aiming)
        {
            // เริ่มเล็งไปทางที่กล้องมองอยู่จริง (ทั้งซ้าย/ขวา และก้ม/เงย): กล้องมองต่ำอยู่ = เริ่มเล็งลงพื้น ไม่รีเซ็ตกลับระดับสายตา
            if (aimFromCameraDirection && TryGetCameraLookAngles(out float camYaw, out float camPitch))
            {
                aimYaw = camYaw;
                aimPitch = Mathf.Clamp(camPitch, minAimPitch, maxAimPitch);
            }
            else
            {
                aimYaw = player.transform.eulerAngles.y;
                aimPitch = 0f;
            }

            UpdateAimPivot();

            if (aimCamera != null)
                aimCamera.Priority.Value = aimPriority;

            player.SetAimOverride(this, true, freezeMovementWhileAiming);
        }
        else
        {
            if (aimCamera != null)
                aimCamera.Priority.Value = 0;

            if (player != null)
                player.SetAimOverride(this, false, false);

            // กลับกล้องปกติ: อยู่ข้างหลังทิศที่เพิ่งเล็ง และก้ม/เงยเท่าที่เล็งค้างไว้ (ภาพต่อเนื่อง ไม่กระตุกกลับ)
            if (recenterBehindAfterAim)
                SyncOrbitToLook(aimYaw, aimPitch);

            hasCrosshairPoint = false;
            SetCrosshairVisible(false);
        }
    }

    private void UpdateAimLook()
    {
        Vector2 look = Application.isFocused
            ? PlayerControls.ReadLook(aimMouseSensitivity, aimGamepadSensitivity, playerInput)
            : Vector2.zero;

        aimYaw = WrapAngle(aimYaw + look.x);
        aimPitch = Mathf.Clamp(aimPitch + (invertY ? look.y : -look.y), minAimPitch, maxAimPitch);

        // RE4: เมาส์หันตัวละคร (ตอนเริ่มเล็งหมุนเข้าหาทิศกล้องเร็วๆ ไม่ใช่ตัดฉับ)
        Quaternion target = Quaternion.Euler(0f, aimYaw, 0f);
        player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, target, aimTurnSpeed * Time.deltaTime);

        UpdateAimPivot();
    }

    private void UpdateAimPivot()
    {
        if (aimPivot != null)
            aimPivot.rotation = Quaternion.Euler(aimPitch, aimYaw, 0f);
    }

    /// <summary>มุมที่กล้องจริง (Main Camera) มองอยู่: yaw = ซ้าย/ขวา, pitch บวก = ก้มลง (แกน X ของ Unity)</summary>
    private static bool TryGetCameraLookAngles(out float yaw, out float pitch)
    {
        yaw = pitch = 0f;
        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Vector3 forward = cam.transform.forward;
        yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        return true;
    }

    // ตั้งกล้องปกติให้มองทิศเดียวกับที่เพิ่งเล็ง/ซูมอยู่ (วงโคจร: มุมสูงของกล้อง ≈ มุมก้มของภาพ)
    private void SyncOrbitToLook(float yaw, float pitch)
    {
        if (orbital == null)
            return;

        orbital.HorizontalAxis.Value = WrapAngle(yaw);
        Vector2 range = orbital.VerticalAxis.Range;
        orbital.VerticalAxis.Value = Mathf.Clamp(pitch, range.x, range.y);
    }

    /// <summary>
    /// จุดที่กลางจอเล็งอยู่: ยิงเส้นจากกล้องผ่านกลางจอ เริ่มนับจากระดับตัวผู้เล่น
    /// (ไม่นับของที่อยู่ระหว่างกล้องกับผู้เล่น ไม่งั้นกระสุนจะยิงย้อนหลัง)
    /// </summary>
    public bool TryGetAimPoint(out Vector3 point)
    {
        point = Vector3.zero;
        Camera cam = Camera.main;
        if (cam == null || player == null)
            return false;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 pivot = aimPivot != null ? aimPivot.position : player.transform.position;
        float skip = Mathf.Max(0f, Vector3.Dot(pivot - ray.origin, ray.direction));
        ray.origin += ray.direction * skip;

        point = RaycastIgnoringPlayer(ray.origin, ray.direction, aimRange, out Vector3 hit)
            ? hit
            : ray.GetPoint(aimRange);
        return true;
    }

    /// <summary>ทิศกระสุนจากปากกระบอกไปยังจุดเล็งกลางจอ (GunAction เรียกตอนยิง)</summary>
    public bool TryGetShotDirection(Vector3 muzzle, out Vector3 direction)
    {
        direction = Vector3.zero;
        if (!TryGetAimPoint(out Vector3 point))
            return false;

        direction = point - muzzle;
        if (direction.sqrMagnitude < 0.0001f)
            return false;

        direction.Normalize();
        return true;
    }

    private bool RaycastIgnoringPlayer(Vector3 origin, Vector3 direction, float distance, out Vector3 point)
    {
        point = Vector3.zero;
        int count = Physics.RaycastNonAlloc(origin, direction, Hits, distance, aimMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(Hits, 0, count, RaycastHitDistanceComparer.Instance);

        for (int i = 0; i < count; i++)
        {
            if (player != null && Hits[i].collider.transform.IsChildOf(player.transform))
                continue; // ตัวผู้เล่นเอง + ปืนที่ถืออยู่

            point = Hits[i].point;
            return true;
        }

        return false;
    }

    // ------------------------------------------------------------ เป้าเล็ง

    // เรียกหลัง CinemachineBrain ขยับกล้องจริงเสร็จในเฟรมนี้ → เป้าตรงกับภาพที่เห็นพอดี
    private void OnCameraUpdated(CinemachineBrain updatedBrain)
    {
        if (!isAiming || heldGun == null || crosshair == null)
            return;

        Camera cam = updatedBrain != null ? updatedBrain.OutputCamera : Camera.main;
        if (cam == null || !TryGetAimPoint(out Vector3 aimPoint))
        {
            SetCrosshairVisible(false);
            return;
        }

        // จุดที่กระสุนจะไปโดนจริง: จากปากกระบอกไปยังจุดเล็ง (ถ้ามีของขวางหน้าปืน จุดจะอยู่ตรงของนั้น)
        Vector3 muzzle = heldGun.firePoint != null ? heldGun.firePoint.position : aimPivot.position;
        Vector3 toAim = aimPoint - muzzle;
        crosshairWorldPoint = aimPoint;
        if (toAim.sqrMagnitude > 0.0001f &&
            RaycastIgnoringPlayer(muzzle, toAim.normalized, toAim.magnitude + 0.05f, out Vector3 impact))
            crosshairWorldPoint = impact;

        hasCrosshairPoint = true;
        Vector3 screen = cam.WorldToScreenPoint(crosshairWorldPoint);
        if (screen.z <= 0f)
        {
            SetCrosshairVisible(false);
            return;
        }

        crosshair.position = new Vector3(screen.x, screen.y, 0f);
        SetCrosshairVisible(true);
    }

    private void SetCrosshairVisible(bool visible)
    {
        if (crosshair != null && crosshair.gameObject.activeSelf != visible)
            crosshair.gameObject.SetActive(visible);
    }

    // ไม่ได้ใส่รูปเป้าไว้: สร้างจุดแดงเล็กๆ บน Canvas ของตัวเอง
    private void EnsureCrosshair()
    {
        if (crosshair != null)
            return;

        Canvas canvas = UIBuilder.CreateCanvas("TPS Crosshair (Auto)", 90, transform);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;

        Image dot = UIBuilder.Panel("AimDot", canvas.transform, autoDotColor, false);
        dot.sprite = UIBuilder.CircleSprite;
        dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = Vector2.zero;
        dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        dot.rectTransform.sizeDelta = new Vector2(autoDotSize, autoDotSize);
        crosshair = dot.rectTransform;
    }

    // ------------------------------------------------------------ Blend ระหว่างกล้องปกติ ↔ กล้องเล็ง

    private void SetupAimBlend()
    {
        if (normalCamera == null || aimCamera == null || aimBlendTime <= 0f || CinemachineBrain.ActiveBrainCount == 0)
            return;

        brain = CinemachineBrain.GetActiveBrain(0);
        if (brain == null)
            return;

        // Brain มี Custom Blends ของผู้ใช้อยู่แล้ว: ไม่ทับ (ตั้ง Blend ของกล้องเล็งในไฟล์นั้นแทน)
        if (brain.CustomBlends != null)
            return;

        var blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, aimBlendTime);
        ownBlends = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
        ownBlends.name = "TPS Aim Blends (Auto)";
        ownBlends.CustomBlends = new[]
        {
            new CinemachineBlenderSettings.CustomBlend { From = normalCamera.Name, To = aimCamera.Name, Blend = blend },
            new CinemachineBlenderSettings.CustomBlend { From = aimCamera.Name, To = normalCamera.Name, Blend = blend },
        };
        brain.CustomBlends = ownBlends;
    }

    // ------------------------------------------------------------ ตัวช่วย

    private static void LockCursor()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private static float WrapAngle(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !isAiming || !hasCrosshairPoint)
            return;

        Gizmos.color = Color.red;
        Vector3 muzzle = heldGun != null && heldGun.firePoint != null ? heldGun.firePoint.position : transform.position;
        Gizmos.DrawLine(muzzle, crosshairWorldPoint);
        Gizmos.DrawWireSphere(crosshairWorldPoint, 0.08f);
    }
#endif
}
