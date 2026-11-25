using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// คลังถาวรของผู้เล่น (ข้าม Scene + บันทึกลง PlayerPrefs): Scrap, Metal, กระสุน, ชิป X-Gear, Map Data
/// เก็บเป็น "id → จำนวน" (id ดูที่ ItemCatalog) เพื่อให้ไอเทมทุกชนิดใช้คลังเดียวกันได้
/// ResourceData เดิมยังใช้ได้: id = resourceName
/// </summary>
public class PersistentInventory : MonoBehaviour
{
    public static PersistentInventory Instance { get; private set; }

    /// <summary>ของในคลังเปลี่ยน (UI ใช้รีเฟรชตัวเลข)</summary>
    public static event Action OnChanged;

    private const string SaveKey = "Inventory_v1";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureExists()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("PersistentInventory (Auto)");
        go.AddComponent<PersistentInventory>();
    }

    private readonly Dictionary<string, int> owned = new Dictionary<string, int>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // ซ้ำกับตัวที่มีอยู่แล้ว: ลบเฉพาะคอมโพเนนต์นี้ (อาจอยู่บน GameManagers ร่วมกับ LevelManager)
            Destroy(this);
            return;
        }

        Instance = this;

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        Load();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnApplicationQuit() => Save();

    // ------------------------------------------------------------ อ่าน

    public int GetAmount(string id)
    {
        return !string.IsNullOrEmpty(id) && owned.TryGetValue(id, out int value) ? value : 0;
    }

    public int GetAmount(ResourceData data) => data != null ? GetAmount(data.resourceName) : 0;

    public bool Has(string id, int amount = 1) => GetAmount(id) >= amount;

    /// <summary>สำเนารายการของทั้งหมดที่มีมากกว่า 0</summary>
    public List<KeyValuePair<string, int>> Snapshot()
    {
        List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
        foreach (KeyValuePair<string, int> pair in owned)
        {
            if (pair.Value > 0)
                list.Add(pair);
        }

        return list;
    }

    // ------------------------------------------------------------ เขียน

    public void Add(string id, int amount)
    {
        if (string.IsNullOrEmpty(id) || amount == 0)
            return;

        owned.TryGetValue(id, out int current);
        owned[id] = Mathf.Max(0, current + amount);

        Save();
        OnChanged?.Invoke();
    }

    public void Add(ResourceData data, int amount)
    {
        if (data != null)
            Add(data.resourceName, amount);
    }

    /// <summary>จ่ายของ: คืน false และไม่หักอะไรเลยถ้ามีไม่พอ</summary>
    public bool TrySpend(string id, int amount)
    {
        if (amount <= 0)
            return true;

        if (GetAmount(id) < amount)
            return false;

        Add(id, -amount);
        return true;
    }

    /// <summary>จ่ายหลายอย่างพร้อมกันแบบ "ได้ทั้งหมดหรือไม่ได้เลย" (ใช้กับ Craft / Upgrade)</summary>
    public bool TrySpend(params (string id, int amount)[] costs)
    {
        for (int i = 0; i < costs.Length; i++)
        {
            if (GetAmount(costs[i].id) < costs[i].amount)
                return false;
        }

        for (int i = 0; i < costs.Length; i++)
            Add(costs[i].id, -costs[i].amount);

        return true;
    }

    /// <summary>แทนที่ของทั้งหมดในคลัง (ใช้ตอนโหลดเซฟ)</summary>
    public void ReplaceAll(IEnumerable<KeyValuePair<string, int>> items)
    {
        owned.Clear();
        if (items != null)
        {
            foreach (KeyValuePair<string, int> pair in items)
            {
                if (!string.IsNullOrEmpty(pair.Key) && pair.Value > 0)
                    owned[pair.Key] = pair.Value;
            }
        }

        Save();
        OnChanged?.Invoke();
    }

    // ------------------------------------------------------------ Save / Load

    private void Load()
    {
        owned.Clear();

        if (!PlayerPrefs.HasKey(SaveKey))
        {
            // เล่นครั้งแรก: กระสุนตั้งต้น เพื่อให้ปืนที่เก็บได้ในด่านรีโหลดได้
            owned[ItemCatalog.AmmoRifle] = 60;
            owned[ItemCatalog.AmmoPistol] = 24;
            owned[ItemCatalog.AmmoSniper] = 5;
            Save();
            return;
        }

        string saved = PlayerPrefs.GetString(SaveKey, string.Empty);
        string[] entries = saved.Split(';');

        for (int i = 0; i < entries.Length; i++)
        {
            int separator = entries[i].LastIndexOf('=');
            if (separator <= 0)
                continue;

            if (int.TryParse(entries[i].Substring(separator + 1), out int amount) && amount > 0)
                owned[entries[i].Substring(0, separator)] = amount;
        }
    }

    public void Save()
    {
        StringBuilder builder = new StringBuilder();
        foreach (KeyValuePair<string, int> pair in owned)
        {
            if (pair.Value <= 0)
                continue;

            if (builder.Length > 0)
                builder.Append(';');

            builder.Append(pair.Key).Append('=').Append(pair.Value);
        }

        PlayerPrefs.SetString(SaveKey, builder.ToString());
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR
    // เมนูทดสอบ: ล้างเซฟคลัง / X-Gear / ระดับอัปเกรดอาวุธ
    [UnityEditor.MenuItem("Tools/Paralate/Reset Inventory + Loadout Save")]
    private static void EditorReset()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        XGearSystem.ResetOldLoadout();
        PickupSystem.ResetSave();
        WeaponUpgrades.ResetSave();
        GameLog.ResetSave();
        CommandBook.ResetSave();
        PlayerPrefs.Save();
        Debug.Log("Reset inventory / X-Gear / weapon upgrade save แล้ว");
    }

    [UnityEditor.MenuItem("Tools/Paralate/Give Test Items")]
    private static void EditorGive()
    {
        if (!Application.isPlaying || Instance == null)
        {
            Debug.LogWarning("ใช้ได้ตอนกด Play เท่านั้น");
            return;
        }

        Instance.Add(ItemCatalog.Scrap, 30);
        Instance.Add(ItemCatalog.Metal, 30);
        Instance.Add(ItemCatalog.ChipFleetFoot, 1);
        Instance.Add(ItemCatalog.ChipInvisible, 1);
        Instance.Add(ItemCatalog.MapHideOut, 1);
        Instance.Add(ItemCatalog.Medkit, 3);
        Instance.Add(ItemCatalog.XGearCell, 2);
        Instance.Add(ItemCatalog.WeaponUsp, 1);
        Instance.Add(ItemCatalog.WeaponSniper, 1);
        Instance.Add(ItemCatalog.WeaponAk, 1);

        foreach (string id in new[] { ItemCatalog.Medkit, ItemCatalog.XGearCell, ItemCatalog.WeaponUsp, ItemCatalog.WeaponSniper, ItemCatalog.WeaponAk })
            CommandBook.LearnFromItem(id);
    }
#endif
}
