using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;


[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(AudioSource))]
public class TopDownPlayerController : MonoBehaviour
{
    [Header("Movement Type")]
    [Tooltip("ติ๊กถูกสำหรับ Isometric/Third-Person, เอาออกสำหรับ Top-Down เดิม")]
    public bool useCameraRelativeMovement = false;

    [Header("Movement Speeds")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float crouchSpeed = 2.5f;
    [Tooltip("เวลาที่ใช้เร่งจากความเร็วเดินถึงความเร็ววิ่งสูงสุด (วินาที)\n" +
             "ระหว่างเร่ง = ท่า Slow Run / ถึงความเร็วสูงสุด = ท่า Fast Run")]
    [Min(0f)] public float sprintAccelerationTime = 0.8f;
    private float wallHugMoveAmount;

    [Header("Rotation Settings")]
    public float rotationSpeed = 15f;

    [Header("Aim & Armed Settings")]
    public bool isArmed = false;
    public float cameraZoomSpeed = 10f;
    private float normalFOV = 50f;
    private float aimFOV = 40f;

    [Header("Wall Hug")]
    [SerializeField] private WallHugController wallHug;


    [Header("Cinemachine Camera")]
    public CinemachineCamera virtualCamera;

    [Header("Vault Settings (ปีนข้ามสิ่งกีดขวาง)")]
    public float vaultDistance = 2f;
    public float vaultHeight = 1.2f;
    public float vaultDuration = 0.4f;
    public LayerMask hurdleLayer;
    public float raycastDistance = 1f;
    public float lowRayHeight = 0.2f;
    public float highRayHeight = 1.0f;

    [Header("Crouch Physical Settings (การย่อตัว)")]
    [Tooltip("เปิดเพื่อหดขนาดแคปซูลฟิสิกส์ตอนนั่ง (ถ้าเปิดแล้วเดินไม่ไปให้ปิดไว้)")]
    public bool enablePhysicalCrouch = false;
    public float crouchHeight = 1f;
    public float crouchTransitionSpeed = 10f;
    private float originalHeight;
    private Vector3 originalCenter;

    [Header("Animation (อนิเมชั่น)")]
    public Animator animator;

    [Header("Ladder Settings (ปีนบันได)")]
    public float climbUpSpeed = 3f;
    public float climbDownSpeed = 6f;
    public float topExitOffset = 1.5f;
    [Tooltip("ใช้กับบันไดเก่าที่ไม่มีคอมโพเนนต์ Ladder เท่านั้น (บันไดที่มี Ladder ใช้ Stand Distance ของบันไดนั้น)\n" +
             "ระยะห่างจากหน้าบันไดถึงตัวผู้เล่นตอนปีน (เมตร)")]
    public float ladderSnapDistance = 0.35f;
    [Tooltip("ความสูงจากเท้าที่ใช้ยิง Ray หาหน้าบันได")]
    public float ladderSnapRayHeight = 1.0f;
    [Tooltip("คลิปปีน (MigeClimbing) เล่นความเร็วปกติแล้วตัวขึ้นกี่เมตรต่อวินาที ใช้ปรับความเร็วอนิเมชั่นให้ตรงกับความเร็วปีนจริง")]
    public float climbAnimMetersPerSecond = 0.68f;
    [Tooltip("ความเร็วอนิเมชั่นปีนสูงสุด (เท่าของปกติ) กันท่าปีนเร็วจนดูไม่จริง")]
    public float maxClimbAnimSpeed = 2.5f;
    [Tooltip("ความเร็วท่าปีนขึ้นขอบ (MigeClimbingEdge) ตอนถึงยอดบันได: 1 = ปกติ (2.07 วิ), มากกว่า = เร็วขึ้น")]
    [Min(0.1f)] public float topExitAnimSpeed = 1.4f;
    [Tooltip("ชื่อคลิปท่าปีนขึ้นขอบ ใช้หาความยาวคลิปให้การเลื่อนตัวตรงกับท่า (ไม่มีคลิป = ก้าวขึ้น 0.5 วิแบบเดิม)")]
    public string topExitClipName = "MigeClimbingEdge";
    [Tooltip("มีท่าปีนขึ้นขอบ: เริ่มท่าเมื่อเท้าอยู่ต่ำกว่าพื้นด้านบนเท่านี้ (เมตร) = ตอนมือถึงขอบพอดีตามเฟรมแรกของคลิป")]
    public float topExitStartBelowFloor = 0.95f;
    public LayerMask ladderLayer;

    // ★ แก้ไขตรงนี้ — เพิ่ม field ใหม่ ใช้กำหนดว่ากล้องมองชันแค่ไหนถึงจะสลับไปใช้ camUp แทน camForward
    [Header("Camera Relative Movement (Top-down fix)")]
    [Tooltip("ถ้า |camera.forward.y| เกินค่านี้ ถือว่ากล้องมองชันเกินไป จะสลับไปใช้ camera.up แทน")]
    public float topDownAngleThreshold = 0.9f;

    [Tooltip("กล้องตัดเปลี่ยนมุม/เข้าโซนใหม่ระหว่างกดเดินค้าง: เดินทิศเดิมต่อจนกว่าจะปล่อยปุ่มหรือเปลี่ยนทิศที่กด " +
             "(กันตัวละครหักกลับไปมาตรงรอยต่อกล้อง แบบเกมกล้องมุมตายตัว)")]
    public bool keepDirectionOnCameraCut = true;

    // แกนการเดินที่ใช้อยู่ (ค้างไว้ตอนกล้องตัด)
    private Vector3 heldForward = Vector3.forward;
    private Vector3 heldRight = Vector3.right;
    private bool hasHeldAxes;
    private Vector2 heldInput;

    [Header("Vent / Tank Control Settings")]
    public bool isInVentMode = false;
    public float ventMoveSpeed = 2.5f;
    public float ventTurnSpeed = 90f; // องศาต่อวินาที

    private CharacterController controller;
    private Camera mainCamera;
    private PlayerInput playerInput;

    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private InputAction pointerAction;
    private InputAction hurdleAction;
    private InputAction aimAction;
    private InputAction whistleAction;

    private bool isVaulting = false;
    // ล็อกแบบมีเจ้าของ: อ่านโน้ต / รัดคอ / ตาย / ปีนบันได ล็อกด้วย key ของตัวเอง ไม่ปลดล็อกของกันและกัน
    private readonly HashSet<object> movementLockOwners = new HashSet<object>();
    private bool isMovementLocked => movementLockOwners.Count > 0;

    private static readonly int IsWallHuggingHash = Animator.StringToHash("isWallHugging");
    private static readonly int WallHugMoveHash = Animator.StringToHash("WallHugMove");
    private static readonly int IsClimbingHash = Animator.StringToHash("IsClimbing");
    private static readonly int ClimbSpeedHash = Animator.StringToHash("ClimbSpeed");
    private static readonly int ClimbTopExitHash = Animator.StringToHash("ClimbTopExit");
    private static readonly int IsWalkHash = Animator.StringToHash("isWalk");
    private static readonly int IsSneakHash = Animator.StringToHash("isSneak");
    private static readonly int IsRunHash = Animator.StringToHash("isRun");
    private static readonly int RunBlendHash = Animator.StringToHash("RunBlend");

    // 0 = เพิ่งเริ่มวิ่ง (Slow Run) → 1 = วิ่งเต็มความเร็ว (Fast Run)
    private float sprintBlend;
    private bool hasRunParams;
    private bool checkedRunParams;
    private bool isClimbing = false;
    private float movementSpeedMultiplier = 1f;
    private bool isSprintLocked = false;

    private bool isWalking = false;
    private bool isCrouching = false;
    private bool isSprinting = false;
    private bool isAiming = false;

    private Transform aimCameraOverrideTransform;
    private float aimCameraOverrideFov = 50f;

    private float climbCooldown = 0f;

    public bool IsMovementLocked => isMovementLocked;
    public bool IsClimbing => isClimbing;
    public bool IsVaulting => isVaulting;
    public bool IsCrouching => isCrouching;
    public bool IsAiming => isAiming;

    // ห้ามเล็ง (คลิกขวา) แบบมีเจ้าของ เหมือน movementLockOwners: กล้องอิสระที่ Base ใช้ปิดคลิกขวา
    private readonly HashSet<object> aimBlockers = new HashSet<object>();

    /// <summary>เล็งได้ไหม: ไม่ได้ถ้ามีระบบสั่งบล็อก หรือกำลังพิงกำแพง (ตอนพิง เมาส์ใช้เลื่อนกล้องซ้าย/ขวา)</summary>
    private PickupSystem heldItems;

    bool HoldingUnarmedOrKnife()
    {
        if (heldItems == null)
            heldItems = GetComponent<PickupSystem>();

        return heldItems != null &&
               (heldItems.CurrentSlot == HeldSlot.Unarmed || heldItems.CurrentSlot == HeldSlot.Knife);
    }

    public bool CanAim => aimBlockers.Count == 0 && !(wallHug != null && wallHug.IsHugging);

    // ------------------------------------------------------------ กล้อง TPS (PlayerTpsCameraRig)

    // บังคับเดินตามทิศกล้อง (ไม่สน Move Space ของ CameraZone) แบบมีเจ้าของ
    private readonly HashSet<object> cameraRelativeOwners = new HashSet<object>();
    private bool ForceCameraRelative => cameraRelativeOwners.Count > 0;

    /// <summary>กล้อง TPS: บังคับให้กด W = เดินไปทางที่กล้องหันเสมอ (ปิดคืนเมื่อกล้อง TPS ปิด)</summary>
    public void SetForceCameraRelative(object owner, bool active)
    {
        if (owner == null)
            return;

        if (active) cameraRelativeOwners.Add(owner);
        else cameraRelativeOwners.Remove(owner);
    }

    // เล็งไหล่แบบ RE4: ระบบภายนอกหมุนตัวละครเอง (ตามเมาส์) และเลือกได้ว่าจะให้หยุดเดินระหว่างเล็งไหม
    private object aimOverrideOwner;
    private bool aimOverrideFreezesMovement;

    /// <summary>มีระบบอื่นคุมการเล็ง/การหันตัวอยู่ (เช่น PlayerTpsCameraRig ตอนเล็งไหล่)</summary>
    public bool HasAimOverride => aimOverrideOwner != null;

    public void SetAimOverride(object owner, bool active, bool freezeMovement)
    {
        if (owner == null)
            return;

        if (active)
        {
            aimOverrideOwner = owner;
            aimOverrideFreezesMovement = freezeMovement;
        }
        else if (aimOverrideOwner == owner)
        {
            aimOverrideOwner = null;
            aimOverrideFreezesMovement = false;
        }
    }

    public void SetAimBlocked(object owner, bool blocked)
    {
        if (owner == null)
            return;

        if (blocked) aimBlockers.Add(owner);
        else aimBlockers.Remove(owner);
    }

    /// <summary>ตัวคูณความเร็วตอนย่อ (ชิป Passive ของ X-Gear เช่น FleetFoot = 1.05)</summary>
    public float CrouchSpeedMultiplier { get; set; } = 1f;

    [Header("Player Audio")]
    public PlayerAudioController playerAudio;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;
        playerInput = GetComponent<PlayerInput>();
        if (wallHug == null)
            wallHug = GetComponent<WallHugController>();

        if (playerAudio == null) playerAudio = GetComponent<PlayerAudioController>();

        if (controller != null)
        {
            originalHeight = controller.height;
            originalCenter = controller.center;
        }

        moveAction = playerInput.actions["Movement"];
        sprintAction = playerInput.actions["Sprint"];
        crouchAction = playerInput.actions["Crouch"];
        pointerAction = playerInput.actions["PointerPosition"];
        hurdleAction = playerInput.actions["Hurdle"];
        aimAction = playerInput.actions["Aiming"];
        whistleAction = playerInput.actions["Whistle"];

        if (crouchAction != null) crouchAction.performed += OnCrouch;
        if (sprintAction != null)
        {
            sprintAction.performed += OnSprintStart;
            sprintAction.canceled += OnSprintStop;
        }
        if (hurdleAction != null) hurdleAction.performed += OnVault;
        if (whistleAction != null) whistleAction.performed += OnWhistle;

        if (virtualCamera != null)
        {
            normalFOV = virtualCamera.Lens.FieldOfView;
            aimFOV = normalFOV * 0.8f;
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        // ระบบเสริมของผู้เล่น (X-Gear, Eye Wide, เมนู Esc) ติดให้อัตโนมัติ ไม่ต้องแก้ Prefab
        PlayerSystems.EnsureOn(this);
    }

    private void OnDestroy()
    {
        if (crouchAction != null) crouchAction.performed -= OnCrouch;
        if (sprintAction != null)
        {
            sprintAction.performed -= OnSprintStart;
            sprintAction.canceled -= OnSprintStop;
        }
        if (hurdleAction != null) hurdleAction.performed -= OnVault;
        if (whistleAction != null) whistleAction.performed -= OnWhistle;
    }

    private void OnCrouch(InputAction.CallbackContext context)
    {
        isCrouching = !isCrouching;
    }

    private void OnSprintStart(InputAction.CallbackContext context)
    {
        if (isSprintLocked) return;
        isSprinting = true;
    }

    private void OnSprintStop(InputAction.CallbackContext context)
    {
        isSprinting = false;
    }

    private void OnVault(InputAction.CallbackContext context)
    {
        if (isVaulting || isMovementLocked || isClimbing) return;
        AttemptVault();
    }

    private void OnWhistle(InputAction.CallbackContext context)
    {
        if (isVaulting || isMovementLocked) return;
        if (playerAudio != null) playerAudio.PlayWhistle();
    }

    void Update()
    {
        if (climbCooldown > 0f) climbCooldown -= Time.deltaTime;
        if (isVaulting || isMovementLocked) return;
        if (isClimbing) { HandleClimbing(); return; }

        if (isInVentMode)
        {
            HandleVentMovement();
            PlayAnima();
            return;
        }

        // คลิกขวาหันตามเมาส์ได้เฉพาะตอนถือปืน (มือเปล่า / มีด = ไม่หันตามเมาส์ ฟันตรงหน้าตัวละคร)
        // เล็งไหล่ (กล้อง TPS): ระบบเล็งคุมการหันตัวเอง
        if (aimOverrideOwner != null) isAiming = true;
        else if (aimAction != null) isAiming = CanAim && aimAction.IsPressed() && !HoldingUnarmedOrKnife();

        if (!isArmed && virtualCamera != null)
        {
            float targetFOV = isAiming ? aimFOV : normalFOV;
            virtualCamera.Lens.FieldOfView = Mathf.Lerp(virtualCamera.Lens.FieldOfView, targetFOV, cameraZoomSpeed * Time.deltaTime);
        }

        HandleCrouchPhysicality();

        // อ่าน input และคำนวณทิศทางครั้งเดียวต่อเฟรม แล้วส่งให้ทั้งการเดินและการหมุน
        Vector3 rawDirection = GetRawMoveDirection();

        // เล็งไหล่แบบ RE4: ยืนนิ่ง (ยังเรียก HandleMovement เพื่อแรงโน้มถ่วง/แอนิเมชันยืน) และไม่หันตัวเอง
        if (aimOverrideOwner != null && aimOverrideFreezesMovement)
            rawDirection = Vector3.zero;

        HandleMovement(rawDirection);
        if (aimOverrideOwner == null)
            HandleRotation(rawDirection);
        PlayAnima();
    }

    public void SetMovementLocked(object owner, bool locked)
    {
        if (owner == null)
            return;

        if (locked)
        {
            movementLockOwners.Add(owner);

            // หยุดแอนิเมชันเดินค้าง ตอนถูกล็อก (Update จะ return ก่อนถึง PlayAnima)
            isWalking = false;
            PlayAnima();
        }
        else
        {
            movementLockOwners.Remove(owner);
        }
    }

    public void SetMovementSpeedMultiplier(float multiplier)
    {
        movementSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    public void SetSprintLocked(bool locked)
    {
        isSprintLocked = locked;
        if (locked) isSprinting = false;
    }

    public void SetAimCameraOverride(Transform cameraTransform, float fieldOfView, bool active)
    {
        aimCameraOverrideTransform = active ? cameraTransform : null;
        aimCameraOverrideFov = fieldOfView;
    }

    void HandleCrouchPhysicality()
    {
        if (!enablePhysicalCrouch || controller == null) return;

        float targetHeight = isCrouching ? crouchHeight : originalHeight;

        Vector3 targetCenter = originalCenter;
        targetCenter.y = originalCenter.y - ((originalHeight - targetHeight) * 0.5f);

        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        controller.center = Vector3.Lerp(controller.center, targetCenter, crouchTransitionSpeed * Time.deltaTime);
    }

    // ★ แก้ไขตรงนี้ — เพิ่มฟังก์ชันใหม่ทั้งหมด ใช้แทนการคำนวณ direction ที่เคยเขียนซ้ำสองจุด (HandleMovement กับ HandleRotation)
    Vector3 GetRawMoveDirection()
    {
        Vector2 moveInput = moveAction != null
            ? moveAction.ReadValue<Vector2>()
            : Vector2.zero;

        GetMoveAxes(out Vector3 forward, out Vector3 right);

        if (moveInput.sqrMagnitude < 0.01f)
        {
            // ปล่อยปุ่ม: ใช้แกนใหม่ได้ทันทีครั้งหน้า
            hasHeldAxes = false;
            return Vector3.zero;
        }

        // กล้อง TPS หมุนตามเมาส์ตลอด: แกนต้องเปลี่ยนตามกล้องทันที (ไม่ค้างทิศเดิมแบบกล้องมุมตายตัว)
        if (keepDirectionOnCameraCut && hasHeldAxes && !ForceCameraRelative)
        {
            bool axesChanged = Vector3.Dot(forward, heldForward) < 0.94f;   // แกนเปลี่ยนเกิน ~20°
            bool inputSame = Vector2.Dot(moveInput.normalized, heldInput) > 0.8f;

            if (axesChanged && inputSame)
            {
                forward = heldForward;   // กดค้างทิศเดิม → เดินทิศเดิมต่อ
                right = heldRight;
            }
        }

        heldForward = forward;
        heldRight = right;
        heldInput = moveInput.normalized;
        hasHeldAxes = true;

        return (forward * moveInput.y + right * moveInput.x).normalized;
    }

    /// <summary>
    /// แกนการเดิน (W = forward, D = right) บนพื้นราบ:
    /// ถ้าผู้เล่นอยู่ใน CameraZone ที่ตั้ง Move Space ไว้ ใช้ค่าของโซนนั้น ไม่งั้นใช้ Use Camera Relative Movement ของผู้เล่น
    /// </summary>
    void GetMoveAxes(out Vector3 forward, out Vector3 right)
    {
        // กล้อง TPS: เดินตามกล้องเสมอ (โซนกล้องห้องที่ยังอยู่ใน Scene ไม่มีผลต่อทิศเดิน)
        if (ForceCameraRelative && TryGetCameraAxes(out forward, out right))
            return;

        CameraZone zone = CameraZone.CurrentMoveZone;
        CameraZone.MoveSpace space = zone != null ? zone.moveSpace : CameraZone.MoveSpace.PlayerDefault;

        if (space == CameraZone.MoveSpace.PlayerDefault)
            space = useCameraRelativeMovement ? CameraZone.MoveSpace.CameraRelative : CameraZone.MoveSpace.WorldAxes;

        if (space == CameraZone.MoveSpace.CameraRelative && TryGetCameraAxes(out forward, out right))
            return;

        if (zone != null && space == CameraZone.MoveSpace.CustomAxes)
        {
            zone.GetFixedAxes(out forward, out right);
            return;
        }

        forward = Vector3.forward;
        right = Vector3.right;
    }

    bool TryGetCameraAxes(out Vector3 forward, out Vector3 right)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        forward = Vector3.forward;
        right = Vector3.right;

        if (mainCamera == null)
            return false;

        Vector3 camForward = mainCamera.transform.forward;
        Vector3 camRight = mainCamera.transform.right;

        // กล้องมองชันเกือบตรงลงพื้น (top-down): forward ไม่มีส่วนแนวราบเหลือ ใช้ camera.up แทน
        Vector3 forwardOnPlane = Mathf.Abs(camForward.y) > topDownAngleThreshold ? mainCamera.transform.up : camForward;
        forwardOnPlane.y = 0f;
        camRight.y = 0f;

        if (forwardOnPlane.sqrMagnitude < 0.0001f || camRight.sqrMagnitude < 0.0001f)
            return false;

        forward = forwardOnPlane.normalized;
        right = camRight.normalized;
        return true;
    }

    Vector3 GetCameraRelativeDirection(Vector2 moveInput)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        Vector3 camForward = mainCamera.transform.forward;
        Vector3 camUp = mainCamera.transform.up;
        Vector3 camRight = mainCamera.transform.right;

        // ถ้ากล้องมองชันเกือบตรงลงพื้น (top-down) forward จะไม่มีองค์ประกอบแนวราบเหลือให้ใช้
        // ให้สลับไปใช้ camera.up แทน เพราะมันยังอยู่ในแนวราบพอดีตอนกล้องมองลงตรงๆ
        Vector3 forwardOnPlane = Mathf.Abs(camForward.y) > topDownAngleThreshold ? camUp : camForward;

        forwardOnPlane.y = 0f;
        camRight.y = 0f;

        return (forwardOnPlane.normalized * moveInput.y + camRight.normalized * moveInput.x).normalized;
    }

