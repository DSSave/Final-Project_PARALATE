using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระบบบันทึก (Memo) ของหน้า Logs ในเมนู Esc
///   - หัวข้อทั้งหมดเป็นไฟล์ LogEntryData ใน Assets/Resources/Logs/ (ไม่ต้องแก้โค้ดเพื่อเพิ่ม/แก้ข้อความ)
///   - บันทึกที่ unlockAtStart = ขึ้นตั้งแต่เริ่ม / ที่เหลือขึ้นเมื่อเรียก GameLog.Unlock("id")
///     (จาก LogUnlockTrigger, UnityEvent หรือโค้ดตอนเกิดเหตุการณ์)
///   - จำว่าปลดล็อก/อ่านแล้วอันไหน ใน PlayerPrefs (อยู่ข้ามด่านและข้ามการเปิดเกม)
/// </summary>
public static class GameLog
{
    [Serializable]
    private sealed class SaveData
    {
        public List<string> unlocked = new List<string>();
        public List<string> read = new List<string>();
    }

    private const string Folder = "Logs";
    private const string PrefKey = "GameLog_v2";

    private static List<LogEntryData> entries;
    private static SaveData save;

    /// <summary>บันทึกถูกปลดล็อกใหม่ (หน้า Logs ที่เปิดอยู่ใช้รีเฟรช)</summary>
    public static event Action<LogEntryData> OnUnlocked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        entries = null;
        save = null;
        OnUnlocked = null;
    }

    /// <summary>ทุกหัวข้อ เรียงตาม order</summary>
    public static IReadOnlyList<LogEntryData> All
    {
        get
        {
            if (entries == null)
            {
                entries = new List<LogEntryData>(Resources.LoadAll<LogEntryData>(Folder));
                entries.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.title, b.title));
            }

            return entries;
        }
    }

    public static LogEntryData Find(string id)
    {
        IReadOnlyList<LogEntryData> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].id == id)
                return all[i];
        }

        return null;
    }

    public static bool IsUnlocked(LogEntryData entry)
    {
        if (entry == null)
            return false;

        Load();
        return entry.unlockAtStart || save.unlocked.Contains(entry.id);
    }

    /// <summary>ยังไม่เคยเปิดอ่าน (โชว์ป้าย NEW)</summary>
    public static bool IsNew(LogEntryData entry)
    {
        Load();
        return entry != null && !save.read.Contains(entry.id);
    }

    public static void MarkRead(LogEntryData entry)
    {
        Load();
        if (entry == null || save.read.Contains(entry.id))
            return;

        save.read.Add(entry.id);
        Save();
    }

    /// <summary>ปลดล็อกบันทึกตาม id (ปลดแล้ว = ไม่ทำอะไร) คืน true ถ้าเพิ่งปลดครั้งนี้</summary>
    public static bool Unlock(string id)
    {
        LogEntryData entry = Find(id);
        if (entry == null)
        {
            if (!string.IsNullOrEmpty(id))
                Debug.LogWarning($"[GameLog] ไม่พบบันทึก id '{id}' ใน Resources/{Folder}");
            return false;
        }

        if (IsUnlocked(entry))
            return false;

        save.unlocked.Add(entry.id);
        Save();

        HudToast.Show("New log: " + entry.title + "   (Esc > logs)", 2.5f);
        OnUnlocked?.Invoke(entry);
        return true;
    }

    public static void ResetSave()
    {
        PlayerPrefs.DeleteKey(PrefKey);
        save = null;
    }

    private static void Load()
    {
        if (save != null)
            return;

        string json = PlayerPrefs.GetString(PrefKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            try { save = JsonUtility.FromJson<SaveData>(json); }
            catch (Exception) { save = null; } // ข้อมูลเสีย: เริ่มใหม่
        }

        if (save == null)
            save = new SaveData();

        if (save.unlocked == null) save.unlocked = new List<string>();
        if (save.read == null) save.read = new List<string>();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(save));
        PlayerPrefs.Save();
    }
}
