using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// สร้าง Prefab ของหน้าต่าง UI ทั้งหมดจากหน้าตาเริ่มต้น (BuildDefault ของแต่ละ View)
/// ลงที่ Assets/Resources/UI/ — หลังจากนั้นแก้ Prefab ใน Editor ได้อิสระ เกมจะโหลด Prefab แทนโค้ด
///   Create Missing UI Prefabs : สร้างเฉพาะตัวที่ยังไม่มี (ไม่ทับงานที่แก้ไว้)
///   Rebuild ... (overwrite)    : สร้างใหม่ทับ (ใช้เมื่ออยากรีเซ็ตเป็นหน้าตาเริ่มต้น)
/// </summary>
public static class UIPrefabBaker
{
    private const string Folder = "Assets/Resources/UI";

    private static readonly (string name, Func<Component> build)[] Views =
    {
        (WeaponHudView.PrefabName, () => WeaponHudView.BuildDefault()),
        (CommandLineView.PrefabName, () => CommandLineView.BuildDefault()),
        (PauseMenuView.PrefabName, () => PauseMenuView.BuildDefault()),
        (SaveSlotsView.PrefabName, () => SaveSlotsView.BuildDefault()),
        (XGearInstallerView.PrefabName, () => XGearInstallerView.BuildDefault()),
    };

    [MenuItem("Tools/Paralate/UI/Create Missing UI Prefabs")]
    public static void CreateMissing()
    {
        Bake(false);
    }

    [MenuItem("Tools/Paralate/UI/Rebuild All UI Prefabs (overwrite)")]
    public static void RebuildAll()
    {
        if (!EditorUtility.DisplayDialog("Rebuild UI Prefabs",
                "สร้าง Prefab UI ทั้งหมดใหม่ทับของเดิม\nสิ่งที่แก้ไว้ใน Prefab จะหายทั้งหมด", "Rebuild", "Cancel"))
            return;

        Bake(true);
    }

    public static string Bake(bool overwrite)
    {
        Directory.CreateDirectory(Folder);
        EnsureSprites();

        var scene = EditorSceneManager.GetActiveScene();
        bool wasDirty = scene.isDirty;
        string report = string.Empty;

        foreach (var (name, build) in Views)
        {
            string path = $"{Folder}/{name}.prefab";
            if (!overwrite && File.Exists(path))
            {
                report += $"{name}: มีอยู่แล้ว (ข้าม)\n";
                continue;
            }

            Component view = build();
            GameObject go = view.gameObject;
            go.name = name;
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            report += $"{name}: สร้างแล้ว → {path}\n";
        }

        // การสร้างชั่วคราวทำให้ Scene ถูกมาร์กว่าแก้ไข ทั้งที่ไม่ได้เปลี่ยนอะไร: เซฟคืนสถานะเดิม (เฉพาะตอนเดิมไม่มีงานค้าง)
        if (!wasDirty && scene.isDirty && !string.IsNullOrEmpty(scene.path))
            EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        Debug.Log("UI Prefabs:\n" + report);
        return report;
    }

    // รูปวงกลม/สามเหลี่ยมของ UIBuilder ต้องเป็นไฟล์จริง Prefab ถึงอ้างถึงได้
    private static void EnsureSprites()
    {
        bool created = false;
        created |= WriteSprite("UI_Circle", UIBuilderTextures.Circle(128));
        created |= WriteSprite("UI_Triangle", UIBuilderTextures.Triangle(64, 48));

        if (created)
            AssetDatabase.Refresh();

        UIBuilder.ClearSpriteCache();
    }

    private static bool WriteSprite(string name, Texture2D texture)
    {
        string path = $"{Folder}/{name}.png";
        if (File.Exists(path))
            return false;

        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return true;
    }

    /// <summary>วาดรูปแบบเดียวกับ UIBuilder (วงกลม / สามเหลี่ยมชี้ลง ขอบนุ่ม)</summary>
    private static class UIBuilderTextures
    {
        public static Texture2D Circle(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            float radius = size * 0.5f - 1f;
            Vector2 center = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - Vector2.Distance(new Vector2(x, y), center) + 0.5f));

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        public static Texture2D Triangle(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                float halfWidth = (y + 0.5f) / height * (width * 0.5f);
                for (int x = 0; x < width; x++)
                {
                    float fromCenter = Mathf.Abs(x + 0.5f - width * 0.5f);
                    pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(halfWidth - fromCenter + 0.5f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
