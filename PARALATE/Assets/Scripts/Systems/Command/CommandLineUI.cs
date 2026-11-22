using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// CommandLine: กด Q เปิดแถบใส่คำสั่ง (มุมขวาล่าง เหนือ WeaponWheel) → กด W A S D ใส่ลูกศร → กด Q อีกครั้งเพื่อยืนยัน
///   ตรงสูตร  : ไอเทม/ปืนเข้าช่อง Command ของ WeaponWheel และถือขึ้นมาทันที (SaveGame = เปิดหน้า Save & Load)
///   ไม่ตรง   : แถบกะพริบแดง ล้างลูกศรให้ใส่ใหม่ / กด Q ตอนยังไม่ใส่ลูกศร = ปิด / Esc = ยกเลิก
/// ระหว่างใส่คำสั่ง ผู้เล่นหยุดเดิน (ปุ่ม WASD ถูกใช้เป็นลูกศร) / กด Tab ค้างอยู่ (เปิด WeaponWheel) จะกด Q ไม่ได้
/// สูตรทั้งหมดอยู่ใน Resources/CommandBook (ดู CommandBook)
/// หน้าตาอยู่ใน Prefab Resources/UI/CommandLine (CommandLineView) จัดตำแหน่ง/สีใน Editor ได้
/// คอมโพเนนต์นี้ถูกเพิ่มให้ผู้เล่นอัตโนมัติ (PlayerSystems)
/// </summary>
[DisallowMultipleComponent]
public class CommandLineUI : MonoBehaviour
{
    [Tooltip("ค่าแกนขั้นต่ำของปุ่มเดินที่นับเป็น 1 ลูกศร")]
    public float axisThreshold = 0.5f;

    /// <summary>กำลังใส่คำสั่งอยู่ (ระบบอื่นใช้กันไม่ให้ทำงานซ้อน เช่น WeaponWheel / คลิกซ้ายใช้ของ)</summary>
    public static bool IsTyping { get; private set; }

    private TopDownPlayerController player;
    private PickupSystem pickup;
    private WeaponWheelUI wheel;
    private KnockoutSystem knockout;
    private InputAction commandAction;
    private InputAction moveAction;
    private InputAction wheelAction;

    private readonly List<CommandArrow> input = new List<CommandArrow>();
    private bool isOpen;
    private bool axisHeld;
    private float flashUntil;
    private Color flashColor;
    private float messageUntil;

    private CommandLineView view;
    private GameObject root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsTyping = false;

