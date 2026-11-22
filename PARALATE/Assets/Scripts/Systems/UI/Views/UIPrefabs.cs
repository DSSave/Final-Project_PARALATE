using System;
using UnityEngine;

/// <summary>
/// โหลดหน้าต่าง UI จาก Prefab ที่แก้ใน Editor ได้ (Assets/Resources/UI/ชื่อ.prefab)
///
/// หลักการ: แยก "หน้าตา" (View = Prefab ที่จัดตำแหน่ง/สี/ฟอนต์ใน Editor) ออกจาก "การทำงาน" (สคริปต์เดิม)
///   - สคริปต์การทำงานเรียก Spawn → ได้ View ที่มีช่องอ้างอิงถึงปุ่ม/ข้อความทั้งหมด แล้วเติมข้อมูลลงไป
///   - ของที่ซ้ำหลายชิ้น (แถว Log, ช่อง Inventory, ช่องเซฟ) ใช้ "Template" ใน Prefab (วัตถุที่ปิดไว้) แล้ว Clone ตอนรัน
///     แก้ Template ใน Prefab = ทุกแถวเปลี่ยนตาม
///   - ถ้ายังไม่มี Prefab จะสร้างหน้าตาเริ่มต้นด้วยโค้ดแทน (เกมไม่พัง) และสร้าง Prefab ได้ที่
///     Tools > Paralate > UI > Create Missing UI Prefabs
/// </summary>
public static class UIPrefabs
{
    public const string Folder = "UI/";

    public static T Spawn<T>(string name, Func<T> buildDefault) where T : Component
    {
        GameObject prefab = Resources.Load<GameObject>(Folder + name);
        if (prefab != null)
        {
            GameObject go = UnityEngine.Object.Instantiate(prefab);
            go.name = prefab.name;
            T view = go.GetComponent<T>();
            if (view != null)
                return view;

            Debug.LogError($"UIPrefabs: Prefab '{name}' ไม่มีคอมโพเนนต์ {typeof(T).Name} ที่ตัวบนสุด ใช้หน้าตาเริ่มต้นแทน", prefab);
            UnityEngine.Object.Destroy(go);
        }

        return buildDefault();
    }

    /// <summary>Clone Template (วัตถุต้นแบบที่ปิดไว้ใน Prefab) มาวางใต้ parent แล้วเปิด</summary>
    public static T Clone<T>(T template, Transform parent) where T : Component
    {
        T item = UnityEngine.Object.Instantiate(template, parent, false);
        item.gameObject.SetActive(true);
        item.name = template.name.Replace("Template", string.Empty);
        return item;
    }

    /// <summary>ลบลูกที่ Clone มาทั้งหมด (เก็บ Template และลูกที่ระบุไว้)</summary>
    public static void ClearClones(Transform parent, params Component[] keep)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            bool kept = false;
            for (int k = 0; k < keep.Length && !kept; k++)
                kept = keep[k] != null && keep[k].transform == child;

            if (kept)
                continue;

            child.gameObject.SetActive(false); // ออกจาก Layout ทันที
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    /// <summary>คอมโพเนนต์ส่งต่อเมาส์ (ชี้/ออก/คลิกขวา) ใส่ให้ถ้ายังไม่มี</summary>
    public static UIPointerRelay Relay(Component target)
    {
        UIPointerRelay relay = target.GetComponent<UIPointerRelay>();
        return relay != null ? relay : target.gameObject.AddComponent<UIPointerRelay>();
    }
}
