using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ตัวพาไป Scene ที่เลือกใน Inspector (ผ่าน LevelManager จึงมี Fade + Loading)
///   - ปุ่ม UI: ลากวัตถุนี้ใส่ OnClick ของปุ่ม แล้วเลือก SceneLoader > Load()
///   - จุดวาร์ป/ทางออก: ติ๊ก Load On Player Enter แล้วใส่ Collider (Is Trigger) ผู้เล่นเดินเข้า = ไป Scene นั้น
///   - เรียกจาก UnityEvent อื่นก็ได้ (จบ Timeline, InteractableObject.onInteract ฯลฯ)
/// </summary>
public class SceneLoader : MonoBehaviour
{
    [Tooltip("Scene ปลายทาง (เลือกจาก Build Settings)")]
    [SceneName] public string targetScene;

    [Tooltip("ไม่บังคับ: id ของ SpawnPoint ใน Scene ปลายทาง (ว่าง = จุดเริ่มของด่าน)")]
    public string spawnId;

    [Tooltip("ติ๊ก = เริ่มรอบใหม่ ทิ้งของที่เก็บมาในรอบนี้ / ไม่ติ๊ก = เก็บของไว้ (เช่น เดินต่อไปอีกพื้นที่)")]
    public bool abandonRun = true;

    [Tooltip("ไปทันทีเมื่อผู้เล่นเดินเข้า Collider (ต้องติ๊ก Is Trigger)")]
    public bool loadOnPlayerEnter;

    public void Load()
    {
        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogWarning($"SceneLoader '{name}': ยังไม่ได้เลือก Target Scene", this);
            return;
        }

        if (LevelManager.Instance != null)
            LevelManager.Instance.LoadScene(targetScene, string.IsNullOrEmpty(spawnId) ? null : spawnId, abandonRun);
        else
            SceneManager.LoadScene(targetScene);
    }

    /// <summary>ไป Scene ตามชื่อ (ใช้จาก UnityEvent ที่ส่ง string ได้)</summary>
    public void Load(string sceneName)
    {
        targetScene = sceneName;
        Load();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (loadOnPlayerEnter && other.CompareTag("Player"))
            Load();
    }
}
