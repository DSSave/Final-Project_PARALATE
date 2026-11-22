using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ระบบเซฟเกม 3 ช่อง (ไฟล์ JSON ที่ Application.persistentDataPath/saves/slotN.json)
/// บันทึก: Scene, ตำแหน่ง/ทิศผู้เล่น, เลือด, X-Gear charge, ของในคลังถาวร + ของในรอบนี้, Keycard,
///         ชิป X-Gear ที่ใส่อยู่, สูตร Command ที่รู้แล้ว
/// ไม่บันทึก: สถานะศัตรู/ของที่เก็บไปแล้วในฉาก (โหลดแล้ว Scene เริ่มใหม่ตามค่าเริ่มต้น แต่ผู้เล่นกลับมาที่จุดเซฟ)
/// โหลด: คืนค่าคลังทันที → โหลด Scene → รอผู้เล่นใน Scene ใหม่พร้อม แล้ววางตำแหน่ง/เลือด/บัตร
/// </summary>
public static class SaveSystem
{
    public const int SlotCount = 3;

    [Serializable]
    public class Entry
    {
        public string id;
        public int amount;
    }

    [Serializable]
    public class Data
    {
        public int version = 1;
        public string scene;
        public Vector3 position;
        public float yaw;
        public float health;
        public int xgearCharges;
        public List<Entry> inventory = new List<Entry>();
        public List<Entry> run = new List<Entry>();
        public List<int> keycards = new List<int>();
        public string passive;
        public string active;
        public string map;
        public List<string> commands = new List<string>();
        public string savedAt;
    }

    private static Data pending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => pending = null;

    private static string Folder => Path.Combine(Application.persistentDataPath, "saves");

    private static string PathOf(int slot) => Path.Combine(Folder, $"slot{slot + 1}.json");

    public static bool Exists(int slot) => File.Exists(PathOf(slot));

    /// <summary>ลบไฟล์เซฟของช่องนี้ (ปุ่ม X ในหน้า Load Game)</summary>
    public static void Delete(int slot)
    {
        try
        {
            if (Exists(slot))
                File.Delete(PathOf(slot));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SaveSystem: ลบช่อง {slot + 1} ไม่ได้ ({e.Message})");
        }
    }

    /// <summary>อ่านข้อมูลช่อง (ไว้แสดงในหน้าต่าง) null = ว่างหรือไฟล์เสีย</summary>
    public static Data Peek(int slot)
    {
        try
        {
            return Exists(slot) ? JsonUtility.FromJson<Data>(File.ReadAllText(PathOf(slot))) : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SaveSystem: อ่านช่อง {slot + 1} ไม่ได้ ({e.Message})");
            return null;
        }
    }

    // ------------------------------------------------------------ Save

    public static bool Save(int slot, out string error)
    {
        error = null;

        TopDownPlayerController player = UnityEngine.Object.FindAnyObjectByType<TopDownPlayerController>();
        if (player == null)
        {
            error = "No player in this scene";
            return false;
        }

        Data data = new Data
        {
            scene = SceneManager.GetActiveScene().name,
            position = player.transform.position,
            yaw = player.transform.eulerAngles.y,
            passive = XGearLoadout.PassiveId,
            active = XGearLoadout.ActiveId,
            map = XGearLoadout.MapId,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        };

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        data.health = health != null ? health.health : 100f;

        XGearSystem gear = player.GetComponent<XGearSystem>();
        data.xgearCharges = gear != null ? gear.Charges : 0;

        if (PersistentInventory.Instance != null)
            foreach (KeyValuePair<string, int> pair in PersistentInventory.Instance.Snapshot())
                data.inventory.Add(new Entry { id = pair.Key, amount = pair.Value });

        if (RunInventory.Instance != null)
            foreach (KeyValuePair<string, int> pair in RunInventory.Instance.Snapshot())
                data.run.Add(new Entry { id = pair.Key, amount = pair.Value });

        PlayerKeycards cards = player.GetComponent<PlayerKeycards>();
        if (cards != null)
            data.keycards.AddRange(cards.OwnedLevels);

        data.commands.AddRange(CommandBook.LearnedIds);

        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(PathOf(slot), JsonUtility.ToJson(data, true));
            return true;
        }
        catch (Exception e)
        {
            error = "Save failed: " + e.Message;
            Debug.LogError("SaveSystem: " + error);
            return false;
        }
    }

    // ------------------------------------------------------------ Load

    public static bool Load(int slot, out string error)
    {
        error = null;
        Data data = Peek(slot);

        if (data == null || string.IsNullOrEmpty(data.scene))
        {
            error = "Empty slot";
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(data.scene))
        {
            error = $"Scene '{data.scene}' is not in Build Settings";
            return false;
        }

        // ของที่ไม่ผูกกับ Scene: คืนค่าทันที
        PersistentInventory.Instance?.ReplaceAll(ToPairs(data.inventory));
        RunInventory.Instance?.ReplaceAll(ToPairs(data.run));
        XGearLoadout.SetRaw(data.passive, data.active, data.map);
        CommandBook.SetLearned(data.commands);

        // ของบนตัวผู้เล่น: ใส่หลังโหลด Scene เสร็จ
        pending = data;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (LevelManager.Instance != null)
            LevelManager.Instance.LoadScene(data.scene, null, false);
        else
            SceneManager.LoadScene(data.scene);

        return true;
    }

    private static IEnumerable<KeyValuePair<string, int>> ToPairs(List<Entry> entries)
    {
        if (entries == null)
            yield break;

        foreach (Entry entry in entries)
            yield return new KeyValuePair<string, int>(entry.id, entry.amount);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pending == null || scene.name != pending.scene)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Runner.Instance.StartCoroutine(ApplyWhenReady(pending));
        pending = null;
    }

    private static IEnumerator ApplyWhenReady(Data data)
    {
        // รอให้ Start() ของ Scene ใหม่ (ผู้เล่น, LevelManager วางจุดเกิด, XGearSystem ตั้งค่า) ทำงานก่อน แล้วค่อยทับด้วยค่าจากเซฟ
        yield return null;
        yield return null;

        TopDownPlayerController player = UnityEngine.Object.FindAnyObjectByType<TopDownPlayerController>();
        if (player == null)
            yield break;

        CharacterController controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;
        player.transform.SetPositionAndRotation(data.position, Quaternion.Euler(0f, data.yaw, 0f));
        if (wasEnabled) controller.enabled = true;
        Physics.SyncTransforms();

        if (controller != null)
            CameraZone.RefreshForPlayer(controller);

        player.GetComponent<PlayerHealth>()?.SetHealth(data.health);
        player.GetComponent<XGearSystem>()?.SetCharges(data.xgearCharges);

        if (data.keycards != null && data.keycards.Count > 0)
        {
            PlayerKeycards cards = PlayerKeycards.GetOrAdd(player.gameObject);
            foreach (int level in data.keycards)
                cards.Grant(level);
        }
    }

    /// <summary>ตัวรัน Coroutine ข้าม Scene (ซ่อนไว้ ไม่ต้องวางในฉาก)</summary>
    private sealed class Runner : MonoBehaviour
    {
        private static Runner instance;

        public static Runner Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("SaveSystem (Runner)") { hideFlags = HideFlags.HideInHierarchy };
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    instance = go.AddComponent<Runner>();
                }

                return instance;
            }
        }
    }
}
