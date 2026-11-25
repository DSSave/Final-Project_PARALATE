using System.Collections;
using UnityEngine;

/// <summary>
/// ประตูบานพับ (เปิดแบบหมุนรอบบานพับ) กด E ที่ประตูเพื่อเปิด/ปิด
///
/// โครง Prefab (Assets/Prefabs/Interact/HingedDoor.prefab):
///   HingedDoor          ← สคริปต์นี้ วางตรง "แนวบานพับ" (ตัวนี้คือจุดหมุน)
///     Panel             ← บานประตู (Collider ทึบ กันคนเดินทะลุ) ยื่นออกไปทางแกน X
///     InteractZone      ← Collider แบบ Trigger ใน Layer "Items" ให้ระบบกด E (PickupDetector) เห็นประตู
///
/// การล็อก (ตั้งใน Inspector) ใช้หลายแบบพร้อมกันได้ ต้องผ่านทุกแบบถึงเปิดได้:
///   Required Key           : กุญแจเฉพาะของประตูนี้ (ไฟล์ DoorKeyData ใน Resources/Keys) ว่าง = ไม่ต้องใช้
///   Required Keycard Level : 0 = ไม่ต้องใช้ / 1 ขึ้นไป = ต้องมี Keycard ระดับนั้นขึ้นไป (บัตรไม่หมด)
///   Locked                 : ล็อกด้วยเหตุการณ์ (เปิดไม่ได้จนกว่าจะเรียก Unlock() จาก UnityEvent/สคริปต์)
///   Shootable Knob         : ติ๊ก = ประตูที่ล็อกอยู่ (ไม่ว่าแบบไหน) ยิงโดนลูกบิด (Knob ที่มี DoorKnob) แล้วกลอนพัง เปิดทันที
/// </summary>
public class HingedDoor : MonoBehaviour, IInteractable
{
    [Header("การเปิด")]
    [Tooltip("มุมที่เปิด (องศา)")]
    [Range(10f, 170f)] public float openAngle = 95f;
    [Tooltip("ความเร็วการหมุน (องศา/วินาที)")]
    public float openSpeed = 220f;
    [Tooltip("เปิดหนีผู้เล่นเสมอ (ผลักออกไปฝั่งตรงข้าม) / ไม่ติ๊ก = เปิดทางเดียวตาม Open Direction")]
    public bool openAwayFromPlayer = true;
    [Tooltip("ใช้เมื่อไม่ติ๊ก Open Away From Player: 1 = หมุนตามเข็ม, -1 = ทวนเข็ม (มองจากด้านบน)")]
    public int openDirection = 1;
    [Tooltip("ปิดเองหลังเปิดค้างกี่วินาที (0 = เปิดค้างจนกว่าจะกด E ปิด)")]
    [Min(0f)] public float autoCloseDelay = 0f;

    [Header("การล็อก")]
    [Tooltip("กุญแจเฉพาะของประตูนี้ (ลากไฟล์ DoorKeyData จาก Resources/Keys มาใส่) ว่าง = ไม่ต้องใช้กุญแจ")]
    public DoorKeyData requiredKey;
    [Tooltip("0 = ไม่ต้องใช้ Keycard, 1 = ต้องมี Keycard Lv.1 ขึ้นไป, 2 = Lv.2 ขึ้นไป ...")]
    [Min(0)] public int requiredKeycardLevel = 0;
    [Tooltip("ล็อกไว้จนกว่าจะมีเหตุการณ์เรียก Unlock() (เช่น กดสวิตช์, จบบทสนทนา)")]
    public bool locked = false;

    [Header("ยิงลูกบิดเพื่อเปิด")]
    [Tooltip("ติ๊ก = ตอนประตูล็อกอยู่ ยิงโดนลูกบิด (วัตถุ Knob ที่มี DoorKnob + Collider) แล้วกลอนพัง ประตูเปิดทันทีและไม่ล็อกอีก")]
    public bool shootableKnob = false;
    [Tooltip("ยิงกี่นัดกลอนถึงพัง")]
    [Min(1)] public int shotsToBreak = 1;

    [Header("ข้อความ Prompt")]
    public string openPrompt = "[ E ] Open";
    public string closePrompt = "[ E ] Close";
    [Tooltip("{0} = ชื่อกุญแจที่ต้องใช้")]
    public string keyPrompt = "Requires {0}";
    [Tooltip("{0} = ระดับ Keycard ที่ต้องใช้")]
    public string keycardPrompt = "Requires Keycard Lv.{0}";
    public string lockedPrompt = "Locked";
    [Tooltip("ต่อท้ายข้อความล็อก เมื่อติ๊ก Shootable Knob (บอกผู้เล่นว่ายิงลูกบิดได้) ว่าง = ไม่บอก")]
    public string shootHint = "";

    [Header("เสียง (ไม่บังคับ: ว่าง = ใช้ door_open / door_close / door_locked ในคลังเสียง)")]
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip lockedSound;

    private Quaternion closedRotation;
    private float currentAngle;   // มุมปัจจุบันเทียบกับตอนปิด
    private float targetAngle;
    private Coroutine motion;
    private float closeAt = -1f;

    private bool lockBroken; // ยิงลูกบิดพังแล้ว = ไม่ล็อกอีกเลย
    private int knobHits;

    public bool IsOpen => !Mathf.Approximately(targetAngle, 0f);

    public bool HasAccess =>
        lockBroken ||
        (!locked &&
         PlayerKeycards.PlayerHasKey(requiredKey) &&
         (requiredKeycardLevel <= 0 || PlayerKeycards.CurrentLevel >= requiredKeycardLevel));

