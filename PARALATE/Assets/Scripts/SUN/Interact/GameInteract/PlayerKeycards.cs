using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keycard ที่ผู้เล่นถือ (0 = ไม่มีบัตร) บัตรเป็นของถาวรในด่าน: ใช้แล้วไม่หมด
/// และบัตรระดับสูงเปิดประตูที่ต้องการระดับ "ต่ำกว่าหรือเท่ากับ" ได้เสมอ (ประตูเช็กแค่ระดับสูงสุดที่มี)
/// อยู่บนตัวผู้เล่นของแต่ละ Scene จึงเริ่มใหม่ทุกรอบ/ทุกครั้งที่โหลดด่าน
/// KeycardPickup จะเพิ่มคอมโพเนนต์นี้ให้ผู้เล่นเองถ้ายังไม่มี ไม่ต้องเพิ่มเอง
/// </summary>
public class PlayerKeycards : MonoBehaviour
{
    public static PlayerKeycards Instance { get; private set; }

    /// <summary>ระดับ Keycard สูงสุดที่มีตอนนี้ (0 ถ้าไม่มีผู้เล่น/ไม่มีบัตร)</summary>
    public static int CurrentLevel => Instance != null ? Instance.HighestLevel : 0;

    public int HighestLevel { get; private set; }

    /// <summary>ระดับของบัตรทุกใบที่เก็บมา เรียงจากน้อยไปมาก (หน้า Inventory ใช้แสดง)</summary>
    public IReadOnlyList<int> OwnedLevels => ownedLevels;

    private readonly List<int> ownedLevels = new List<int>();

    public event Action<int> OnHighestLevelChanged;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>ได้รับ Keycard ระดับนี้ (บัตรสูงกว่าใช้แทนบัตรต่ำกว่าได้ จึงเก็บแค่ระดับสูงสุด)</summary>
    public void Grant(int level)
    {
        if (level > 0 && !ownedLevels.Contains(level))
        {
            ownedLevels.Add(level);
            ownedLevels.Sort();
        }

        if (level <= HighestLevel)
            return;

        HighestLevel = level;
        OnHighestLevelChanged?.Invoke(HighestLevel);
    }

    public bool HasLevel(int requiredLevel)
    {
        return requiredLevel <= 0 || HighestLevel >= requiredLevel;
    }

    // ------------------------------------------------------------ กุญแจเฉพาะประตู (DoorKeyData)

    private readonly List<DoorKeyData> keys = new List<DoorKeyData>();

    /// <summary>กุญแจทุกดอกที่เก็บมาในด่านนี้ (หน้า Inventory / ระบบเซฟใช้)</summary>
    public IReadOnlyList<DoorKeyData> OwnedKeys => keys;

    public event Action<DoorKeyData> OnKeyGranted;

    public void GrantKey(DoorKeyData key)
    {
        if (key == null || keys.Contains(key))
            return;

        keys.Add(key);
        OnKeyGranted?.Invoke(key);
    }

    public bool HasKey(DoorKeyData key)
    {
        return key == null || keys.Contains(key);
    }

    /// <summary>ผู้เล่นใน Scene มีกุญแจดอกนี้ไหม (key = null ถือว่ามี)</summary>
    public static bool PlayerHasKey(DoorKeyData key)
    {
        return key == null || (Instance != null && Instance.HasKey(key));
    }

    public static PlayerKeycards GetOrAdd(GameObject player)
    {
        PlayerKeycards cards = player.GetComponentInParent<PlayerKeycards>();
        return cards != null ? cards : player.AddComponent<PlayerKeycards>();
    }
}
