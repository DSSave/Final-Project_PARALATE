using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI ของ KnockoutSystem: ข้อความบอกปุ่ม (โชว์เมื่ออยู่ข้างหลังศัตรู) + หลอดนับถอยหลังตอน Kill/Mercy
/// หลอดเต็มตอนเริ่ม แล้วลดลงตามเวลาที่เหลือ ถ้าปล่อย Ctrl ก่อนเสร็จ หลอดจะค่อยๆ เพิ่มกลับจนเต็มแล้วซ่อน
///
/// โครง UI ที่ต้องสร้างใน Canvas:
///   Kill&Mercy                 (กลุ่มแม่ ห้ามใส่ในช่อง Root ใดๆ)
///   ├─ Mercy&KillText          ← promptRoot + promptText   (ข้อความปุ่ม)
///   └─ Mercy&KillBar           ← barRoot                   (กลุ่มของหลอดทั้งหมด)
///      ├─ Bar (Slider)         ← barSlider  (หรือใช้ barFill แทน ถ้าไม่ใช้ Slider)
///      ├─ ActionLabel          ← barLabel   (ไม่บังคับ)
///      └─ TimeLabel            ← barTimeText (ไม่บังคับ)
///
/// สำคัญ: promptRoot กับ barRoot ต้อง "ไม่ซ้อนกัน" (ตัวหนึ่งห้ามเป็นลูกของอีกตัว) ไม่งั้นซ่อนข้อความแล้วหลอดหายไปด้วย
/// </summary>
public class KnockoutPromptUI : MonoBehaviour
{
    [Header("Prompt (โชว์เมื่ออยู่ข้างหลังศัตรู)")]
    public GameObject promptRoot;
    public TMP_Text promptText;
    [TextArea] public string promptMessage = "LeftCtrl to mercy\nF to kill";

    [Header("Countdown Bar")]
    public GameObject barRoot;
    [Tooltip("ใช้ Slider ทำหลอด (ไม่บังคับ): ระบบจะตั้ง Min 0 / Max 1 และปิดการลากให้เอง ถ้าใส่ช่องนี้ไม่ต้องใส่ Bar Fill")]
    public Slider barSlider;
    [Tooltip("ทางเลือกแทน Slider: RectTransform ของแถบสีที่หดลง ระบบจะตั้ง Anchor ให้เอง")]
    public RectTransform barFill;
    public TMP_Text barLabel;
    public TMP_Text barTimeText;
    public string killLabel = "KILL";
    public string mercyLabel = "MERCY";

    [Tooltip("ปล่อย Ctrl ก่อนเสร็จ: เปิด = ซ่อนหลอดทันที (ข้อความปุ่มกลับมาโดยไม่รก) / " +
             "ปิด = หลอดค่อยๆ เพิ่มกลับจนเต็มแล้วค่อยซ่อน")]
    public bool hideBarOnRelease = true;

    [Tooltip("ความเร็วที่หลอดเพิ่มกลับ (ใช้เมื่อปิด Hide Bar On Release) สัดส่วนของหลอดต่อวินาที")]
    public float refillSpeed = 2f;

    private float totalSeconds = 1f;
    private float displayedFraction = 1f;
    private bool barActive;
    private bool refilling;

    private void Awake()
    {
        ResolveHudGroups();
        ValidateSetup();

        if (barSlider != null)
        {
            barSlider.minValue = 0f;
            barSlider.maxValue = 1f;
            barSlider.wholeNumbers = false;
            barSlider.interactable = false;                       // ผู้เล่นลากเองไม่ได้
            barSlider.transition = Selectable.Transition.None;    // ไม่ให้สีจางเพราะ interactable = false
        }

        if (barFill != null && barSlider == null)
        {
            // ยืดเต็มพื้นหลังก่อน แล้วค่อยลดความกว้างด้วย anchorMax.x (ไม่ต้องใช้ Sprite แบบ Filled)
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = Vector2.one;
            barFill.offsetMin = Vector2.zero;
            barFill.offsetMax = Vector2.zero;
        }

        if (promptText != null)
            promptText.text = promptMessage;

        SetPromptVisible(false);
        EndCountdown();
    }

    private void Update()
    {
        if (!barActive || !refilling)
            return;

        displayedFraction = Mathf.MoveTowards(displayedFraction, 1f, refillSpeed * Time.deltaTime);
        ApplyFraction();

        if (displayedFraction >= 1f)
            EndCountdown();
    }

    /// <summary>เปลี่ยนข้อความปุ่ม (KnockoutSystem ใช้สลับ "[F] to Strangle" กับ "[F] Kill [Ctrl hold] Knockout")</summary>
    public void SetPromptText(string text)
    {
        if (promptText != null && promptText.text != text)
            promptText.text = text;
    }

    public void SetPromptVisible(bool visible)
    {
        // ครั้งแรกที่ได้ตัดสินใจ ฆ่า/ไว้ชีวิต: ปลดล็อกบันทึก "Kill or Not" (ปลดแล้วจะไม่ทำซ้ำ)
        if (visible)
            GameLog.Unlock("kill_or_not");

        if (promptRoot != null && promptRoot.activeSelf != visible)
            promptRoot.SetActive(visible);
    }

