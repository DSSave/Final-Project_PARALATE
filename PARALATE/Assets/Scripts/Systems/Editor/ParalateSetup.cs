#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ตัวตั้งค่าเนื้อหาของอัปเดตนี้ (เมนู Tools > Paralate > Setup Content) รันซ้ำได้ ไม่สร้างของซ้ำ
/// 1) สร้าง Prefab: กล่องกระสุน 3 ชนิด, ชิป FleetFoot / Invisible, Map Data HideOut, กล้องวงจรปิด
/// 2) ตั้งค่า Prefab Sniper (ชนิดกระสุน, fire rate, มุมซูม)
/// 3) Base: ติด UpgradeTerminal ให้วัตถุ "Upgrade"
/// 4) HideOut: วางของทดสอบ (Sniper, กระสุน, ชิป, Map Data) ข้างจุดเริ่มของผู้เล่น
/// ให้ Unity เป็นคนเขียนไฟล์ Prefab/Scene เอง (ผ่าน PrefabUtility / EditorSceneManager) แทนการแก้ YAML ด้วยมือ
/// </summary>
public static class ParalateSetup
{
    private const string ItemsFolder = "Assets/Prefabs/Items";
    private const string EnemyFolder = "Assets/Prefabs/Enemy";
    private const string ScrapPrefabPath = ItemsFolder + "/Scrap.prefab";
    private const string SniperPrefabPath = ItemsFolder + "/Sniper.prefab";

    [MenuItem("Tools/Paralate/Setup Content (prefabs + scenes)")]
    public static void SetupFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string previousScene = SceneManager.GetActiveScene().path;
        Run();