    void HandleMovement(Vector3 rawDirection)
    {
        Vector3 direction = rawDirection;

        // ตรวจ input ดิบก่อนกรองทิศเดิน:
        // กดเฉียงเข้าหากำแพงเพื่อพิง หรือกดเดินออกเพื่อปล่อย
        if (wallHug != null)
            wallHug.UpdateWallHug(direction);

        if (wallHug != null)
            direction = wallHug.ConstrainMoveDirection(direction);

        if (animator != null)
            animator.SetBool(IsWallHuggingHash, wallHug != null && wallHug.IsHugging);

        isWalking = direction.magnitude >= 0.1f;

        float currentSpeed = walkSpeed;

        if (isCrouching)
            currentSpeed = crouchSpeed * CrouchSpeedMultiplier;
        else if (isSprinting && !isSprintLocked)
            currentSpeed = Mathf.Lerp(walkSpeed, sprintSpeed, sprintBlend);

        // เร่งความเร็ววิ่งทีละนิด (Slow Run → Fast Run) ปล่อยปุ่ม/หยุดเดิน = กลับเป็น 0
        bool running = isWalking && isSprinting && !isSprintLocked && !isCrouching;
        float rate = sprintAccelerationTime > 0f ? Time.deltaTime / sprintAccelerationTime : 1f;
        sprintBlend = running ? Mathf.MoveTowards(sprintBlend, 1f, rate) : 0f;

        currentSpeed *= movementSpeedMultiplier;

        Vector3 velocity = direction * currentSpeed;

        // ทำงานแม้ยืนนิ่ง เพื่อรักษาระยะจากกำแพง
        if (wallHug != null)
            velocity += wallHug.GetDistanceCorrectionVelocity();

        Vector3 positionBeforeMove = transform.position;
        controller.Move(velocity * Time.deltaTime);

        // ท่าเดินพิงกำแพงดูจาก "ระยะที่ขยับได้จริง" ในเฟรมนี้ (ไม่ใช่ปุ่มที่กด):
        // เดินไปสุดกำแพง/ชนของขวางแล้วตัวไม่ขยับ → ค่าเป็น 0 → เล่นท่ายืนพิงแทนท่าเดิน
        Vector3 actualVelocity = Time.deltaTime > 0f
            ? (transform.position - positionBeforeMove) / Time.deltaTime
            : Vector3.zero;

        wallHugMoveAmount = wallHug != null && wallHug.IsHugging
            ? wallHug.GetWallMoveAmount(actualVelocity, currentSpeed)
            : 0f;

        // damp 0.1 วินาที: เบลนด์ซ้าย/นิ่ง/ขวานุ่มขึ้น ไม่กระตุกตอนค่ากระโดด
        if (animator != null)
            animator.SetFloat(WallHugMoveHash, wallHugMoveAmount, 0.1f, Time.deltaTime);

        controller.Move(Vector3.down * 9.81f * Time.deltaTime);

        // เสียงเดินต้องเรียก "หลัง" Move แรงโน้มถ่วง: isGrounded บอกผลของ Move ครั้งล่าสุดเท่านั้น
        // เดิมเรียกหลัง Move แนวราบ (ไม่มีแรงกดลงพื้น) ซึ่งได้ isGrounded = false 80–98% ของเฟรม
        // → ก้าวที่เท้าแตะพื้นตรงเฟรมนั้นถูกทิ้ง เสียงเดิน/วิ่ง (และเสียงที่ศัตรูได้ยิน) จึงหายเป็นช่วงๆ
        if (isWalking && playerAudio != null)
        {
            playerAudio.HandleFootstep(
                controller.isGrounded,
                isSprinting,
                isCrouching,
                currentSpeed);
        }
    }

