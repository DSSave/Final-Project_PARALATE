using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// โซนของ Minimap (Map Chip): วางกล่อง (BoxCollider แบบ Trigger) ครอบพื้นที่ 1 โซน แล้วตั้ง Map Chip Id ของโซนนั้น
///   ผู้เล่นเข้าโซน + มี Map Chip ของโซนนี้ในคลัง → Minimap ขึ้นและจัดกล้องให้เห็นทั้งโซนจากด้านบน (รวมศัตรู)
///   เข้าโซนที่ไม่มีชิป / อยู่นอกทุกโซน → ซ่อน Minimap (และหยุดเรนเดอร์กล้อง Minimap ประหยัดเฟรมเรต)
///   โซนซ้อนกัน: ใช้โซนที่เข้าล่าสุด
///
/// Map Chip ของโซนใหม่: ตั้ง id ขึ้นต้น map_ (เช่น map_metro) แล้ววาง InventoryPickup ที่ใช้ id เดียวกันในด่าน
/// ไม่ต้องแก้โค้ด (ชื่อที่แสดงในหน้า Esc > X-Gear มาจาก Area Name ของโซนนี้)
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class MapArea : MonoBehaviour
{
    [Tooltip("id ของ Map Chip ที่เปิด Minimap ของโซนนี้ (ขึ้นต้น map_ เช่น map_metro)")]
    public string mapChipId = "map_area";
    [Tooltip("ชื่อโซน (แสดงในหน้า Esc > X-Gear และใต้ Minimap)")]
    public string areaName = "Area";
    [TextArea] public string description = "Shows this area's minimap, including enemies, while you are inside it.";

    [Header("กล้อง Minimap")]
    [Tooltip("เผื่อขอบภาพรอบโซน (เท่าของขนาดโซน)")]
    [Min(1f)] public float padding = 1.1f;
    [Tooltip("ความสูงกล้องเหนือขอบบนของโซน (เมตร)")]
    public float cameraHeightAboveArea = 15f;

    private static readonly List<MapArea> occupied = new List<MapArea>();
    private BoxCollider box;

    /// <summary>โซนที่ผู้เล่นอยู่ล่าสุด (null = นอกทุกโซน)</summary>
    public static MapArea Current => occupied.Count > 0 ? occupied[occupied.Count - 1] : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => occupied.Clear();

    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        ItemCatalog.RegisterMap(mapChipId, "Map Data : " + areaName, description);
    }

    private void OnEnable()
    {
        PersistentInventory.OnChanged += RefreshMinimap;
    }

    private void OnDisable()
    {
        PersistentInventory.OnChanged -= RefreshMinimap;
        if (occupied.Remove(this))
            RefreshMinimap();
    }

    private void Start()
    {
        // ผู้เล่นเกิดในโซนตั้งแต่แรก (Trigger ไม่ส่ง Enter)
        Collider[] hits = Physics.OverlapBox(box.bounds.center, box.bounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                Enter();
                break;
            }
        }

        RefreshMinimap();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            Enter();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && occupied.Remove(this))
            RefreshMinimap();
    }

    private void Enter()
    {
        occupied.Remove(this);
        occupied.Add(this);
        RefreshMinimap();
    }

    // ------------------------------------------------------------ แสดง/ซ่อน Minimap ตามโซนปัจจุบัน

    /// <summary>แสดง/ซ่อน Minimap ตามโซนที่ผู้เล่นอยู่ (XGearSystem เรียกตอนเริ่ม Scene ด้วย: Scene ที่ไม่มีโซนเลย = ซ่อน)</summary>
    public static void RefreshMinimap()
    {
        MinimapManager manager = FindAnyObjectByType<MinimapManager>(FindObjectsInactive.Include);
        if (manager == null)
            return; // Scene นี้ไม่มี Minimap

        MapArea area = Current;
        bool show = area != null && PersistentInventory.Instance != null && PersistentInventory.Instance.Has(area.mapChipId);

        // กลุ่ม "Map" ใน HUD (พ่อของ MinimapManager)
        Transform mapRoot = manager.transform.parent;
        if (mapRoot != null && mapRoot.GetComponent<Canvas>() == null)
        {
            if (mapRoot.gameObject.activeSelf != show)
                mapRoot.gameObject.SetActive(show);

            // ชื่อโซนใต้ Minimap (ข้อความ "Map Descript" ใน MainHUD)
            if (show)
                foreach (TMPro.TMP_Text text in mapRoot.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    if (text.name == "Map Descript") text.text = area.areaName;
        }

        MinimapCameraFollow follow = FindAnyObjectByType<MinimapCameraFollow>(FindObjectsInactive.Include);
        Camera mapCamera = follow != null ? follow.GetComponent<Camera>() : null;
        if (mapCamera == null)
            return;

        // ไม่ได้โชว์ = ปิดกล้อง Minimap สนิท / โชว์อยู่ = เรนเดอร์ 10 ครั้งต่อวินาที
        MinimapRenderThrottle.EnsureOn(mapCamera).SetVisible(show);
        if (!show)
            return;

        // จัดกล้องให้เห็นทั้งโซนจากด้านบน
        Bounds bounds = area.box.bounds;
        follow.enabled = false;

        float aspect = mapCamera.targetTexture != null
            ? (float)mapCamera.targetTexture.width / mapCamera.targetTexture.height
            : mapCamera.aspect;

        float height = bounds.max.y + area.cameraHeightAboveArea;
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = Mathf.Max(bounds.extents.z, bounds.extents.x / Mathf.Max(0.01f, aspect)) * area.padding;
        mapCamera.farClipPlane = Mathf.Max(mapCamera.farClipPlane, height - bounds.min.y + 10f);
        mapCamera.transform.SetPositionAndRotation(new Vector3(bounds.center.x, height, bounds.center.z), Quaternion.Euler(90f, 0f, 0f));
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        BoxCollider b = GetComponent<BoxCollider>();
        if (b == null)
            return;

        Gizmos.color = new Color(0.9f, 0.5f, 0.9f, 0.15f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = new Color(0.9f, 0.5f, 0.9f, 0.8f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
#endif
}
