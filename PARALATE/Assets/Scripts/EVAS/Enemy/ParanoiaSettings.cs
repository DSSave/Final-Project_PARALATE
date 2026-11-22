using UnityEngine;

/// <summary>
/// ค่าของ Paranoid ระดับทั้งด่าน (Assets/Resources/ParanoiaSettings.asset) ปรับใน Inspector ได้
/// ไม่มีไฟล์ = ใช้ค่าเริ่มต้นในคลาสนี้
/// </summary>
[CreateAssetMenu(menuName = "Paralate/Paranoia Settings", fileName = "ParanoiaSettings")]
public class ParanoiaSettings : ScriptableObject
{
    [Tooltip("ระดับสูงสุดของทั้งด่าน")]
    [Min(0)] public int maxLevel = 5;

    [Tooltip("ศัตรูหลายตัว Alert ภายในช่วงเวลานี้ = นับเป็นเหตุการณ์เดียว (+1 ระดับครั้งเดียว) (วินาที)")]
    [Min(0f)] public float incidentWindow = 15f;

    [Tooltip("แต้ม Alert ที่ได้ (ทั้งเห็นและได้ยิน) เพิ่มขึ้นกี่ % ต่อระดับ (0.15 = +15%)")]
    [Min(0f)] public float gainPerLevel = 0.15f;

    [Tooltip("ศัตรูใจเย็นลงช้าลงกี่ % ต่อระดับ (0.15 = ลดแต้มช้าลง 15%)")]
    [Min(0f)] public float returnSlowdownPerLevel = 0.15f;

    [Tooltip("ไม่มีศัตรู Alert นานเท่านี้ ระดับลด 1 (วินาที) / 0 = ไม่ลดจนจบด่าน")]
    [Min(0f)] public float decayAfterSeconds = 0f;

    private static ParanoiaSettings cached;
    private static ParanoiaSettings fallback;

    public static ParanoiaSettings Current
    {
        get
        {
            if (cached == null)
                cached = Resources.Load<ParanoiaSettings>("ParanoiaSettings");
            if (cached != null)
                return cached;

            if (fallback == null)
                fallback = CreateInstance<ParanoiaSettings>();
            return fallback;
        }
    }
}
