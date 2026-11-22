#if UNITY_EDITOR
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ทำให้วัตถุ "EyeWide" ใน Player.prefab เป็นกล้อง (CinemachineCamera) ตั้งแต่ใน Prefab
/// จะได้เห็นและปรับค่ากล้องใน Inspector ได้ ไม่ต้องรอให้โค้ดเพิ่มให้ตอน Play
/// รันเองอัตโนมัติหลัง Unity คอมไพล์ ถ้ายังไม่มีกล้อง (หรือกดเมนู Tools > Paralate > Setup Eye Wide Camera)
/// ให้ Unity เป็นคนเขียนไฟล์ Prefab เอง (PrefabUtility) ไม่แก้ YAML ด้วยมือ
/// </summary>
public static class EyeWidePrefabSetup
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        EditorApplication.delayCall += () =>
        {
            // ไม่แตะ Prefab ตอนกำลัง Play หรือตอนผู้ใช้เปิด Prefab ค้างอยู่ในโหมดแก้ไข (กันทับงานที่ยังไม่เซฟ)
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                return;

            if (NeedsSetup())
                Run();
        };
    }

    private static bool NeedsSetup()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
            return false;

        EyeWideController eyeWide = prefab.GetComponentInChildren<EyeWideController>(true);

        // ทำเฉพาะเมื่อ EyeWide เป็นวัตถุลูกของ Player (ถ้าติดบนตัว Player ตรงๆ โค้ดจะสร้างกล้องแยกเองตอนรัน)
        return eyeWide != null && eyeWide.gameObject != prefab && eyeWide.GetComponent<CinemachineCamera>() == null;
    }

    [MenuItem("Tools/Paralate/Setup Eye Wide Camera")]
    public static void Run()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

        try
        {
            EyeWideController eyeWide = root.GetComponentInChildren<EyeWideController>(true);
            if (eyeWide == null || eyeWide.gameObject == root)
            {
                Debug.LogWarning("[EyeWidePrefabSetup] ไม่พบวัตถุลูกที่มี EyeWideController ใน Player.prefab");
                return;
            }

            CinemachineCamera camera = eyeWide.GetComponent<CinemachineCamera>();
            if (camera == null)
            {
                camera = eyeWide.gameObject.AddComponent<CinemachineCamera>();
                camera.Priority.Value = 0;                     // ปกติไม่ได้ใช้ โค้ดจะดัน Priority ขึ้นตอนกด T
                camera.Lens.FieldOfView = eyeWide.defaultFov;
                camera.Lens.NearClipPlane = 0.05f;             // ใกล้หน้ามาก กันภาพขาดตอนอยู่ชิดกำแพง
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[EyeWidePrefabSetup] DONE: EyeWide ใน Player.prefab มี CinemachineCamera แล้ว");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
