using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตัวกำกับลำดับ Scene ของเกม (ไฟล์เดียว ปรับใน Inspector): Assets/Resources/SceneFlow.asset
///   - New Game ไป Scene ไหน, ฮับ (Base), Main Menu, หน้า Options และรายชื่อด่านที่เลือกเล่นได้
///   - ช่องชื่อ Scene เป็น Dropdown รายชื่อ Scene ใน Build Settings (เลือกได้เลย ไม่ต้องพิมพ์)
/// โค้ดทั้งเกมอ่านค่าผ่าน SceneNames (SceneNames.Tutorial, SceneNames.Base, ...) ซึ่งดึงจากไฟล์นี้
/// ถ้าไม่มีไฟล์ จะใช้ค่าเริ่มต้นด้านล่าง (เหมือนก่อนมีไฟล์นี้)
/// </summary>
[CreateAssetMenu(menuName = "Paralate/Scene Flow", fileName = "SceneFlow")]
public class SceneFlow : ScriptableObject
{
    [Header("เริ่มเกม")]
    [Tooltip("กด New Game ที่หน้า Main Menu แล้วไป Scene นี้")]
    [SceneName] public string newGame = "Simulation 1";

    [Header("Scene หลัก")]
    [SceneName] public string mainMenu = "MainMenu";
    [Tooltip("ฮับ (ปุ่ม Back to Base / จบด่านแล้วกลับ)")]
    [SceneName] public string hub = "Base";
    [Tooltip("หน้า Options (โหลดซ้อนบน Main Menu)")]
    [SceneName] public string options = "OptionsMenu";

    [Header("ด่านที่เลือกเล่นได้จากฮับ (ลำดับ = index ที่ LevelManager.LoadLevel ใช้)")]
    [SceneName] public List<string> levels = new List<string> { "HideOut" };

    private const string ResourcePath = "SceneFlow";

    private static SceneFlow current;
    private static string[] levelArray;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        current = null;
        levelArray = null;
    }

    /// <summary>ค่าที่ใช้อยู่ (ไฟล์ใน Resources หรือค่าเริ่มต้นถ้าไม่มีไฟล์)</summary>
    public static SceneFlow Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<SceneFlow>(ResourcePath);
                if (current == null)
                    current = CreateInstance<SceneFlow>();
            }

            return current;
        }
    }

    public static string[] Levels
    {
        get
        {
            if (levelArray == null)
                levelArray = Current.levels.ToArray();

            return levelArray;
        }
    }

#if UNITY_EDITOR
    // แก้ค่าใน Inspector ระหว่าง Play: ให้รายชื่อด่านอัปเดตตาม
    private void OnValidate()
    {
        levelArray = null;
    }
#endif
}