    private void Awake()
    {
        player = GetComponent<TopDownPlayerController>();
        pickup = GetComponent<PickupSystem>();
        wheel = GetComponent<WeaponWheelUI>();
        knockout = GetComponent<KnockoutSystem>();

        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.actions != null)
        {
            commandAction = playerInput.actions.FindAction(PlayerControls.Command, false);
            moveAction = playerInput.actions.FindAction("Movement", false);
            wheelAction = playerInput.actions.FindAction("WeaponWheel", false);
        }
    }

    private void OnDisable()
    {
        if (isOpen)
            Close();
    }

    private void OnDestroy()
    {
        if (root != null)
            Destroy(root);
    }

    private void Update()
    {
        UpdateFeedback();

        bool pressed = commandAction != null ? commandAction.WasPressedThisFrame()
            : Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;

        if (!isOpen)
        {
            if (pressed && CanOpen())
                Open();
            return;
        }

        // ยกเลิก: Esc / ถูกล็อกโดยอย่างอื่น (เช่น โดนรัดคอ, ตาย)
        if (PlayerControls.WasPressed(PlayerControls.Pause, Key.Escape) || PlayerHealth.PlayerIsDead || Time.timeScale <= 0f)
        {
            Close();
            return;
        }

        ReadArrow();

        if (pressed)
            Submit();
    }

    private bool CanOpen()
    {
        if (Time.timeScale <= 0f || PlayerHealth.PlayerIsDead)
            return false;

        if (pickup != null && pickup.WeaponsBlocked)
            return false; // โหมด Hub

        // กด Tab ค้างอยู่ (เปิด WeaponWheel) = กด Q ไม่ได้
        if ((wheel != null && wheel.IsOpen) || (wheelAction != null && wheelAction.IsPressed()))
            return false;

        if (knockout != null && knockout.IsKnockout)
            return false;

        return player == null || !player.IsMovementLocked;
    }

    private void Open()
    {
        EnsureUI();
        isOpen = true;
        IsTyping = true;
        input.Clear();
        axisHeld = moveAction != null && moveAction.ReadValue<Vector2>().sqrMagnitude > axisThreshold * axisThreshold;

        if (player != null)
            player.SetMovementLocked(this, true);

        root.SetActive(true);
        RefreshArrows();
        SfxLibrary.PlayUI("menu_open");
    }

    /// <param name="playSound">ปิดแบบยกเลิก (Q ตอนว่าง / Esc) = มีเสียงปิด / ใช้สูตรสำเร็จมีเสียงสำเร็จแทนแล้ว</param>
    private void Close(bool playSound = true)
    {
        if (playSound && isOpen)
            SfxLibrary.PlayUI("menu_close");

        isOpen = false;
        IsTyping = false;
        input.Clear();

        if (player != null)
            player.SetMovementLocked(this, false);

        // ข้อความผลลัพธ์ยังค้างให้อ่านได้สักครู่ ซ่อนทั้งแถบเมื่อหมดเวลา (UpdateFeedback)
        if (root != null && Time.unscaledTime >= messageUntil)
            root.SetActive(false);
    }

    // อ่านปุ่มเดินเป็นลูกศร: นับเฉพาะตอนกดใหม่ (ต้องปล่อยปุ่มก่อนถึงจะนับลูกศรถัดไป)
    private void ReadArrow()
    {
        if (moveAction == null)
            return;

        Vector2 axis = moveAction.ReadValue<Vector2>();
        bool active = axis.sqrMagnitude > axisThreshold * axisThreshold;

        if (!active)
        {
            axisHeld = false;
            return;
        }

        if (axisHeld)
            return;

        axisHeld = true;

        CommandArrow arrow = Mathf.Abs(axis.x) > Mathf.Abs(axis.y)
            ? (axis.x > 0f ? CommandArrow.Right : CommandArrow.Left)
            : (axis.y > 0f ? CommandArrow.Up : CommandArrow.Down);

        if (input.Count >= CommandBook.MaxLength)
        {
            SfxLibrary.PlayUI("ui_error");
            return;
        }

        input.Add(arrow);
        RefreshArrows();
        SfxLibrary.PlayUI("wheel_tick", true);
    }

    private void Submit()
    {
        if (input.Count == 0)
        {
            Close();
            return;
        }

        CommandRecipe recipe = CommandBook.Match(input);
        if (recipe == null)
        {
            Fail("Unknown command");
            return;
        }

        if (recipe.kind == CommandKind.OpenSaveMenu)
        {
            Succeed(recipe.displayName);
            Close(false);
            SaveLoadUI.Open();
            return;
        }

        if (pickup == null)
        {
            Fail("No pickup system");
            return;
        }

        if (!pickup.TryLoadCommand(recipe, out string reason))
        {
            Fail(reason);
            return;
        }

        Succeed(recipe.displayName);
        Close(false);
    }

    private void Succeed(string text)
    {
        SfxLibrary.PlayUI("upgrade_success");
        ShowMessage(text, view != null ? view.successColor : Color.cyan);
    }

    private void Fail(string text)
    {
        SfxLibrary.PlayUI("ui_error");
        input.Clear();
        RefreshArrows();
        ShowMessage(text, view != null ? view.errorColor : Color.red);
    }

    private void ShowMessage(string text, Color color)
    {
        flashColor = color;
        flashUntil = Time.unscaledTime + 0.35f;
        messageUntil = Time.unscaledTime + 1.6f;

        if (view != null && view.message != null)
        {
            view.message.text = text;
            view.message.gameObject.SetActive(true);
        }
    }

    private void UpdateFeedback()
    {
        if (root == null)
            return;

        if (view.message != null && view.message.gameObject.activeSelf && Time.unscaledTime >= messageUntil)
        {
            view.message.gameObject.SetActive(false);
            if (!isOpen)
                root.SetActive(false);
        }

        if (view.panel != null)
        {
            float t = Mathf.Clamp01((flashUntil - Time.unscaledTime) / 0.35f);
            view.panel.color = Color.Lerp(view.panelColor, flashColor, t);
        }
    }

    // ------------------------------------------------------------ UI

    private void EnsureUI()
    {
        if (root != null)
            return;

        view = UIPrefabs.Spawn(CommandLineView.PrefabName, CommandLineView.BuildDefault);
        root = view.gameObject;

        if (view.message != null)
            view.message.gameObject.SetActive(false);

        root.SetActive(false);
    }

    private void RefreshArrows()
    {
        if (view == null || view.arrowRow == null)
            return;

        UIPrefabs.ClearClones(view.arrowRow, view.arrowTemplate);

        if (view.placeholder != null)
            view.placeholder.gameObject.SetActive(input.Count == 0);

        for (int i = 0; i < input.Count; i++)
            AddArrow(view.arrowTemplate, view.arrowRow, input[i]);
    }

    /// <summary>Clone ลูกศรต้นแบบ (ชี้ลง) แล้วหมุนตามทิศ ใช้ร่วมกับหน้า Logs &gt; Command</summary>
    public static Image AddArrow(Image template, Transform parent, CommandArrow arrow)
    {
        if (template == null)
            return null;

        Image image = UIPrefabs.Clone(template, parent);
        image.rectTransform.localEulerAngles = new Vector3(0f, 0f, RotationOf(arrow));
        return image;
    }

    // สามเหลี่ยมต้นแบบชี้ลงที่มุม 0
    private static float RotationOf(CommandArrow arrow)
    {
        switch (arrow)
        {
            case CommandArrow.Up: return 180f;
            case CommandArrow.Left: return -90f;
            case CommandArrow.Right: return 90f;
            default: return 0f;
        }
    }
}
