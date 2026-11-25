using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// กล้องบุคคลที่สามแบบหมุนอิสระ (ใช้ที่ Base): ตัวละครอยู่กลางจอ ขยับเมาส์เพื่อหมุนกล้องรอบตัวละครได้ทุกทิศ
/// ตัวละครเดินตามทิศของกล้อง (TopDownPlayerController.useCameraRelativeMovement) กล้องไม่หมุนตามตัวละครอีกแล้ว
///
/// หลักการ (Orbit Camera):
///   1) เก็บมุม yaw / pitch ของกล้องเอง บวกค่าจากเมาส์ทุกเฟรม
///   2) จุดหมุน (pivot) = ตำแหน่งผู้เล่น + ความสูงระดับไหล่ ไล่ตามผู้เล่นแบบนุ่ม (SmoothDamp)
///   3) ตำแหน่งกล้อง = pivot ถอยหลังตามมุม yaw/pitch เป็นระยะ distance ถ้ามีกำแพงขวางให้ขยับเข้ามาใกล้
///   4) เมาส์ถูกล็อกกลางจอระหว่างเดิน และปล่อยเองเมื่อผู้เล่นถูกล็อก (เปิดเมนู / เลือกด่าน / Upgrade)
/// คลิกขวา (เล็ง) ถูกปิดผ่าน TopDownPlayerController.SetAimBlocked
/// </summary>
public class ThirdPersonFollowCamera : MonoBehaviour
{
    [Header("ผู้เล่นที่จะติดตาม")]
    public Transform player;

    [Header("มุมกล้อง (Third Person)")]
    [Tooltip("มุมก้มเริ่มต้น (องศา) ค่ามาก = กล้องอยู่สูงมองลง")]
    [Range(-30f, 80f)]
    public float pitch = 20f;
    public float distance = 8f;
    public float heightOffset = 1.6f;

    [Header("หมุนกล้องด้วยเมาส์")]
    [Tooltip("เปิด = หมุนกล้องอิสระด้วยเมาส์ / ปิด = กล้องหมุนตามหลังตัวละครแบบเดิม")]
    public bool freeLook = true;
    [Tooltip("องศาต่อการขยับเมาส์ 1 พิกเซล")]
    public float mouseSensitivity = 0.12f;
    [Tooltip("องศาต่อวินาทีเมื่อใช้ก้านขวาของจอย")]
    public float gamepadSensitivity = 140f;
    public float minPitch = -15f;
    public float maxPitch = 65f;
    public bool invertY = false;
    [Tooltip("ปิดการเล็ง (คลิกขวา) ขณะใช้กล้องนี้")]
    public bool blockAiming = true;

    [Header("ความนุ่มนวล")]
    public float positionSmoothTime = 0.12f;

    [Header("ความหน่วงของการหมุนตาม (ใช้เมื่อปิด Free Look)")]
    [Tooltip("ยิ่งค่าน้อย ยิ่งหน่วง/ตามช้า ยิ่งค่ามาก ยิ่งตามไว")]
    public float yawSmoothTime = 0.25f;

    [Header("กันกล้องทะลุกำแพง")]
    public LayerMask wallLayer;
    public float minDistance = 2.5f;
    public float skin = 0.2f;
    [Tooltip("ความเร็วที่กล้องถอยกลับไประยะปกติหลังพ้นกำแพง (เมตร/วินาที) ตอนชนกำแพงจะขยับเข้าทันที")]
    public float distanceRecoverSpeed = 6f;

    [Header("โหมด Hub (Base)")]
    [Tooltip("ตอนใช้กล้องนี้: ปิดพิงกำแพง + กล้อง WallHug, กล้อง Vent, การใช้อาวุธ และแถบ/ลูกกลิ้งเลือกอาวุธ ของ Player")]
    public bool hubMode = true;

    private TopDownPlayerController controller;
    private PlayerInput playerInput;
    private Vector3 pivotVelocity;
    private Vector3 smoothedPivot;
    private float yawVelocity;
    private float currentYaw;
    private float currentPitch;
    private float currentDistance;
    private bool hadControlLastFrame;