    /// <summary>ประตูนี้ยังล็อกอยู่สำหรับผู้เล่นตอนนี้ (ไม่มีกุญแจ/บัตร หรือล็อกด้วยเหตุการณ์)</summary>
    public bool IsLockedForPlayer => !HasAccess;

    public string PromptText
    {
        get
        {
            if (IsOpen)
                return closePrompt;

            return HasAccess ? openPrompt : LockedMessage;
        }
    }

    // เหตุผลที่เปิดไม่ได้ (เรียงตามลำดับ: ล็อกด้วยเหตุการณ์ → กุญแจ → Keycard)
    private string LockedMessage
    {
        get
        {
            string message;
            if (locked)
                message = lockedPrompt;
            else if (!PlayerKeycards.PlayerHasKey(requiredKey))
                message = string.Format(keyPrompt, requiredKey.displayName);
            else
                message = string.Format(keycardPrompt, requiredKeycardLevel);

            return shootableKnob && !string.IsNullOrEmpty(shootHint) ? message + shootHint : message;
        }
    }

    private void Awake()
    {
        closedRotation = transform.localRotation;
    }

    private void Update()
    {
        if (closeAt > 0f && Time.time >= closeAt)
        {
            closeAt = -1f;
            Close();
        }
    }

    public void Interact(GameObject interactor)
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        if (!HasAccess)
        {
            if (lockedSound != null) AudioManager.PlayPickup(lockedSound, transform.position);
            else if (!SfxLibrary.Play("door_locked", transform.position)) SfxLibrary.Play("empty_click", transform.position);

            HudToast.Show(LockedMessage);
            return;
        }

        Open(interactor != null ? interactor.transform.position : transform.position - transform.forward);
    }

    /// <summary>
    /// กระสุนโดนลูกบิด (DoorKnob เรียก): ประตูที่ติ๊ก Shootable Knob และกำลังล็อกอยู่ → กลอนพัง เปิดหนีจากจุดที่ยิงมา
    /// ประตูที่ไม่ได้ล็อก / ไม่ได้ติ๊ก = ไม่มีผล (ไม่เปิดเองตอนยิงเล่น)
    /// </summary>
    public void ShootKnob(Vector3 hitPoint)
    {
        if (!shootableKnob || lockBroken || HasAccess)
            return;

        knobHits++;
        if (knobHits < shotsToBreak)
            return;

        lockBroken = true;
        locked = false;

        // เปิดหนีผู้ยิง: ใช้ตำแหน่งผู้เล่น (ยิงจากฝั่งไหน บานผลักไปอีกฝั่ง)
        TopDownPlayerController player = FindAnyObjectByType<TopDownPlayerController>();
        Vector3 from = player != null ? player.transform.position : hitPoint + (hitPoint - transform.position);
        Open(from);
    }

    /// <summary>เปิดประตู (หนีจากตำแหน่ง from ถ้าติ๊ก Open Away From Player) ไม่เช็กกุญแจ</summary>
    public void Open(Vector3 from)
    {
        int direction = openDirection >= 0 ? 1 : -1;

        if (openAwayFromPlayer)
        {
            // ผู้เล่นอยู่ด้านหน้าประตู (ทิศ forward ของบานพับ) → ผลักบานไปด้านหลัง และกลับกัน
            Vector3 toPlayer = from - transform.position;
            direction = Vector3.Dot(transform.forward, toPlayer) > 0f ? 1 : -1;
        }

        targetAngle = openAngle * direction;
        StartMotion(openSound, "door_open");

        if (autoCloseDelay > 0f)
            closeAt = Time.time + autoCloseDelay;
    }

    /// <summary>เปิดจาก UnityEvent (เช่น สวิตช์) ไม่เช็กกุญแจ</summary>
    public void OpenFromEvent()
    {
        Open(transform.position - transform.forward);
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        closeAt = -1f;
        targetAngle = 0f;
        StartMotion(closeSound, "door_close");
    }

    /// <summary>ปลดล็อกจากเหตุการณ์ (ไม่ได้เปิดประตูให้ ผู้เล่นต้องกด E เอง)</summary>
    public void Unlock()
    {
        locked = false;
    }

    public void Lock()
    {
        locked = true;
    }

    private void StartMotion(AudioClip clip, string libraryName)
    {
        if (clip != null) AudioManager.PlayPickup(clip, transform.position);
        else SfxLibrary.Play(libraryName, transform.position);

        if (motion != null)
            StopCoroutine(motion);

        motion = StartCoroutine(Rotate());
    }

    private IEnumerator Rotate()
    {
        while (!Mathf.Approximately(currentAngle, targetAngle))
        {
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, openSpeed * Time.deltaTime);
            transform.localRotation = closedRotation * Quaternion.Euler(0f, currentAngle, 0f);
            yield return null;
        }

        motion = null;
    }

#if UNITY_EDITOR
    // วาดมุมเปิดใน Scene view (เส้นสีฟ้า = ทิศที่บานจะเปิดไปได้)
    private void OnDrawGizmosSelected()
    {
        Transform panel = transform.Find("Panel");
        float width = panel != null ? panel.localScale.x : 1f;

        Gizmos.color = new Color(0.25f, 0.7f, 1f, 0.8f);
        Vector3 origin = transform.position + Vector3.up * 0.05f;
        Gizmos.DrawLine(origin, origin + transform.right * width);
        Gizmos.DrawLine(origin, origin + transform.rotation * Quaternion.Euler(0f, openAngle, 0f) * Vector3.right * width);
        Gizmos.DrawLine(origin, origin + transform.rotation * Quaternion.Euler(0f, -openAngle, 0f) * Vector3.right * width);
    }
#endif
}
