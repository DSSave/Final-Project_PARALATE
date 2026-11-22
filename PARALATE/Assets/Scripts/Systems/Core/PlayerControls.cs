using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ทางเดียวในการอ่านปุ่มของผู้เล่น: ทุกปุ่มอยู่ใน Assets/Scripts/PlayerInput.inputactions (map "Player")
/// เปลี่ยนปุ่ม = แก้ในไฟล์นั้นที่เดียว ไม่ต้องแก้โค้ด
///
/// สคริปต์ที่อยู่บนตัวผู้เล่นอ่านจาก PlayerInput ของตัวเองได้เลย
/// สคริปต์ที่ไม่ได้อยู่บนผู้เล่น (เมนู Esc, หน้า Upgrade, หน้าเลือกด่าน) ใช้คลาสนี้ ซึ่งหา PlayerInput ของผู้เล่นใน Scene ให้
/// ถ้า Scene ไม่มีผู้เล่น/ไม่มี action ชื่อนั้น จะใช้ปุ่มสำรอง (fallback) แทน เกมจึงไม่พังระหว่างแก้ไฟล์ปุ่ม
/// </summary>
public static class PlayerControls
{
    public const string Pause = "Pause";           // Esc: เมนูหยุดเกม / ปิดหน้าต่าง
    public const string Inventory = "Inventory";   // B: เปิดเมนูที่หน้า Inventory
    public const string EyeWide = "EyeWide";       // T: เข้า/ออก Eye Wide
    public const string Zoom = "Zoom";             // ลูกกลิ้ง: ซูม Eye Wide
    public const string CameraLook = "CameraLook"; // เมาส์ขยับ / right stick: หมุนกล้อง
    public const string Command = "Command";       // Q: เปิด/ยืนยัน CommandLine (ใส่ลูกศรด้วยปุ่มเดิน WASD)

    private static PlayerInput cached;
    private static int lastSearchFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cached = null;
        lastSearchFrame = -1;
    }

    /// <summary>PlayerInput ของผู้เล่นใน Scene ปัจจุบัน (ค้นใหม่ไม่เกินเฟรมละครั้ง)</summary>
    private static PlayerInput Player
    {
        get
        {
            if (cached != null)
                return cached;

            if (lastSearchFrame == Time.frameCount)
                return null;

            lastSearchFrame = Time.frameCount;
            TopDownPlayerController player = Object.FindAnyObjectByType<TopDownPlayerController>();
            cached = player != null ? player.GetComponent<PlayerInput>() : null;
            return cached;
        }
    }

    /// <summary>หา action ตามชื่อ: ใช้ PlayerInput ที่ส่งมาก่อน ไม่งั้นใช้ของผู้เล่นใน Scene</summary>
    public static InputAction Find(string name, PlayerInput source = null)
    {
        PlayerInput input = source != null ? source : Player;
        return input != null && input.actions != null ? input.actions.FindAction(name, false) : null;
    }

    public static bool WasPressed(string name, Key fallback, PlayerInput source = null)
    {
        InputAction action = Find(name, source);
        if (action != null)
            return action.WasPressedThisFrame();

        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[fallback].wasPressedThisFrame;
    }

    /// <summary>
    /// ค่าหมุนกล้องเฟรมนี้ (องศา) จาก action CameraLook
    ///   เมาส์ = ระยะที่ขยับเป็นพิกเซล x mouseScale / จอยสติ๊ก = ค่า -1..1 x stickDegreesPerSecond x เวลาเฟรม
    /// </summary>
    public static Vector2 ReadLook(float mouseScale, float stickDegreesPerSecond = 140f, PlayerInput source = null)
    {
        InputAction action = Find(CameraLook, source);
        if (action == null)
            return Mouse.current != null ? Mouse.current.delta.ReadValue() * mouseScale : Vector2.zero;

        Vector2 value = action.ReadValue<Vector2>();
        bool stick = action.activeControl != null && action.activeControl.device is Gamepad;
        return stick ? value * (stickDegreesPerSecond * Time.unscaledDeltaTime) : value * mouseScale;
    }

    /// <summary>ลูกกลิ้งเฟรมนี้ (บวก = เลื่อนขึ้น) จาก action Zoom</summary>
    public static float ReadZoom(PlayerInput source = null)
    {
        InputAction action = Find(Zoom, source);
        if (action != null)
            return action.ReadValue<float>();

        return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
    }
}
