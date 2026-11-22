using UnityEngine;

/// <summary>
/// ระดับกราฟิกที่ผู้เล่นเลือกได้จากเมนู Esc > Setting (ใช้ Quality Level ของโปรเจกต์:
/// High Fidelity / Balanced / Performant ซึ่งแต่ละระดับผูกกับ HDRP Asset คนละตัว)
/// ค่าที่เลือกเก็บใน PlayerPrefs และถูกใช้ทันทีตอนเปิดเกมครั้งถัดไป
/// </summary>
public static class GraphicsQuality
{
    private const string PrefKey = "GraphicsQualityLevel";

    public static string CurrentName
    {
        get
        {
            string[] names = QualitySettings.names;
            int level = QualitySettings.GetQualityLevel();
            return level >= 0 && level < names.Length ? names[level] : "Default";
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySaved()
    {
        if (!PlayerPrefs.HasKey(PrefKey))
            return; // ยังไม่เคยเลือก: ใช้ค่าเริ่มต้นของโปรเจกต์

        int level = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey), 0, QualitySettings.names.Length - 1);
        if (level != QualitySettings.GetQualityLevel())
            QualitySettings.SetQualityLevel(level, true);
    }

    /// <summary>เปลี่ยนไประดับถัดไป (วนกลับระดับแรกเมื่อสุด) คืนชื่อระดับใหม่</summary>
    public static string Cycle()
    {
        int count = QualitySettings.names.Length;
        if (count == 0)
            return CurrentName;

        int level = (QualitySettings.GetQualityLevel() + 1) % count;
        QualitySettings.SetQualityLevel(level, true);

        PlayerPrefs.SetInt(PrefKey, level);
        PlayerPrefs.Save();
        return CurrentName;
    }
}
