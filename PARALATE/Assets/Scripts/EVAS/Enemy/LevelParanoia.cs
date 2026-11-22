using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Paranoid ระดับทั้งด่าน: ทั้งฐานระแวงขึ้นทุกครั้งที่มี "เหตุการณ์" (ศัตรูตัวใดก็ได้เข้า Alert)
///   - ศัตรูหลายตัว Alert พร้อมกัน (ภายใน Incident Window) นับเป็นเหตุการณ์เดียว
///   - ทุกระดับ: ศัตรูทุกตัวในด่านได้แต้ม Alert ไวขึ้น (ทั้งเห็นและได้ยิน) และใจเย็นลงช้าลง
///   - เริ่มใหม่ที่ 0 ทุกครั้งที่โหลดฉาก (ลดลงตามเวลาได้ถ้าตั้ง Decay After Seconds)
/// ทำงานคู่กับ Paranoid รายตัวใน AlertState (สองระดับรวมกัน) ค่าปรับที่ Resources/ParanoiaSettings
/// </summary>
public static class LevelParanoia
{
    public static int Level { get; private set; }

    /// <summary>ระดับทั้งด่านเปลี่ยน (ใช้ทำ HUD / เสียงเตือนได้ภายหลัง)</summary>
    public static event Action<int> OnChanged;

    private static float lastIncidentTime = -999f;
    private static float lastAlertSeenTime;

    /// <summary>ตัวคูณแต้ม Alert จากระดับทั้งด่าน (1 = ปกติ)</summary>
    public static float GainMultiplier => 1f + Level * ParanoiaSettings.Current.gainPerLevel;

    /// <summary>ตัวคูณเวลาลดแต้มจากระดับทั้งด่าน (1 = ปกติ)</summary>
    public static float ReturnSlowdown => Level * ParanoiaSettings.Current.returnSlowdownPerLevel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Level = 0;
        OnChanged = null;
        lastIncidentTime = -999f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        AlertState.AlertStarted -= HandleAlertStarted;
        AlertState.AlertStarted += HandleAlertStarted;
        Ticker.Ensure();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
            SetLevel(0);

        // AlertState.ResetStatics ล้าง event ตอนเริ่มเกม: ผูกใหม่กันหลุด
        AlertState.AlertStarted -= HandleAlertStarted;
        AlertState.AlertStarted += HandleAlertStarted;
        lastIncidentTime = -999f;
        lastAlertSeenTime = Time.time;
    }

    private static void HandleAlertStarted(AlertState enemy)
    {
        ParanoiaSettings settings = ParanoiaSettings.Current;

        if (Time.time - lastIncidentTime < settings.incidentWindow)
            return; // ยังเป็นเหตุการณ์เดิม

        lastIncidentTime = Time.time;
        SetLevel(Level + 1);
    }

    private static void SetLevel(int value)
    {
        int clamped = Mathf.Clamp(value, 0, ParanoiaSettings.Current.maxLevel);
        if (clamped == Level)
            return;

        Level = clamped;
        OnChanged?.Invoke(Level);
    }

    // ลดระดับตามเวลา (ถ้าเปิดไว้): ไม่มีศัตรู Alert ต่อเนื่องนานพอ = ลด 1 ระดับ
    private static void Tick()
    {
        float decay = ParanoiaSettings.Current.decayAfterSeconds;
        if (decay <= 0f || Level == 0)
            return;

        for (int i = 0; i < AlertState.Active.Count; i++)
        {
            if (AlertState.Active[i] != null && AlertState.Active[i].IsAlertAndAlive)
            {
                lastAlertSeenTime = Time.time;
                return;
            }
        }

        if (Time.time - lastAlertSeenTime >= decay)
        {
            lastAlertSeenTime = Time.time;
            SetLevel(Level - 1);
        }
    }

    /// <summary>ตัวเรียก Tick ทุกเฟรม (ซ่อนไว้ ไม่ต้องวางในฉาก)</summary>
    private sealed class Ticker : MonoBehaviour
    {
        private static Ticker instance;

        public static void Ensure()
        {
            if (instance != null)
                return;

            GameObject go = new GameObject("LevelParanoia (Ticker)") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<Ticker>();
        }

        private void Update() => Tick();
    }
}