    void Start()
    {
        if (player == null)
            return;

        controller = player.GetComponent<TopDownPlayerController>();
        playerInput = player.GetComponent<PlayerInput>();
        currentYaw = player.eulerAngles.y;
        currentPitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        currentDistance = distance;
        smoothedPivot = player.position + Vector3.up * heightOffset;

        ApplyAimBlock(true);
    }

    void OnEnable()
    {
        ApplyAimBlock(true);
    }

    void OnDisable()
    {
        ApplyAimBlock(false);
    }

    void ApplyAimBlock(bool blocked)
    {
        if (controller == null)
            return;

        controller.SetAimBlocked(this, blocked && blockAiming && freeLook);

        // กล้อง TPS หมุนอิสระ = พื้นที่ Hub: ปิดพิงกำแพง / กล้อง Vent / อาวุธ / แถบเลือกอาวุธ ของ Player
        controller.SetHubMode(blocked && hubMode && freeLook);
    }

    void LateUpdate()
    {
        if (player == null) return;

        if (freeLook)
            UpdateFreeLook();
        else
            currentYaw = Mathf.SmoothDampAngle(currentYaw, player.eulerAngles.y, ref yawVelocity, yawSmoothTime);

        Vector3 targetPivot = player.position + Vector3.up * heightOffset;
        smoothedPivot = Vector3.SmoothDamp(smoothedPivot, targetPivot, ref pivotVelocity, positionSmoothTime);

        Vector3 offsetDir = CalculateOffsetDirection(currentPitch, currentYaw);

        // กันทะลุกำแพง: ชนแล้วขยับเข้าทันที พ้นแล้วค่อยๆ ถอยกลับ (ไม่กระตุกเวลาหมุนผ่านเสา)
        float safeDist = distance;
        if (Physics.SphereCast(smoothedPivot, skin, offsetDir, out RaycastHit hit, distance, wallLayer, QueryTriggerInteraction.Ignore))
            safeDist = Mathf.Max(hit.distance, minDistance);

        currentDistance = safeDist < currentDistance
            ? safeDist
            : Mathf.MoveTowards(currentDistance, safeDist, distanceRecoverSpeed * Time.deltaTime);

        transform.position = smoothedPivot + offsetDir * currentDistance;
        transform.rotation = Quaternion.LookRotation(-offsetDir);
    }

    void UpdateFreeLook()
    {
        // หมุนได้เฉพาะตอนผู้เล่นคุมตัวละครอยู่ (ไม่ได้เปิดเมนู / เลือกด่าน / Upgrade / Pause)
        bool hasControl = Time.timeScale > 0f && Application.isFocused &&
                          (controller == null || !controller.IsMovementLocked);

        if (!hasControl)
        {
            hadControlLastFrame = false;
            return;
        }

        // ล็อกเมาส์กลางจอ (หน้าต่างอื่นปลดล็อกเองตอนเปิด และเราล็อกกลับเมื่อได้การควบคุมคืน)
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // เฟรมแรกหลังได้การควบคุมคืน ข้ามค่าเมาส์ (กันกล้องสะบัดจากระยะที่เมาส์ขยับตอนอยู่ในเมนู)
        if (!hadControlLastFrame)
        {
            hadControlLastFrame = true;
            return;
        }

        // CameraLook ใน PlayerInput.inputactions (เมาส์ขยับ / right stick) ค่าที่ได้เป็นองศาแล้ว
        Vector2 look = PlayerControls.ReadLook(mouseSensitivity, gamepadSensitivity, playerInput);

        currentYaw += look.x;
        currentPitch = Mathf.Clamp(currentPitch + (invertY ? look.y : -look.y), minPitch, maxPitch);
    }

    Vector3 CalculateOffsetDirection(float pitchAngle, float yaw)
    {
        float pitchRad = pitchAngle * Mathf.Deg2Rad;
        float height = Mathf.Sin(pitchRad);
        float horizontal = Mathf.Cos(pitchRad);

        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 horizontalDir = yawRotation * Vector3.back;

        return (horizontalDir * horizontal + Vector3.up * height).normalized;
    }
}
