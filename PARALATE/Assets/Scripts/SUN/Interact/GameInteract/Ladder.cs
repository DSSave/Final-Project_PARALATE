using UnityEngine;

/// <summary>
/// บันได: กด E ในโซนเพื่อเกาะ/ปล่อยบันได
///
/// ตำแหน่งตอนปีนคำนวณจากตัวบันไดจริง (Climb Collider = Collider ทึบใน Layer Ladder ของบันไดนี้):
///   - แนวราบ: อยู่กึ่งกลางความกว้างบันได (+ Lateral Offset) ห่างหน้าบันได Stand Distance
///   - หันหน้า: ตามทิศ Climb Start Point (ลูกศรสีน้ำเงินชี้เข้าหาบันได)
///   - ขึ้นสูงสุด: ยอด Collider (+ Top Reach Offset) แล้วก้าวขึ้นพื้นด้านบนเอง
/// เลือกวัตถุนี้แล้วจะเห็นเส้นสีเหลือง = แนวที่ตัวผู้เล่นจะเลื่อนขึ้นลง (ปรับตัวเลขแล้วดูเส้นได้ทันที)
/// </summary>
[RequireComponent(typeof(InteractableObject))]
public class Ladder : MonoBehaviour, IInteractPromptSource
{
    [Header("การตั้งค่าบันได")]
    public Transform climbStartPoint;

    [Tooltip("Collider ทึบของตัวบันได (ว่าง = หาเองจากวัตถุพ่อ: Collider ที่ไม่ใช่ Trigger)")]
    public Collider climbCollider;

    [Tooltip("ระยะจากหน้าบันไดถึงจุดกลางตัวผู้เล่นตอนปีน (เมตร) — คลิปปีนยื่นมือไปข้างหน้าราว 0.3 ม.")]
    public float standDistance = 0.3f;

    [Tooltip("เลื่อนตัวไปซ้าย(-)/ขวา(+) จากกึ่งกลางบันได (เมตร) ถ้าท่าปีนดูเยื้อง")]
    public float lateralOffset = 0f;

    [Tooltip("เท้าขึ้นไปถึงระดับไหนแล้วก้าวขึ้นพื้นด้านบน (เทียบกับยอดบันได, เมตร) / ติดลบ = ก้าวขึ้นก่อนเท้าถึงยอด")]
    public float topReachOffset = -0.6f;

    [Header("ข้อความ Prompt")]
    public string enterPrompt = "กด E เพื่อปีน";
    [Tooltip("โชว์ตอนกำลังเกาะบันไดอยู่ (กด E อีกครั้งเพื่อปล่อย) เว้นว่าง = ไม่โชว์")]
    public string exitPrompt = "กด E เพื่อลงจากบันได";

    private InteractableObject interactable;
    private GameObject cachedPlayerObject;
    private TopDownPlayerController cachedPlayer;

    void Start()
    {
        interactable = GetComponent<InteractableObject>();
    }

    // ------------------------------------------------------------ ข้อมูลบันได

    public Collider ClimbCollider
    {
        get
        {
            if (climbCollider == null)
            {
                Transform root = transform.parent != null ? transform.parent : transform;
                foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                {
                    if (!c.isTrigger)
                    {
                        climbCollider = c;
                        break;
                    }
                }
            }

            return climbCollider;
        }
    }

    /// <summary>ทิศที่ผู้เล่นหันตอนปีน (เข้าหาบันได แนวราบ)</summary>
    public Vector3 ClimbFacing
    {
        get
        {
            Vector3 forward = climbStartPoint != null ? climbStartPoint.forward : -transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }
    }

    public float TopY => ClimbCollider != null ? ClimbCollider.bounds.max.y : transform.position.y + 3f;
    public float BottomY => ClimbCollider != null ? ClimbCollider.bounds.min.y : transform.position.y;

    /// <summary>จุดกลางตัวผู้เล่นตอนปีนที่ความสูง y: กึ่งกลางบันได ห่างหน้าบันได Stand Distance</summary>
    public Vector3 GetClimbPosition(float y)
    {
        Vector3 facing = ClimbFacing;
        Vector3 right = Vector3.Cross(Vector3.up, facing);
        Collider col = ClimbCollider;

        Vector3 center = col != null ? col.bounds.center : transform.position;

        // หน้าบันได = จุดบน Collider ที่ใกล้ที่สุดเมื่อมองจากฝั่งผู้เล่น
        Vector3 face = col != null ? col.ClosestPoint(center - facing * 10f) : center;
        float faceDistance = Vector3.Dot(face - center, -facing); // ระยะจากกลางบันไดถึงหน้าบันได

        Vector3 position = center + (-facing) * (faceDistance + standDistance) + right * lateralOffset;
        position.y = y;
        return position;
    }

    // ------------------------------------------------------------ กด E

    public void ActivateLadder()
    {
        if (interactable.interactingPlayer != null)
        {
            TopDownPlayerController player = interactable.interactingPlayer.GetComponent<TopDownPlayerController>();

            if (player != null && climbStartPoint != null)
                player.StartClimbing(ClimbFacing, climbStartPoint, this);
        }
    }

    // InteractableObject บนวัตถุเดียวกันจะเรียกใช้ทุกเฟรมที่ผู้เล่นอยู่ในโซน
    public string GetPromptMessage(GameObject player)
    {
        if (player != cachedPlayerObject)
        {
            cachedPlayerObject = player;
            cachedPlayer = player != null ? player.GetComponentInParent<TopDownPlayerController>() : null;
        }

        return cachedPlayer != null && cachedPlayer.IsClimbing ? exitPrompt : enterPrompt;
    }

#if UNITY_EDITOR
    // เส้นเหลือง = แนวที่ตัวผู้เล่น (จุดกลางตัว) เลื่อนขึ้นลงตอนปีน
    private void OnDrawGizmosSelected()
    {
        if (ClimbCollider == null)
            return;

        // ใช้ความสูงเท้า (จุดเริ่ม/จุดก้าวขึ้น) ไม่ใช่จุดกลางตัว
        Vector3 bottom = GetClimbPosition(BottomY);
        Vector3 top = GetClimbPosition(TopY + topReachOffset);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(bottom, top);
        Gizmos.DrawWireSphere(bottom, 0.15f);
        Gizmos.DrawWireSphere(top, 0.15f);
        Gizmos.DrawRay(top, ClimbFacing * 0.6f);
    }
#endif
}
