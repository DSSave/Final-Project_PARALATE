using UnityEngine;

/// <summary>
/// HitBox หัวของศัตรูสำหรับ Headshot: GunAction เช็กว่ากระสุนผ่าน Collider นี้ไหม แล้วคูณดาเมจตามปืน
/// EnemyHealth สร้างให้อัตโนมัติ (หากระดูก Head ของโมเดลเอง) จึงไม่ต้องแก้ Prefab ศัตรู
/// ถ้าอยากปรับตำแหน่ง/ขนาดเอง: สร้างลูกของศัตรู ใส่ SphereCollider + สคริปต์นี้ไว้ล่วงหน้า ระบบจะใช้ตัวนั้นแทน
/// </summary>
[RequireComponent(typeof(Collider))]
public class HeadHitbox : MonoBehaviour
{
    public EnemyHealth owner;

    private const float HeadRadius = 0.17f; // เมตร (โลก)

    public static void EnsureOn(EnemyHealth enemy)
    {
        if (enemy == null)
            return;

        HeadHitbox existing = enemy.GetComponentInChildren<HeadHitbox>(true);
        if (existing != null)
        {
            existing.owner = enemy;
            return;
        }

        Transform head = FindHeadBone(enemy.transform);

        GameObject go = new GameObject("HeadHitbox");
        go.layer = enemy.gameObject.layer;

        if (head != null)
        {
            go.transform.SetParent(head, false);
            // ยกขึ้นจากข้อต่อคอเล็กน้อยให้อยู่กลางศีรษะ (แปลงเป็นหน่วย local ของกระดูก)
            go.transform.position = head.position + Vector3.up * 0.08f;
        }
        else
        {
            // ไม่มีกระดูกหัว: วางที่ปลายบนของ Capsule
            go.transform.SetParent(enemy.transform, false);
            CapsuleCollider capsule = enemy.GetComponent<CapsuleCollider>();
            float top = capsule != null ? capsule.center.y + capsule.height * 0.5f : 1.8f;
            go.transform.localPosition = new Vector3(0f, top - HeadRadius, 0f);
        }

        // โมเดล FBX มักมี scale ไม่ใช่ 1: หารด้วย scale เพื่อให้รัศมีในโลกคงที่
        float scale = Mathf.Max(0.0001f, Mathf.Abs(go.transform.lossyScale.x));
        SphereCollider sphere = go.AddComponent<SphereCollider>();
        sphere.radius = HeadRadius / scale;

        go.AddComponent<HeadHitbox>().owner = enemy;
    }

    private static Transform FindHeadBone(Transform root)
    {
        Animator animator = root.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            Transform bone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (bone != null)
                return bone;
        }

        // โมเดลแบบ Generic: หาจากชื่อกระดูก
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string lower = all[i].name.ToLowerInvariant();
            if (lower.EndsWith("head") && !lower.Contains("headtop"))
                return all[i];
        }

        return null;
    }
}