        if (!string.IsNullOrEmpty(previousScene))
            EditorSceneManager.OpenScene(previousScene);
    }

    /// <summary>เรียกจาก batchmode: Unity -batchmode -executeMethod ParalateSetup.Run -quit</summary>
    public static void Run()
    {
        GameObject ammoAk = CreatePickupPrefab("AmmoBox_AK", ItemCatalog.AmmoRifle, 30, new Vector3(0.35f, 0.25f, 0.35f));
        GameObject ammoUsp = CreatePickupPrefab("AmmoBox_USP", ItemCatalog.AmmoPistol, 12, new Vector3(0.3f, 0.2f, 0.3f));
        GameObject ammoSniper = CreatePickupPrefab("AmmoBox_Sniper", ItemCatalog.AmmoSniper, 5, new Vector3(0.3f, 0.2f, 0.4f));
        GameObject chipFleet = CreatePickupPrefab("Chip_FleetFoot", ItemCatalog.ChipFleetFoot, 1, new Vector3(0.25f, 0.06f, 0.25f));
        GameObject chipInvisible = CreatePickupPrefab("Chip_Invisible", ItemCatalog.ChipInvisible, 1, new Vector3(0.25f, 0.06f, 0.25f));
        GameObject mapHideOut = CreatePickupPrefab("MapData_HideOut", ItemCatalog.MapHideOut, 1, new Vector3(0.3f, 0.04f, 0.4f));

        CreateSecurityCameraPrefab();
        ConfigureSniperPrefab();

        AssetDatabase.SaveAssets();

        SetupBaseScene();
        SetupHideOutScene(new[] { ammoAk, ammoUsp, ammoSniper, chipFleet, chipInvisible, mapHideOut });
        SyncHideOutWithPrefabs();

        AssetDatabase.SaveAssets();
        Debug.Log("[ParalateSetup] DONE");
    }

    // ------------------------------------------------------------ Prefab

    // ใช้ Scrap.prefab เป็นแม่แบบ (ได้ Mesh/Material/Collider/Layer ที่ถูกกับโปรเจกต์) แล้วสลับสคริปต์เป็น InventoryPickup
    private static GameObject CreatePickupPrefab(string prefabName, string itemId, int amount, Vector3 scale)
    {
        string path = $"{ItemsFolder}/{prefabName}.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing;

        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(ScrapPrefabPath);
        GameObject instance;

        if (template != null)
        {
            instance = Object.Instantiate(template);
            ResourcePickup old = instance.GetComponent<ResourcePickup>();
            if (old != null)
                Object.DestroyImmediate(old);
        }
        else
        {
            instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            int itemsLayer = LayerMask.NameToLayer("Items");
            if (itemsLayer >= 0) instance.layer = itemsLayer;
        }

        instance.name = prefabName;
        instance.transform.localScale = scale;

        InventoryPickup pickup = instance.AddComponent<InventoryPickup>();
        pickup.itemId = itemId;
        pickup.amount = amount;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        Debug.Log("[ParalateSetup] created " + path);
        return saved;
    }

    private static void CreateSecurityCameraPrefab()
    {
        string path = EnemyFolder + "/SecurityCamera.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return;

        GameObject root = new GameObject("SecurityCamera");

        GameObject mount = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mount.name = "Mount";
        mount.transform.SetParent(root.transform, false);
        mount.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
        Object.DestroyImmediate(mount.GetComponent<Collider>());

        GameObject head = new GameObject("Head");
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, -0.15f, 0f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(head.transform, false);
        body.transform.localPosition = new Vector3(0f, 0f, 0.15f);
        body.transform.localRotation = Quaternion.Euler(20f, 0f, 0f); // ก้มมองลงเล็กน้อย
        body.transform.localScale = new Vector3(0.2f, 0.18f, 0.45f);
        Object.DestroyImmediate(body.GetComponent<Collider>());

        // ใช้ Material ของโปรเจกต์ (Primitive ที่สร้างจากโค้ดจะได้ Material มาตรฐานซึ่งเป็นสีชมพูใน HDRP)
        Material material = FindProjectMaterial();
        if (material != null)
        {
            mount.GetComponent<MeshRenderer>().sharedMaterial = material;
            body.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        SecurityCamera camera = root.AddComponent<SecurityCamera>();
        camera.head = head.transform;
        camera.obstructionMask = LayerMask.GetMask("Obstacle");

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log("[ParalateSetup] created " + path);
    }

    private static Material FindProjectMaterial()
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(ScrapPrefabPath);
        MeshRenderer renderer = template != null ? template.GetComponentInChildren<MeshRenderer>() : null;
        return renderer != null ? renderer.sharedMaterial : null;
    }

    private static void ConfigureSniperPrefab()
    {
        if (!File.Exists(SniperPrefabPath))
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(SniperPrefabPath);
        GunAction gun = contents.GetComponent<GunAction>();

        if (gun != null && gun.ammoType != AmmoType.Sniper)
        {
            gun.ammoType = AmmoType.Sniper;
            gun.slotType = WeaponSlotType.Primary;
            gun.fireRate = 1.5f;            // ยิงทีละนัด ช้ากว่าปืนอื่นชัดเจน
            gun.scopedFieldOfView = 15f;    // ซูมไกลกว่าปืนทั่วไป (40)
            gun.scopedMovementSpeedMultiplier = 0.4f;
            gun.headshotMultiplier = 4f;
            gun.hitMask = ~(1 << 2);
            PrefabUtility.SaveAsPrefabAsset(contents, SniperPrefabPath);
            Debug.Log("[ParalateSetup] configured Sniper prefab");
        }

        PrefabUtility.UnloadPrefabContents(contents);
    }

    // ------------------------------------------------------------ Scene

    private static string FindScenePath(string sceneName)
    {
        string path = $"Assets/Scenes/{sceneName}.unity";
        return File.Exists(path) ? path : null;
    }

    private static void SetupBaseScene()
    {
        string path = FindScenePath(SceneNames.Base);
        if (path == null)
            return;

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        if (Object.FindAnyObjectByType<UpgradeTerminal>(FindObjectsInactive.Include) != null)
            return;

        GameObject target = GameObject.Find("Upgrade");
        if (target == null)
        {
            Debug.LogWarning("[ParalateSetup] Base: ไม่พบวัตถุชื่อ 'Upgrade' จึงไม่ได้ติด UpgradeTerminal");
            return;
        }

        int itemsLayer = LayerMask.NameToLayer("Items");
        if (itemsLayer >= 0)
            target.layer = itemsLayer;

        target.AddComponent<UpgradeTerminal>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ParalateSetup] Base: added UpgradeTerminal to 'Upgrade'");
    }

    private static void SetupHideOutScene(GameObject[] pickups)
    {
        string path = FindScenePath("HideOut");
        if (path == null)
            return;

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        if (GameObject.Find("XGearTestItems") != null)
            return;

        TopDownPlayerController player = Object.FindAnyObjectByType<TopDownPlayerController>(FindObjectsInactive.Include);
        if (player == null)
        {
            Debug.LogWarning("[ParalateSetup] HideOut: ไม่พบผู้เล่น จึงไม่ได้วางของทดสอบ");
            return;
        }

        // วางเป็นแถวด้านหน้าผู้เล่น รวมไว้ใต้กลุ่มเดียว ย้าย/ลบทั้งกลุ่มได้ง่าย
        GameObject group = new GameObject("XGearTestItems");
        Transform origin = player.transform;
        Vector3 start = origin.position + origin.forward * 2f - origin.right * 2.5f;

        int index = 0;
        for (int i = 0; i < pickups.Length; i++)
        {
            if (pickups[i] == null)
                continue;

            Place(pickups[i], group.transform, start + origin.right * (index * 0.9f) + Vector3.up * 0.3f);
            index++;
        }

        GameObject sniper = AssetDatabase.LoadAssetAtPath<GameObject>(SniperPrefabPath);
        if (sniper != null && Object.FindObjectsByType<GunAction>(FindObjectsInactive.Include).Length > 0)
            Place(sniper, group.transform, start + origin.right * (index * 0.9f) + Vector3.up * 0.5f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ParalateSetup] HideOut: placed test items near the player start");
    }

    /// <summary>
    /// HideOut ถูกทำก่อนที่ระบบพิงกำแพง/เสียงเดินจะถูกย้ายเข้า Player prefab จึงมีของค้าง:
    /// 1) ผู้เล่นมี WallHugController / WallHugCameraPan / PlayerAudioController ที่เพิ่มใน Scene ซ้ำกับของ prefab
    ///    → ลบตัวที่เพิ่มใน Scene, คืนช่อง wallHug ให้ชี้ตัวของ prefab, ใช้ค่ากล้องพิงกำแพงชุดเดียวกับ Simulation 1
    ///    → ปิด WallHugCam ตัวเก่าที่วางแยกใน Scene (ใช้ตัวที่อยู่ใน prefab แทน)
    /// 2) override ที่ชี้ไปคอมโพเนนต์ซึ่งไม่มีใน prefab แล้ว → ลบทิ้ง
    /// รันซ้ำได้: ถ้าไม่มีอะไรค้างจะไม่แตะ Scene
    /// </summary>
    private static void SyncHideOutWithPrefabs()
    {
        string path = FindScenePath("HideOut");
        if (path == null)
            return;

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        bool changed = false;

        TopDownPlayerController player = Object.FindAnyObjectByType<TopDownPlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            GameObject playerObject = player.gameObject;

            // เก็บกล้องตัวเก่าที่คอมโพเนนต์ซ้ำใช้อยู่ ก่อนลบคอมโพเนนต์
            foreach (WallHugCameraPan pan in playerObject.GetComponents<WallHugCameraPan>())
            {
                if (!PrefabUtility.IsAddedComponentOverride(pan))
                    continue;

                SerializedObject serialized = new SerializedObject(pan);
                Object oldCamera = serialized.FindProperty("wallHugCamera").objectReferenceValue;
                Component cameraComponent = oldCamera as Component;

                // ปิดเฉพาะกล้องที่ไม่ได้อยู่ใต้ผู้เล่น (ตัวที่วางแยกไว้ในห้อง)
                if (cameraComponent != null && !cameraComponent.transform.IsChildOf(playerObject.transform))
                {
                    Transform oldRoot = cameraComponent.transform.parent != null ? cameraComponent.transform.parent : cameraComponent.transform;
                    oldRoot.gameObject.SetActive(false);
                    Debug.Log("[ParalateSetup] HideOut: disabled old scene camera rig '" + oldRoot.name + "'");
                }
            }

            changed |= RemoveAddedDuplicates<WallHugCameraPan>(playerObject);
            changed |= RemoveAddedDuplicates<WallHugController>(playerObject);
            changed |= RemoveAddedDuplicates<PlayerAudioController>(playerObject);

            // ช่อง wallHug เคยถูก override ให้ชี้ตัวที่เพิ่มใน Scene: คืนค่าเป็นของ prefab
            SerializedObject movement = new SerializedObject(player);
            SerializedProperty wallHug = movement.FindProperty("wallHug");
            if (wallHug != null && wallHug.prefabOverride)
            {
                PrefabUtility.RevertPropertyOverride(wallHug, InteractionMode.AutomatedAction);
                changed = true;
            }

            // ค่ากล้องพิงกำแพงชุดล่าสุด (เท่ากับที่ปรับไว้ใน Simulation 1)
            WallHugCameraPan prefabPan = playerObject.GetComponent<WallHugCameraPan>();
            if (prefabPan != null)
            {
                SerializedObject serializedPan = new SerializedObject(prefabPan);
                changed |= SetFloat(serializedPan, "panLimit", 4f);
                changed |= SetFloat(serializedPan, "panSpeed", 0.1f);
                changed |= SetFloat(serializedPan, "cameraDamping", 0.12f);
                serializedPan.ApplyModifiedPropertiesWithoutUndo();
            }

            // override ค้างที่ชี้ไปของซึ่งไม่มีใน prefab แล้ว
            PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(playerObject);
            if (modifications != null)
            {
                System.Collections.Generic.List<PropertyModification> kept = new System.Collections.Generic.List<PropertyModification>();
                foreach (PropertyModification modification in modifications)
                {
                    if (modification.target != null)
                        kept.Add(modification);
                }

                if (kept.Count != modifications.Length)
                {
                    PrefabUtility.SetPropertyModifications(playerObject, kept.ToArray());
                    Debug.Log("[ParalateSetup] HideOut: removed " + (modifications.Length - kept.Count) + " stale override(s) on the player");
                    changed = true;
                }
            }
        }

        // กลุ่ม Kill&Mercy: ไม่แตะที่นี่แล้ว KnockoutPromptUI เปิดชุดที่ใช้และปิดชุดซ้ำเองตอนเริ่ม Scene

        if (!changed)
            return;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ParalateSetup] HideOut: synced with prefabs");
    }

    // ลบเฉพาะคอมโพเนนต์ที่ "เพิ่มใน Scene" เมื่อ prefab มีคอมโพเนนต์ชนิดเดียวกันอยู่แล้ว
    private static bool RemoveAddedDuplicates<T>(GameObject target) where T : Component
    {
        T[] all = target.GetComponents<T>();
        if (all.Length < 2)
            return false;

        bool removed = false;
        foreach (T component in all)
        {
            if (!PrefabUtility.IsAddedComponentOverride(component))
                continue;

            Debug.Log("[ParalateSetup] HideOut: removed duplicate " + typeof(T).Name + " added in scene");
            Object.DestroyImmediate(component);
            removed = true;
        }

        return removed;
    }

    private static bool SetFloat(SerializedObject serialized, string property, float value)
    {
        SerializedProperty found = serialized.FindProperty(property);
        if (found == null || Mathf.Approximately(found.floatValue, value))
            return false;

        found.floatValue = value;
        return true;
    }

    private static void Place(GameObject prefab, Transform parent, Vector3 position)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.position = position;
    }
}
#endif
