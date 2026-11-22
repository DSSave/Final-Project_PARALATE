using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// กล้องตอนเปิดหน้า Upgrade: กด E แล้วกล้องเลื่อนไปหา "คนอัปเกรด" (โมเดลที่ยืนอยู่ตรงจุด Upgrade)
/// เห็นหน้า/ลำตัวท่อนบน โดยจัดให้อยู่ "ครึ่งขวาของจอ" ส่วนครึ่งซ้ายเป็นหน้าต่าง Upgrade
/// (ถ้าไม่ได้ส่งเป้าหมายมา จะโชว์ตัวผู้เล่นแทนเหมือนเดิม)
///
/// หลักการ:
///   1) สร้าง CinemachineCamera ตอนรัน Priority สูงกว่ากล้องเล่นปกติ → CinemachineBrain เบลนด์ (เลื่อน) ไปหาเอง
///      ปิดหน้าต่าง = ลด Priority เป็น 0 → กล้องเลื่อนกลับกล้องเดิม
///   2) ตั้งกล้องฝั่งที่ผู้เล่นยืน (มองจากมุมของผู้เล่นไปหาคนอัปเกรด) แล้วไล่หามุมที่ "โล่ง" ด้วย SphereCast
///      ตัวผู้เล่นเองก็นับเป็นสิ่งกีดขวาง กล้องจึงเฉียงหลบไม่ให้ตัวผู้เล่นบังหน้าคนอัปเกรด
///   3) หันกล้องเฉียงไปทางซ้ายเล็กน้อย → เป้าหมายไปอยู่ที่ราว 3/4 ของความกว้างจอ
///      มุมที่ต้องเฉียงคำนวณจาก FOV แนวนอน: tan(มุม) = ระยะบนจอ(0..1 จากกลางถึงขอบ) x tan(FOV แนวนอน / 2)
/// </summary>
public static class UpgradeShowcaseCamera
{
    public const int Priority = 3000;

    /// <summary>เป้าหมายอยู่ห่างจากกลางจอไปทางขวาเท่าไร (0 = กลางจอ, 1 = ขอบขวา)</summary>
    public static float ScreenOffset = 0.5f;
    public static float Distance = 2.1f;
    public static float FieldOfView = 35f;
    /// <summary>จุดที่กล้องเล็ง ต่ำกว่าหัวเท่าไร (เมตร) เพื่อให้เห็นลำตัวท่อนบนด้วย</summary>
    public static float LookBelowHead = 0.22f;

    private static readonly float[] CandidateAngles = { 0f, 30f, -30f, 55f, -55f, 80f, -80f, 110f, -110f, 150f, -150f, 180f };

    private static CinemachineCamera showcaseCamera;

    /// <param name="subject">สิ่งที่กล้องจะหันไปหา (คนอัปเกรด) ถ้า null = โชว์ตัวผู้เล่น</param>
    public static void Show(TopDownPlayerController player, Transform subject = null)
    {
        if (player == null && subject == null)
            return;

        if (showcaseCamera == null)
        {
            GameObject go = new GameObject("UpgradeShowcaseCamera (Auto)");
            showcaseCamera = go.AddComponent<CinemachineCamera>();
            showcaseCamera.Lens.NearClipPlane = 0.1f;
        }

        Vector3 head;
        Vector3 preferred; // ทิศจากเป้าหมายไปยังตำแหน่งกล้องที่อยากได้ (แนวราบ)
        int mask;

        if (subject != null)
        {
            head = HeadPosition(subject);

            // ตั้งกล้องฝั่งผู้เล่น: คนอัปเกรดมักหันหน้าหาคนที่เดินมาคุย
            preferred = player != null ? player.transform.position - subject.position : subject.forward;
            preferred.y = 0f;
            if (preferred.sqrMagnitude < 0.01f)
                preferred = subject.forward;
            preferred.Normalize();

            // ไม่ชนตัวเป้าหมายเอง (ผู้เล่นยังนับเป็นสิ่งกีดขวาง → กล้องเฉียงหลบตัวผู้เล่น)
            mask = ~(1 << subject.gameObject.layer);

            // ผู้เล่นหันหน้าเข้าหาคนอัปเกรด
            if (player != null)
                player.transform.rotation = Quaternion.LookRotation(-preferred, Vector3.up);
        }
        else
        {
            head = HeadPosition(player.transform);
            preferred = player.transform.forward;
            mask = ~(1 << player.gameObject.layer);
        }

        Vector3 direction = preferred;
        float distance = Distance;
        bool found = false;

        for (int i = 0; i < CandidateAngles.Length; i++)
        {
            Vector3 candidate = Quaternion.Euler(0f, CandidateAngles[i], 0f) * preferred;
            if (Physics.SphereCast(head, 0.25f, candidate, out _, Distance + 0.3f, mask, QueryTriggerInteraction.Ignore))
                continue;

            direction = candidate;
            found = true;
            break;
        }

        // ทุกทิศมีของขวาง (ห้องแคบมาก): ใช้ทิศแรก แล้วขยับกล้องเข้ามาเท่าที่มีที่ว่าง
        if (!found && Physics.SphereCast(head, 0.25f, direction, out RaycastHit hit, Distance, mask, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(0.8f, hit.distance - 0.1f);

        // โหมดโชว์ตัวผู้เล่น: ตัวละครหันหน้าเข้ากล้อง
        if (subject == null)
            player.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        Vector3 target = head + Vector3.down * LookBelowHead;
        Vector3 position = target + direction * distance;
        Quaternion lookAtTarget = Quaternion.LookRotation(target - position, Vector3.up);

        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        float halfHorizontalFov = Mathf.Atan(Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * aspect);
        float shiftAngle = Mathf.Atan(ScreenOffset * Mathf.Tan(halfHorizontalFov)) * Mathf.Rad2Deg;

        // หันกล้องไปทางซ้าย → สิ่งที่อยู่ตรงกลางเลื่อนไปทางขวาของจอ
        Quaternion rotation = Quaternion.AngleAxis(-shiftAngle, Vector3.up) * lookAtTarget;

        showcaseCamera.transform.SetPositionAndRotation(position, rotation);
        showcaseCamera.Lens.FieldOfView = FieldOfView;
        showcaseCamera.Priority.Value = Priority;
    }

    public static void Hide()
    {
        if (showcaseCamera != null)
            showcaseCamera.Priority.Value = 0;
    }

    // ตำแหน่งหัว: โมเดล Humanoid ใช้กระดูก Head / โมเดลอื่น (เช่นแคปซูลแทนตัว) ใช้ขอบบนของ Renderer
    private static Vector3 HeadPosition(Transform subject)
    {
        Animator animator = subject.GetComponentInChildren<Animator>();
        Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head != null)
            return head.position;

        Renderer[] renderers = subject.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            // หัวอยู่ต่ำกว่ายอดประมาณ 12% ของความสูง
            return new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * 0.12f, bounds.center.z);
        }

        return subject.position + Vector3.up * 1.5f;
    }
}
