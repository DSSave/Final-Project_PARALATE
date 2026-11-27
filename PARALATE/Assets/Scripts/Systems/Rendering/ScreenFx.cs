using UnityEngine;

/// <summary>
/// คุมฟิลเตอร์ภาพจากโค้ดเกมเพลย์ + ค่าที่ผู้เล่นตั้งเอง (ไม่แตะ Volume Profile)
///   ScreenFx.Pulse(0.6f, 1.5f)  → แรงขึ้นชั่วครู่แล้วจางกลับ (เช่นตอนโดนเห็น / โดนยิง)
///   ScreenFx.SetBoost(0..1)     → แรงขึ้นค้างไว้ (เช่นผูกกับ LevelParanoia) เรียก SetBoost(0) เพื่อเลิก
///   ปริมาณที่เพิ่มต่อ Boost 1 ตั้งที่ Volume > Paralate Screen FX > Boost
///   ผู้เล่น: GrainScale (0–1) / DistortionEnabled บันทึกใน PlayerPrefs คูณทับค่าใน Volume (ไม่เกินที่ตั้งไว้)
/// </summary>
public static class ScreenFx
{
    private const string PrefGrain = "ScreenFx_GrainScale";
    private const string PrefDistortion = "ScreenFx_Distortion";

    private static float boost;
    private static float pulsePeak;
    private static float pulseStart;
    private static float pulseDuration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        boost = 0f;
        pulsePeak = 0f;
        pulseDuration = 0f;
    }

    /// <summary>แรงขึ้นทันทีเท่า amount (0–1) แล้วจางเป็น 0 ใน seconds วินาที (Pulse ซ้อนกันใช้ค่าที่แรงกว่า)</summary>
    public static void Pulse(float amount, float seconds)
    {
        if (CurrentPulse > amount)
            return;

        pulsePeak = Mathf.Clamp01(amount);
        pulseStart = Time.time;
        pulseDuration = Mathf.Max(0.01f, seconds);
    }

    /// <summary>ตั้งความแรงเพิ่มแบบค้าง (0–1)</summary>
    public static void SetBoost(float amount) => boost = Mathf.Clamp01(amount);

    private static float CurrentPulse
    {
        get
        {
            if (pulsePeak <= 0f)
                return 0f;
            float k = 1f - (Time.time - pulseStart) / pulseDuration;
            return k <= 0f ? 0f : pulsePeak * k;
        }
    }

    /// <summary>Boost รวมตอนนี้ (ค่าค้าง หรือ Pulse ที่แรงกว่า)</summary>
    public static float CurrentBoost => Mathf.Max(boost, CurrentPulse);

    // อ่าน PlayerPrefs ครั้งเดียวแล้วจำไว้ (Renderer Feature อ่านทุกเฟรม)
    private static float? grainScale;
    private static bool? distortionEnabled;

    /// <summary>ผู้เล่นตั้งความแรง Grain (0–1, ค่าเริ่ม 1)</summary>
    public static float GrainScale
    {
        get => grainScale ??= PlayerPrefs.GetFloat(PrefGrain, 1f);
        set
        {
            grainScale = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefGrain, grainScale.Value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>ผู้เล่นเปิด/ปิดภาพบิดเบี้ยว (คลื่น เส้นกระตุก โค้งขอบ) สำหรับคนเมาภาพ</summary>
    public static bool DistortionEnabled
    {
        get => distortionEnabled ??= PlayerPrefs.GetInt(PrefDistortion, 1) == 1;
        set
        {
            distortionEnabled = value;
            PlayerPrefs.SetInt(PrefDistortion, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