    /// <summary>เริ่มหลอดนับถอยหลัง (หลอดเต็ม) isKill = true สำหรับ F, false สำหรับ Mercy (Ctrl)</summary>
    public void BeginCountdown(bool isKill, float seconds)
    {
        totalSeconds = Mathf.Max(0.01f, seconds);
        displayedFraction = 1f;
        barActive = true;
        refilling = false;

        if (barLabel != null)
            barLabel.text = isKill ? killLabel : mercyLabel;

        if (barRoot != null)
            barRoot.SetActive(true);

        ApplyFraction();
    }

    /// <summary>อัปเดตเวลาที่เหลือ (เรียกทุกเฟรมระหว่างนับถอยหลัง)</summary>
    public void SetRemaining(float remainingSeconds)
    {
        if (!barActive)
            return;

        refilling = false;
        displayedFraction = Mathf.Clamp01(remainingSeconds / totalSeconds);
        ApplyFraction();
    }

    /// <summary>ผู้เล่นปล่อยก่อนเสร็จ: หลอดค่อยๆ เพิ่มกลับจนเต็มแล้วซ่อน</summary>
    public void ReleaseEarly()
    {
        if (!barActive)
            return;

        if (hideBarOnRelease)
        {
            EndCountdown();
            return;
        }

        refilling = true;
    }

    /// <summary>เสร็จสิ้นหรือยกเลิกทันที: ซ่อนหลอด</summary>
    public void EndCountdown()
    {
        barActive = false;
        refilling = false;

        if (barRoot != null && barRoot.activeSelf)
            barRoot.SetActive(false);
    }

    /// <summary>
    /// ใน Scene อาจมีกลุ่ม "Kill&Mercy" มากกว่า 1 ชุด (ชุดใน MainHUD prefab + ชุดที่วางเองใน Scene)
    /// ชุดที่สคริปต์นี้ไม่ได้คุมจะค้างข้อความ "New Text" กับหลอดเปล่าอยู่บนจอตลอด จึงต้อง:
    ///   1) ถ้าช่อง Prompt Root / Bar Root ว่างหรือชี้ของที่หายไป → ต่อกับชุดแรกที่เจอให้เอง (หาจากชื่อวัตถุ)
    ///   2) เปิดชุดที่ใช้งาน (ให้ป้าย/หลอดโผล่ได้ตอนอยู่หลังศัตรู) และปิดชุดอื่นทั้งหมด
    /// ทำครั้งเดียวตอนเริ่ม Scene
    /// </summary>
    private void ResolveHudGroups()
    {
        const string groupName = "Kill&Mercy";

        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include);

        if (promptRoot == null || barRoot == null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name != groupName)
                    continue;

                Transform text = all[i].Find("Mercy&KillText");
                Transform bar = all[i].Find("Mercy&KillBar");
                if (text == null || bar == null)
                    continue;

                promptRoot = text.gameObject;
                promptText = text.GetComponent<TMP_Text>();
                barRoot = bar.gameObject;
                barSlider = bar.GetComponentInChildren<Slider>(true);
                barFill = null;

                Transform label = bar.Find("ActionLabel");
                Transform time = bar.Find("TimeLabel");
                barLabel = label != null ? label.GetComponent<TMP_Text>() : null;
                barTimeText = time != null ? time.GetComponent<TMP_Text>() : null;
                break;
            }
        }

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name != groupName)
                continue;

            bool inUse = (promptRoot != null && promptRoot.transform.IsChildOf(all[i])) ||
                         (barRoot != null && barRoot.transform.IsChildOf(all[i]));

            if (all[i].gameObject.activeSelf != inUse)
                all[i].gameObject.SetActive(inUse);
        }
    }

    // ตรวจการลากช่อง UI ผิดที่พบบ่อย: promptRoot กับ barRoot ซ้อนกัน
    private void ValidateSetup()
    {
        if (promptRoot != null && barRoot != null &&
            (barRoot.transform.IsChildOf(promptRoot.transform) || promptRoot.transform.IsChildOf(barRoot.transform)))
        {
            Debug.LogError("KnockoutPromptUI: Prompt Root และ Bar Root ต้องเป็นคนละวัตถุที่ไม่ซ้อนกัน " +
                           "(เช่น Prompt Root = ข้อความ, Bar Root = กลุ่มหลอด) ไม่งั้นซ่อนข้อความแล้วหลอดจะหายไปด้วย", this);
        }
    }

    private void ApplyFraction()
    {
        if (barSlider != null)
            barSlider.SetValueWithoutNotify(displayedFraction);
        else if (barFill != null)
            barFill.anchorMax = new Vector2(displayedFraction, 1f);

        if (barTimeText != null)
            barTimeText.SetText("{0:1}s", displayedFraction * totalSeconds);
    }
}
