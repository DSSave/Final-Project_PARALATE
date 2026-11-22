using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Eye Wide: กด T เพื่อสลับเป็นมุมมองบุคคลที่หนึ่งสำหรับสำรวจแผนที่ (ยืนอยู่กับที่ หมุนมองด้วยเมาส์)
/// เลื่อนลูกกลิ้งเพื่อซูมเข้า/ออก (มีเสียงกล้องซูม) กด T หรือ Esc เพื่อออก
/// ใช้กล้อง Cinemachine ที่สร้างเองตอนรัน Priority 1900: สูงกว่ากล้องห้อง/ท่อลม แต่ต่ำกว่ากล้องเล็งปืน (2000)
/// </summary>
[DisallowMultipleComponent]
public class EyeWideController : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("ใช้เมื่อโมเดลไม่มีกระดูก Head (ไม่ใช่ Humanoid): ตำแหน่งตาเทียบกับเท้าผู้เล่น")]
    public Vector3 eyeOffset = new Vector3(0f, 1.4f, 0.18f);
    [Tooltip("ระยะจากกระดูก Head ไปถึงระดับตา: X = สูงขึ้น, Y = ยื่นไปข้างหน้า (กันเห็นด้านในหัว)")]
    public Vector2 eyeFromHeadBone = new Vector2(0.06f, 0.18f);
    public int cameraPriority = 1900;
    public float mouseSensitivity = 0.12f;

    [Header("Look Limits (องศา)")]
    [Tooltip("เงยหน้าได้สูงสุดกี่องศาจากแนวราบ")]
    [Range(0f, 89f)] public float maxLookUp = 70f;
    [Tooltip("ก้มหน้าได้สูงสุดกี่องศาจากแนวราบ")]
    [Range(0f, 89f)] public float maxLookDown = 70f;
    [Tooltip("หันซ้าย/ขวาได้สูงสุดกี่องศา นับจากทิศที่ตัวละครหันอยู่ตอนเข้าโหมด (180 = หมุนได้รอบตัว)")]
    [Range(0f, 180f)] public float maxLookSide = 180f;

    [Header("Zoom")]
    public float defaultFov = 60f;
    public float minFov = 12f;
    public float maxFov = 75f;
    [Tooltip("องศา FOV ที่เปลี่ยนต่อการเลื่อนลูกกลิ้ง 1 คลิก")]
    public float zoomStep = 6f;
    public float zoomSmooth = 12f;
    [Tooltip("หยุดเลื่อนลูกกลิ้งนานเท่านี้ (วินาที) เสียงซูมจะเริ่มเบาลง")]
    public float zoomSoundHold = 0.1f;
    [Tooltip("เวลาที่เสียงซูมใช้เบาลงจนเงียบ (วินาที)")]
    public float zoomSoundFade = 0.08f;

    private TopDownPlayerController controller;
    private PlayerInput playerInput;
    private Transform body; // ตัวผู้เล่น (ใช้วัดตำแหน่ง/ทิศ) คอมโพเนนต์นี้อยู่บนวัตถุลูก "EyeWide" ของผู้เล่นได้
    private PickupSystem pickup;
    private WeaponWheelUI wheel;
    private CinemachineCamera eyeCamera;
    private bool active;
    private float yaw;
    private float baseYaw; // ทิศที่ตัวละครหันอยู่ตอนเข้าโหมด (จุดอ้างอิงของขีดจำกัดซ้าย/ขวา)
    private float pitch;
    private float targetFov;
    private Vector3 activeEyeOffset;
    private AudioSource zoomSource;
    private float lastScrollTime;
    private int lastZoomDirection;

    public bool IsActive => active;

    private void Awake()
    {
        // กล้องต้องไม่แย่งจอตั้งแต่เริ่มด่าน (เผื่อ Priority ใน Prefab ถูกตั้งไว้สูง)
        CinemachineCamera own = GetComponent<CinemachineCamera>();
        if (own != null)
            own.Priority.Value = 0;

        // หาจากตัวเองขึ้นไปหาพ่อ: วางคอมโพเนนต์นี้บนวัตถุลูกของ Player (เช่น "EyeWide") แล้วปรับค่าใน Inspector ได้
        controller = GetComponentInParent<TopDownPlayerController>();
        playerInput = GetComponentInParent<PlayerInput>();
        pickup = GetComponentInParent<PickupSystem>();
        wheel = GetComponentInParent<WeaponWheelUI>();
        body = controller != null ? controller.transform : transform;
    }

    private void Update()
    {
        // ปุ่มอยู่ใน PlayerInput.inputactions: EyeWide (T) / Pause (Esc)
        bool toggle = PlayerControls.WasPressed(PlayerControls.EyeWide, Key.T, playerInput);

        if (active)
        {
            if (toggle || PlayerControls.WasPressed(PlayerControls.Pause, Key.Escape, playerInput))
            {
                Exit();
                return;
            }

            HandleLook();
            HandleZoom();
            return;
        }

        if (toggle && CanEnter())
            Enter();
    }

    private bool CanEnter()
    {
        if (controller == null || Time.timeScale <= 0f)
            return false;

        // ห้ามใช้ตอนถูกล็อก (เมนู/โน้ต/รัดคอ/ตาย) ปีน ข้ามสิ่งกีดขวาง อยู่ในท่อ หรือกำลังเล็งปืน
        if (controller.IsMovementLocked || controller.IsClimbing || controller.IsVaulting ||
            controller.isInVentMode || controller.IsAiming)
            return false;

        return wheel == null || !wheel.IsOpen;
    }

    private void Enter()
    {
        EnsureCamera();

        active = true;
        baseYaw = body.eulerAngles.y;
        yaw = baseYaw;
        pitch = 0f;
        targetFov = defaultFov;
        eyeCamera.Lens.FieldOfView = defaultFov;
        activeEyeOffset = ResolveEyeOffset();
        UpdateCameraPose();

        eyeCamera.Priority.Value = cameraPriority;
        controller.SetMovementLocked(this, true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        SfxLibrary.Play("eyewide_on");
        HudToast.Show("EYE WIDE  —  Scroll to zoom  •  T to exit", 2.5f);
    }

    private void Exit()
    {
        if (!active)
            return;

        active = false;

        if (eyeCamera != null)
            eyeCamera.Priority.Value = 0;

        if (zoomSource != null)
            zoomSource.Stop();

        if (controller != null)
            controller.SetMovementLocked(this, false);

        // กลับสู่โหมดเมาส์ของตอนเล่น (เหมือน TopDownPlayerController.Start)
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        SfxLibrary.Play("eyewide_off");
    }

    private void HandleLook()
    {
        // ซูมมากแล้วหมุนช้าลงตามสัดส่วน จะได้เล็งดูของไกลๆ ได้นิ่ง
        float zoomFactor = Mathf.Clamp(eyeCamera.Lens.FieldOfView / defaultFov, 0.2f, 1.25f);

        // CameraLook (เมาส์ขยับ / right stick) ค่าที่ได้เป็นองศาแล้ว
        Vector2 delta = PlayerControls.ReadLook(mouseSensitivity * zoomFactor, 120f * zoomFactor, playerInput);

        yaw += delta.x;

        // จำกัดมุมหันซ้าย/ขวา: คิดเป็น "ส่วนต่างจากทิศตอนเข้าโหมด" แล้วค่อยหนีบ (DeltaAngle กันปัญหาข้าม 0/360)
        if (maxLookSide < 179.9f)
            yaw = baseYaw + Mathf.Clamp(Mathf.DeltaAngle(baseYaw, yaw), -maxLookSide, maxLookSide);

        // pitch เป็นบวก = ก้ม, ลบ = เงย (ตามแกน X ของ Unity)
        pitch = Mathf.Clamp(pitch - delta.y, -maxLookUp, maxLookDown);

        UpdateCameraPose();
    }

    private void HandleZoom()
    {
        float scroll = PlayerControls.ReadZoom(playerInput);

        if (Mathf.Abs(scroll) > 0.01f)
        {
            float previous = targetFov;
            targetFov = Mathf.Clamp(targetFov - Mathf.Sign(scroll) * zoomStep, minFov, maxFov);

            // เสียงกล้องซูม: เล่นเฉพาะเมื่อซูมได้จริง (ไม่ดังตอนสุดระยะ)
            if (!Mathf.Approximately(previous, targetFov))
                PlayZoomSound(scroll > 0f ? 1 : -1);
        }

        eyeCamera.Lens.FieldOfView = Mathf.Lerp(eyeCamera.Lens.FieldOfView, targetFov, zoomSmooth * Time.unscaledDeltaTime);
        UpdateZoomSound();
    }

    // เสียงซูมใช้ AudioSource ของตัวเอง (ไม่ใช้ PlayOneShot) เพื่อ "ตัดเสียงได้" ทันทีที่หยุดเลื่อนลูกกลิ้ง
    // คลิปเสียงยาวกว่าการซูม 1 คลิก ถ้าปล่อยเล่นจนจบจะได้ยินเสียงค้างหลังหยุดซูม
    private void PlayZoomSound(int direction)
    {
        lastScrollTime = Time.unscaledTime;

        // ยังดังอยู่และซูมทิศเดิม: ปล่อยให้เล่นต่อ (ไม่เริ่มคลิปใหม่ซ้อนทุกคลิก)
        if (zoomSource.isPlaying && direction == lastZoomDirection)
        {
            zoomSource.volume = SfxLibrary.GainOf(zoomSource.clip);
            return;
        }

        AudioClip clip = SfxLibrary.Pick(direction > 0 ? "zoom_in" : "zoom_out");
        if (clip == null)
            return;

        lastZoomDirection = direction;
        zoomSource.clip = clip;
        zoomSource.volume = SfxLibrary.GainOf(clip);
        zoomSource.Play();
    }

    private void UpdateZoomSound()
    {
        if (zoomSource == null || !zoomSource.isPlaying)
            return;

        if (Time.unscaledTime - lastScrollTime < zoomSoundHold)
            return;

        // เบาลงเร็วๆ แทนการตัดฉับ (ตัดฉับจะได้ยินเสียง "ป๊อก")
        zoomSource.volume = Mathf.MoveTowards(zoomSource.volume, 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, zoomSoundFade));
        if (zoomSource.volume <= 0f)
            zoomSource.Stop();
    }

    // ระดับตา = กระดูก Head ของโมเดล (วัดครั้งเดียวตอนเข้าโหมด กล้องจึงไม่สั่นตามแอนิเมชัน)
    private Vector3 ResolveEyeOffset()
    {
        Animator animator = controller != null ? controller.animator : null;
        if (animator == null)
            animator = body.GetComponentInChildren<Animator>();

        Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head == null)
            return eyeOffset;

        float headHeight = head.position.y - body.position.y;
        return new Vector3(0f, headHeight + eyeFromHeadBone.x, eyeFromHeadBone.y);
    }

    private void UpdateCameraPose()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Quaternion yawOnly = Quaternion.Euler(0f, yaw, 0f);
        eyeCamera.transform.SetPositionAndRotation(body.position + yawOnly * activeEyeOffset, rotation);
    }

    private void EnsureCamera()
    {
        if (eyeCamera != null)
            return;

        // ปกติคอมโพเนนต์นี้อยู่บนวัตถุลูก "EyeWide" ของผู้เล่น → ใช้วัตถุนั้นเป็นกล้องเลย (ไม่สร้างวัตถุเพิ่ม)
        // ถ้ามีคนติดไว้บนตัว Player ตรงๆ จะขยับ Transform ของ Player ไม่ได้ จึงค่อยสร้างวัตถุลูกแยก
        GameObject go = gameObject;
        if (transform == body)
        {
            go = new GameObject("EyeWideCamera");
            go.transform.SetParent(transform, false);
        }

        // วัตถุ EyeWide เป็นกล้อง (CinemachineCamera) ในตัว: ถ้าใน Prefab มีอยู่แล้วใช้ตัวนั้น (ค่า Lens อื่นๆ ปรับใน Inspector ได้)
        eyeCamera = go.GetComponent<CinemachineCamera>();
        if (eyeCamera == null)
        {
            eyeCamera = go.AddComponent<CinemachineCamera>();
            eyeCamera.Lens.NearClipPlane = 0.05f;
        }

        eyeCamera.Priority.Value = 0;
        eyeCamera.Lens.FieldOfView = defaultFov;

        zoomSource = go.AddComponent<AudioSource>();
        zoomSource.playOnAwake = false;
        zoomSource.loop = false;
        zoomSource.spatialBlend = 0f;
        AudioManager.RouteToSfx(zoomSource);
    }

    private void OnDisable()
    {
        Exit();
    }
}
