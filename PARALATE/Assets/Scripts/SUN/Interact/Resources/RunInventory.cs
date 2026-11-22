using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ของที่เก็บได้ "ในรอบนี้" (Scrap, Metal, ชิป, Map Data) ยังไม่เป็นของถาวร
/// จบด่านสำเร็จ → CommitToPersistent ย้ายเข้าคลังถาวร / ตายหรือออกกลางคัน → ClearRun ทิ้งทั้งหมด
/// </summary>
public class RunInventory : MonoBehaviour
{
    public static RunInventory Instance { get; private set; }

    public static event Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureExists()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("RunInventory (Auto)");
        go.AddComponent<RunInventory>();
    }

    private readonly Dictionary<string, int> collected = new Dictionary<string, int>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Add(string id, int amount)
    {
        if (string.IsNullOrEmpty(id) || amount <= 0)
            return;

        collected.TryGetValue(id, out int current);
        collected[id] = current + amount;
        OnChanged?.Invoke();
    }

    public void Add(ResourceData data, int amount)
    {
        if (data != null)
            Add(data.resourceName, amount);
    }

    public int GetAmount(string id) =>
        !string.IsNullOrEmpty(id) && collected.TryGetValue(id, out int value) ? value : 0;

    public int GetAmount(ResourceData data) => data != null ? GetAmount(data.resourceName) : 0;

    public List<KeyValuePair<string, int>> Snapshot() => new List<KeyValuePair<string, int>>(collected);

    /// <summary>แทนที่ของในรอบนี้ทั้งหมด (ใช้ตอนโหลดเซฟ)</summary>
    public void ReplaceAll(IEnumerable<KeyValuePair<string, int>> items)
    {
        collected.Clear();
        if (items != null)
        {
            foreach (KeyValuePair<string, int> pair in items)
            {
                if (!string.IsNullOrEmpty(pair.Key) && pair.Value > 0)
                    collected[pair.Key] = pair.Value;
            }
        }

        OnChanged?.Invoke();
    }

    public void ClearRun()
    {
        if (collected.Count == 0)
            return;

        collected.Clear();
        OnChanged?.Invoke();
    }

    /// <summary>ย้ายของที่เก็บในรอบนี้เข้าคลังถาวร (เรียกเมื่อจบด่านสำเร็จเท่านั้น)</summary>
    public void CommitToPersistent()
    {
        if (PersistentInventory.Instance == null)
        {
            // ไม่ล้างของในรอบนี้ เพื่อไม่ให้ของหายถ้าคลังถาวรยังไม่พร้อม
            Debug.LogError("PersistentInventory.Instance is null — cannot commit run inventory.");
            return;
        }

        foreach (KeyValuePair<string, int> pair in collected)
            PersistentInventory.Instance.Add(pair.Key, pair.Value);

        ClearRun();
    }
}
