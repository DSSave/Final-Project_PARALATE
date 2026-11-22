using TMPro;
using UnityEngine;

/// <summary>
/// ป้ายบนจอ "ENEMY IS ALERTED" (ติดอยู่บนวัตถุ Alerting ใน MainHUD)
///   ปกติซ่อน → ศัตรูเข้าสถานะ Alert/Chasing แล้วป้ายแดงขึ้น
///
/// โหมด (ตั้งใน Inspector):
///   Timed       : ขึ้นค้าง Display Seconds วินาทีหลังศัตรูเริ่ม Alert แล้วหายไป (Alert ตัวใหม่ = นับเวลาใหม่)
///   WhileAlert  : ขึ้นค้างตราบใดที่ยังมีศัตรู Alert อยู่ แล้วหายไปเมื่อศัตรูทุกตัว (ในระยะ) เลิก Alert
///                 Nearby Radius = 0 → นับศัตรูทั้งด่าน / มากกว่า 0 → นับเฉพาะศัตรูในรัศมีนั้นรอบผู้เล่น ("แถวนั้น")
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class AlertBanner : MonoBehaviour
{
    public enum Mode { Timed, WhileAlert }

    [Header("ข้อความ")]
    public string message = "ENEMY IS ALERTED";
    public Color color = new Color(1f, 0.2f, 0.2f, 1f);

    [Header("การแสดง")]
    public Mode mode = Mode.WhileAlert;
    [Tooltip("โหมด Timed: แสดงกี่วินาที")]
    [Min(0.1f)] public float displaySeconds = 3f;
    [Tooltip("โหมด WhileAlert: 0 = ศัตรูทั้งด่าน / มากกว่า 0 = เฉพาะศัตรูในรัศมีนี้รอบผู้เล่น (เมตร)")]
    [Min(0f)] public float nearbyRadius = 0f;
    [Tooltip("ค้างต่ออีกนิดหลังศัตรูเลิก Alert กันป้ายกะพริบตอนสถานะสลับไปมา (วินาที)")]
    [Min(0f)] public float lingerSeconds = 0.5f;

    [Header("ลูกเล่น")]
    [Tooltip("เวลา Fade เข้า/ออก (วินาที)")]
    [Min(0f)] public float fadeTime = 0.2f;
    [Tooltip("กะพริบระหว่างแสดง (0 = ไม่กะพริบ)")]
    [Min(0f)] public float pulseSpeed = 4f;

    private TextMeshProUGUI label;
    private Transform player;
    private float visibleUntil = -1f;
    private float alpha;

    private void Awake()
    {
        label = GetComponent<TextMeshProUGUI>();
        label.text = message;
        alpha = 0f;
        Apply();
    }

    private void OnEnable()
    {
        AlertState.AlertStarted += HandleAlertStarted;
    }

    private void OnDisable()
    {
        AlertState.AlertStarted -= HandleAlertStarted;
    }

    private void HandleAlertStarted(AlertState enemy)
    {
        if (mode == Mode.Timed)
            visibleUntil = Time.time + displaySeconds;
    }

    private void Update()
    {
        if (mode == Mode.WhileAlert && AnyRelevantAlert())
            visibleUntil = Time.time + lingerSeconds;

        bool show = Time.time < visibleUntil;
        float step = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;
        alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, step);
        Apply();
    }

    private bool AnyRelevantAlert()
    {
        if (nearbyRadius > 0f && player == null)
        {
            TopDownPlayerController found = FindAnyObjectByType<TopDownPlayerController>();
            player = found != null ? found.transform : null;
        }

        float radiusSqr = nearbyRadius * nearbyRadius;

        for (int i = 0; i < AlertState.Active.Count; i++)
        {
            AlertState enemy = AlertState.Active[i];
            if (enemy == null || !enemy.IsAlertAndAlive)
                continue;

            if (nearbyRadius <= 0f || player == null)
                return true;

            if ((enemy.transform.position - player.position).sqrMagnitude <= radiusSqr)
                return true;
        }

        return false;
    }

    private void Apply()
    {
        if (label.text != message)
            label.text = message;

        float pulse = pulseSpeed > 0f ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI) : 1f;
        Color c = color;
        c.a *= alpha * pulse;
        label.color = c;
        label.enabled = alpha > 0.001f;
    }
}