    void HandleRotation(Vector3 rawDirection)
    {
        if (isAiming)
        {
            // ตอนเล็งปืนให้ใช้ทิศเล็งตามเดิม
            if (TryGetCrosshairLookDirection(out Vector3 lookDirection))
            {
                Quaternion aimTargetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    aimTargetRotation,
                    rotationSpeed * Time.deltaTime);
            }

            return;
        }

        Vector3 direction = rawDirection;

        // ตอนพิงให้หันออกจากกำแพง แม้ไม่ได้กำลังกดเดิน
        if (wallHug != null && wallHug.IsHugging)
            direction = wallHug.WallFacingDirection;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion moveTargetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            moveTargetRotation,
            rotationSpeed * Time.deltaTime);
    }
    public void SetTankControlMode(bool active)
    {
        // โหมด Hub (Base) ไม่มีการเข้าท่อ/กล้อง Vent
        isInVentMode = active && !hubMode;
    }

    private bool hubMode;

    /// <summary>อยู่ในโหมด Hub (พื้นที่ปลอดภัยอย่าง Base ที่ใช้กล้อง TPS หมุนอิสระ) หรือไม่</summary>
    public bool HubMode => hubMode;

    /// <summary>
    /// โหมด Hub: ใช้ Player ตัวเดิม (Prefab เดียวกับในด่าน) แต่ปิดระบบที่ไม่ใช้ใน Base
    ///   - พิงกำแพง + กล้อง WallHug   - กล้อง/การคุมแบบ Vent
    ///   - การใช้อาวุธทุกชนิด และแถบ/วงล้อเลือกอาวุธ (รวมการเลื่อนลูกกลิ้งเปลี่ยนอาวุธ)
    /// เรียกจาก ThirdPersonFollowCamera (กล้อง TPS ของ Base) ปิดโหมด = ทุกอย่างกลับมาทำงานตามเดิม
    /// </summary>
    public void SetHubMode(bool active)
    {
        hubMode = active;

        if (active)
            isInVentMode = false;

        // ปิดคอมโพเนนต์ = ระบบนั้นหยุดทำงานเอง (WallHugController.OnDisable เลิกพิง, WallHugCameraPan.OnDisable คืนกล้อง)
        WallHugController hug = wallHug != null ? wallHug : GetComponent<WallHugController>();
        if (hug != null)
            hug.enabled = !active;

        WallHugCameraPan hugCamera = GetComponent<WallHugCameraPan>();
        if (hugCamera != null)
            hugCamera.enabled = !active;

        PickupSystem pickup = GetComponent<PickupSystem>();
        if (pickup != null)
            pickup.SetWeaponsBlocked(active);
    }

    void HandleVentMovement()
    {
        Vector2 moveInput = Vector2.zero;
        if (moveAction != null) moveInput = moveAction.ReadValue<Vector2>();

        // A/D หมุนตัวผู้เล่น (หมุนกล้อง FPS ที่เป็นลูกไปด้วยในตัว เพราะเป็น Transform Hierarchy)
        float turn = moveInput.x * ventTurnSpeed * Time.deltaTime;
        transform.Rotate(0f, turn, 0f);

        // W/S เดินหน้า-ถอยหลังตามทิศที่หันอยู่ ณ ขณะนั้น
        Vector3 forwardMove = transform.forward * moveInput.y * ventMoveSpeed * Time.deltaTime;
        controller.Move(forwardMove);

        controller.Move(new Vector3(0, -9.81f * Time.deltaTime, 0));

        isWalking = Mathf.Abs(moveInput.y) >= 0.1f; // ใช้ร่วมกับ PlayAnima() ที่มีอยู่แล้ว
    }
    bool TryGetCrosshairLookDirection(out Vector3 lookDirection)
    {
        lookDirection = Vector3.zero;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null && aimCameraOverrideTransform == null)
            return false;

        Vector2 pointerPosition = GetPointerScreenPosition();
        Ray ray = GetAimRay(pointerPosition);
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        if (groundPlane.Raycast(ray, out float rayDistance))
        {
            Vector3 point = ray.GetPoint(rayDistance);
            lookDirection = point - transform.position;
        }
        else
        {
            lookDirection = ray.direction;
        }

        lookDirection.y = 0f;
        return lookDirection.sqrMagnitude > 0.001f;
    }

    Ray GetAimRay(Vector2 screenPosition)
    {
        if (aimCameraOverrideTransform != null)
        {
            float safeScreenHeight = Mathf.Max(1f, Screen.height);
            float safeScreenWidth = Mathf.Max(1f, Screen.width);
            float halfVerticalFov = aimCameraOverrideFov * Mathf.Deg2Rad * 0.5f;
            float halfHeight = Mathf.Tan(halfVerticalFov);
            float halfWidth = halfHeight * (safeScreenWidth / safeScreenHeight);

            float normalizedX = (screenPosition.x / safeScreenWidth - 0.5f) * 2f;
            float normalizedY = (screenPosition.y / safeScreenHeight - 0.5f) * 2f;
            Vector3 localDirection = new Vector3(normalizedX * halfWidth, normalizedY * halfHeight, 1f).normalized;
            Vector3 worldDirection = aimCameraOverrideTransform.TransformDirection(localDirection);

            return new Ray(aimCameraOverrideTransform.position, worldDirection);
        }

        return mainCamera.ScreenPointToRay(screenPosition);
    }

    Vector2 GetPointerScreenPosition()
    {
        if (pointerAction == null)
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Vector2 pointerPosition = pointerAction.ReadValue<Vector2>();
        return new Vector2(
            Mathf.Clamp(pointerPosition.x, 0f, Screen.width),
            Mathf.Clamp(pointerPosition.y, 0f, Screen.height));
    }

    void AttemptVault()
    {
        Vector3 horizontalForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        Vector3 lowRayOrigin = transform.position + (Vector3.up * lowRayHeight);
        Vector3 highRayOrigin = transform.position + (Vector3.up * highRayHeight);

        bool hitLow = Physics.Raycast(lowRayOrigin, horizontalForward, out RaycastHit hit, raycastDistance, hurdleLayer);
        bool hitHigh = Physics.Raycast(highRayOrigin, horizontalForward, raycastDistance, hurdleLayer);

        if (hitLow && !hitHigh)
        {
            Vector3 vaultDirection = -hit.normal;
            vaultDirection.y = 0f;
            vaultDirection.Normalize();

            Vector3 landingSpot = transform.position + (vaultDirection * vaultDistance);
            Vector3 landingRayOrigin = landingSpot + (Vector3.up * highRayHeight);

            bool hitThickWall = Physics.Raycast(landingRayOrigin, Vector3.down, highRayHeight, hurdleLayer);
            if (!hitThickWall)
            {
                transform.rotation = Quaternion.LookRotation(vaultDirection);
                SfxLibrary.Play("vault");
                StartCoroutine(VaultRoutine(landingSpot));
            }
        }
    }

    IEnumerator VaultRoutine(Vector3 targetPosition)
    {
        isVaulting = true;

        controller.height = originalHeight * 0.5f;
        controller.center = originalCenter + new Vector3(0, originalHeight * 0.25f, 0);

        Vector3 startPos = transform.position;
        float timePassed = 0f;

        while (timePassed < 1f)
        {
            timePassed += Time.deltaTime / vaultDuration;

            Vector3 lerpedPos = Vector3.Lerp(startPos, targetPosition, timePassed);
            float heightOffset = Mathf.Sin(Mathf.Clamp01(timePassed) * Mathf.PI) * vaultHeight;

            Vector3 targetFramePos = lerpedPos;
            targetFramePos.y += heightOffset;

            Vector3 delta = targetFramePos - transform.position;
            controller.Move(delta);

            yield return null;
        }

        Vector3 finalDelta = targetPosition - transform.position;
        controller.Move(finalDelta);

        controller.height = originalHeight;
        controller.center = originalCenter;

        isVaulting = false;
    }

    // ------------------------------------------------------------ ปีนบันได
    // หลักการ: ตำแหน่งตอนปีนคำนวณจาก "ตัวบันไดจริง" (Ladder.GetClimbPosition) ทุกเฟรม
    //   แนวราบล็อกไว้กึ่งกลางบันได ห่างหน้าบันไดเท่า Stand Distance / แนวตั้งเลื่อนตามปุ่ม จำกัดไม่เกินยอดบันได
    // เดิมใช้จุดเริ่มปีน + ชนบันไดด้วยรัศมีตัว + Ray หาขอบบน ทำให้ตัวลอยห่าง ไม่ตรงกลาง และออกด้านบนซ้อนหลายครั้ง

    private Ladder climbingLadder;
    private Collider climbingLadderCollider;
    private bool isExitingLadderTop;
    private float ladderLandingY = float.NaN; // ความสูงพื้นด้านบนบันได (หาไว้ตอนเริ่มปีน)

    /// <summary>เริ่ม/เลิกปีน (กด E ที่บันไดซ้ำ = ปล่อยบันได)</summary>
    public void StartClimbing(Vector3 lookDirection, Transform startPoint)
    {
        StartClimbing(lookDirection, startPoint, null);
    }

    public void StartClimbing(Vector3 lookDirection, Transform startPoint, Ladder ladder)
    {
        if (isClimbing) { StopClimbing(); return; }
        if (climbCooldown > 0f) return;

        isClimbing = true;
        isExitingLadderTop = false;
        climbingLadder = ladder;
        transform.rotation = Quaternion.LookRotation(ladder != null ? ladder.ClimbFacing : lookDirection);

        // ระหว่างปีน ไม่ให้ CharacterController ชนตัวบันได (ไม่งั้นรัศมีตัวผู้เล่นดันออกห่างบันไดเสมอ)
        if (ladder != null && ladder.ClimbCollider != null)
        {
            climbingLadderCollider = ladder.ClimbCollider;
            Physics.IgnoreCollision(controller, climbingLadderCollider, true);
        }

        if (animator != null) animator.SetBool(IsClimbingHash, true);

        // พื้นด้านบนที่จะก้าวขึ้นไปยืน: เลยหน้าบันไดไป Top Exit Offset ที่ความสูงยอดบันได
        ladderLandingY = float.NaN;
        if (ladder != null)
        {
            Vector3 probe = ladder.GetClimbPosition(ladder.TopY + 2f) + ladder.ClimbFacing * topExitOffset;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit landing, 4f, ~ladderLayer, QueryTriggerInteraction.Ignore))
                ladderLandingY = landing.point.y;
        }

        Vector3 entry = startPoint != null ? startPoint.position : transform.position;
        if (ladder != null)
            entry = ladder.GetClimbPosition(Mathf.Clamp(entry.y, MinClimbCenterY(ladder), MaxClimbCenterY(ladder)));

        StartCoroutine(SmoothClimbEntryRoutine(entry));
    }

    // ช่วงที่จุดกลางตัวเลื่อนได้บนบันได: เท้าแตะพื้นล่าง ถึง เท้าอยู่ที่ยอดบันได (+ Top Reach Offset)
    float MinClimbCenterY(Ladder ladder) => ladder.BottomY + FeetToCenter;
    float MaxClimbCenterY(Ladder ladder) => ladder.TopY + ladder.topReachOffset + FeetToCenter;

    /// <summary>ความสูงเท้าของผู้เล่น (ตัว Pivot อาจอยู่กลางตัว)</summary>
    float FeetY => transform.position.y + controller.center.y - controller.height * 0.5f;

    IEnumerator SmoothClimbEntryRoutine(Vector3 targetPos)
    {
        SetMovementLocked(this, true);

        Vector3 startPos = transform.position;
        float timePassed = 0f;
        float duration = 0.25f;

        while (timePassed < 1f)
        {
            timePassed += Time.deltaTime / duration;
            SetPositionDirect(Vector3.Lerp(startPos, targetPos, Mathf.Clamp01(timePassed)));
            yield return null;
        }

        SetPositionDirect(targetPos);
        SetMovementLocked(this, false);
    }

    // ย้ายตัวตรงๆ (ปิด CharacterController ชั่วคราว ไม่ให้ชนอะไรระหว่างจัดตำแหน่งบนบันได)
    void SetPositionDirect(Vector3 position)
    {
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.position = position;
        controller.enabled = wasEnabled;
    }

    // เลิกปีน: ให้ชนบันไดได้ตามปกติอีกครั้ง
    void RestoreLadderCollision()
    {
        if (climbingLadderCollider != null && controller != null)
            Physics.IgnoreCollision(controller, climbingLadderCollider, false);

        climbingLadderCollider = null;
    }

    // บันไดที่ไม่มีคอมโพเนนต์ Ladder (เก่า): ดึงตัวเข้าหาหน้าบันไดด้วย Ray แบบเดิม
    void SnapToLadderByRay()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return;
        forward.Normalize();

        Vector3 origin = transform.position + Vector3.up * ladderSnapRayHeight - forward * 0.5f;
        if (!Physics.Raycast(origin, forward, out RaycastHit hit, 3f, ladderLayer, QueryTriggerInteraction.Ignore))
            return;

        if (climbingLadderCollider != hit.collider)
        {
            RestoreLadderCollision();
            climbingLadderCollider = hit.collider;
            Physics.IgnoreCollision(controller, climbingLadderCollider, true);
        }

        Vector3 target = hit.point - forward * ladderSnapDistance;
        target.y = transform.position.y;
        SetPositionDirect(target);
    }

    void HandleClimbing()
    {
        if (isExitingLadderTop)
            return; // กำลังก้าวขึ้นจากยอดบันได

        Vector2 moveInput = Vector2.zero;
        if (moveAction != null) moveInput = moveAction.ReadValue<Vector2>();

        float currentClimbSpeed = moveInput.y < 0 ? climbDownSpeed : climbUpSpeed;
        float deltaY = moveInput.y * currentClimbSpeed * Time.deltaTime;

        if (climbingLadder != null)
        {
            // แนวตั้งเลื่อนตามปุ่ม (จำกัดระหว่างพื้นล่างถึงยอดบันได) / แนวราบล็อกกึ่งกลางบันไดทุกเฟรม
            float y = Mathf.Clamp(transform.position.y + deltaY, MinClimbCenterY(climbingLadder), MaxClimbCenterY(climbingLadder));
            SetPositionDirect(climbingLadder.GetClimbPosition(y));
            transform.rotation = Quaternion.LookRotation(climbingLadder.ClimbFacing);
        }
        else
        {
            controller.Move(new Vector3(0f, deltaY, 0f));
            SnapToLadderByRay();
        }

        // ความเร็วอนิเมชั่นตามความเร็วปีนจริง (มือ/เท้าไม่ไถลบนขั้นบันได) กดลง = เล่นถอยหลัง, ไม่กด = ค้างท่า
        if (animator != null)
        {
            float animSpeed = climbAnimMetersPerSecond > 0f ? currentClimbSpeed / climbAnimMetersPerSecond : 1f;
            animator.SetFloat(ClimbSpeedHash, moveInput.y * Mathf.Min(animSpeed, maxClimbAnimSpeed));
        }

        CheckLadderExit(moveInput.y, moveInput.x);
    }

    /// <summary>ระยะจากเท้าถึง Pivot ของผู้เล่น</summary>
    float FeetToCenter => transform.position.y - FeetY;

    void CheckLadderExit(float verticalInput, float horizontalInput)
    {
        // ล่างสุด: เท้าถึงพื้นล่างของบันได แล้วกดลงหรือกดซ้าย/ขวา = ลงจากบันได
        bool isAtBottom = climbingLadder != null
            ? transform.position.y <= MinClimbCenterY(climbingLadder) + 0.02f
            : controller.isGrounded ||
              Physics.Raycast(new Vector3(transform.position.x, FeetY + 0.1f, transform.position.z), Vector3.down, 0.25f, ~ladderLayer, QueryTriggerInteraction.Ignore);

        if (isAtBottom && (verticalInput < 0 || Mathf.Abs(horizontalInput) > 0.1f))
        {
            StopClimbing();
            return;
        }

        if (verticalInput <= 0f || isExitingLadderTop)
            return;

        bool reachedTop;
        if (climbingLadder != null)
        {
            // ถึงยอด: ตัวขึ้นไปถึงตำแหน่งสูงสุดที่บันไดอนุญาตแล้ว
            reachedTop = transform.position.y >= MaxClimbCenterY(climbingLadder) - 0.01f;

            // มีท่าปีนขึ้นขอบ: เริ่มท่าตอนมือถึงขอบพื้นด้านบน (เท้าต่ำกว่าพื้นด้านบน Top Exit Start Below Floor)
            if (!reachedTop && !float.IsNaN(ladderLandingY) && TopExitClipLength() > 0f)
                reachedTop = FeetY >= ladderLandingY - topExitStartBelowFloor;
        }
        else
        {
            Vector3 rayOrigin = transform.position + (Vector3.up * highRayHeight);
            reachedTop = !Physics.Raycast(rayOrigin, transform.forward, 1.2f, ladderLayer, QueryTriggerInteraction.Ignore);
        }

        if (reachedTop)
            StartCoroutine(LadderTopExitRoutine());
    }

    void StopClimbing()
    {
        isClimbing = false;
        isExitingLadderTop = false;
        climbingLadder = null;
        ladderLandingY = float.NaN;
        RestoreLadderCollision();
        climbCooldown = 0.5f;
        if (animator != null)
        {
            animator.SetBool(IsClimbingHash, false);
            animator.SetFloat(ClimbSpeedHash, 0f);
        }
    }

    private static readonly int ClimbExitSpeedHash = Animator.StringToHash("ClimbExitSpeed");

    // ความยาวคลิปท่าปีนขึ้นขอบใน Animator ของผู้เล่น (0 = ไม่มีคลิป)
    float TopExitClipLength()
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(topExitClipName))
            return 0f;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == topExitClipName)
                return clip.length;
        }

        return 0f;
    }

    bool HasAnimatorParameter(int hash)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == hash) return true;
        return false;
    }

    // ก้าวขึ้นจากยอดบันไดไปยืนบนพื้นด้านบน (เริ่มได้ครั้งเดียวต่อการปีน)
    // มีคลิป MigeClimbingEdge: เล่นท่าเอาแขนค้ำขอบแล้วดันตัวขึ้น และเลื่อนตัวตามจังหวะท่า (ยกตัวช่วงแรก → เดินหน้าช่วงหลัง)
    IEnumerator LadderTopExitRoutine()
    {
        isExitingLadderTop = true;
        SetMovementLocked(this, true);

        float clipLength = TopExitClipLength();
        bool useEdgeClip = clipLength > 0f;

        if (animator != null)
        {
            if (useEdgeClip && HasAnimatorParameter(ClimbExitSpeedHash))
                animator.SetFloat(ClimbExitSpeedHash, topExitAnimSpeed);

            animator.SetTrigger(ClimbTopExitHash);
            // ปิด IsClimbing ทันที ไม่ให้ Any State → Climb ดึงกลับไปท่าปีนระหว่างเล่นท่าขึ้นขอบ
            animator.SetBool(IsClimbingHash, false);
        }

        Vector3 startPos = transform.position;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        // จุดยืนด้านบน: เลยหน้าบันไดไป Top Exit Offset แล้วหาพื้นข้างใต้ / ไม่เจอพื้น = ใช้ความสูงคงที่แบบเดิม
        Vector3 landing = startPos + forward * topExitOffset;
        Vector3 targetPos = startPos + Vector3.up * 1.2f + forward * topExitOffset;
        if (Physics.Raycast(landing + Vector3.up * 3f, Vector3.down, out RaycastHit floor, 6f, ~ladderLayer, QueryTriggerInteraction.Ignore))
            targetPos = new Vector3(landing.x, floor.point.y + FeetToCenter + 0.05f, landing.z);

        // ขึ้นก่อนแล้วค่อยเดินหน้า (ไม่ให้ตัวชนขอบพื้นด้านบน)
        float duration = useEdgeClip ? clipLength / Mathf.Max(0.1f, topExitAnimSpeed) : 0.5f;

        // จังหวะจากท่า MigeClimbingEdge (วัดจากคลิป): ตัวยกขึ้นช่วง 10–55% ของท่า, เดินหน้าช่วง 20–95%
        // ไม่มีคลิป: ยกครึ่งแรก เดินหน้าครึ่งหลังแบบเดิม
        Vector2 riseRange = useEdgeClip ? new Vector2(0.10f, 0.55f) : new Vector2(0f, 0.5f);
        Vector2 forwardRange = useEdgeClip ? new Vector2(0.20f, 0.95f) : new Vector2(0.5f, 1f);
        Vector3 flatStart = new Vector3(startPos.x, 0f, startPos.z);
        Vector3 flatTarget = new Vector3(targetPos.x, 0f, targetPos.z);
        float riseTo = Mathf.Max(startPos.y, targetPos.y);

        for (float time = 0f; time < 1f; time += Time.deltaTime / duration)
        {
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(riseRange.x, riseRange.y, time));
            float move = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(forwardRange.x, forwardRange.y, time));

            // ยังไม่สูงพอพ้นขอบพื้น = ห้ามเดินหน้า (กันตัวทะลุขอบ)
            if (rise < 0.98f)
                move = Mathf.Min(move, 0.35f);

            Vector3 flat = Vector3.Lerp(flatStart, flatTarget, move);
            float y = Mathf.Lerp(startPos.y, riseTo, rise);
            if (move > 0.99f) y = targetPos.y;
            SetPositionDirect(new Vector3(flat.x, y, flat.z));
            yield return null;
        }

        SetPositionDirect(targetPos);
        StopClimbing();
        SetMovementLocked(this, false);
    }

    private void PlayAnima()
    {
        if (animator == null)
            return;

        animator.SetBool(IsWalkHash, isWalking);
        animator.SetBool(IsSneakHash, isCrouching);

        // ท่าวิ่ง: ส่งค่าเฉพาะเมื่อ Animator มีพารามิเตอร์ (ไม่งั้น Unity เตือนทุกเฟรม)
        if (!checkedRunParams)
        {
            checkedRunParams = true;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.nameHash == RunBlendHash) hasRunParams = true;
        }

        if (hasRunParams)
        {
            animator.SetBool(IsRunHash, isWalking && isSprinting && !isSprintLocked && !isCrouching);
            animator.SetFloat(RunBlendHash, sprintBlend, 0.1f, Time.deltaTime);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 horizontalForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position + (Vector3.up * lowRayHeight), horizontalForward * raycastDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + (Vector3.up * highRayHeight), horizontalForward * raycastDistance);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Vector3 testLandingSpot = transform.position + (horizontalForward * vaultDistance);
            Vector3 testLandingOrigin = testLandingSpot + (Vector3.up * highRayHeight);

            Gizmos.DrawRay(testLandingOrigin, Vector3.down * highRayHeight);
            Gizmos.DrawSphere(testLandingSpot, 0.1f);
        }
    }
}