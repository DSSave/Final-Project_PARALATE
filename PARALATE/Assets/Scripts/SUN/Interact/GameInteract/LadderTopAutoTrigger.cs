using UnityEngine;

/// <summary>
/// โซนด้านบนบันได: เดินเข้ามา = เกาะบันไดจากด้านบนอัตโนมัติ (รอกดลง)
/// ไม่ทำงานถ้ากำลังปีนอยู่ (เดิมปีนขึ้นมาชนโซนนี้แล้วหลุดจากบันไดกลางทาง)
/// </summary>
public class LadderTopAutoTrigger : MonoBehaviour
{
    [Header("จุดเริ่มปีนด้านบนสุด")]
    public Transform topStartPoint;

    [Tooltip("บันไดของโซนนี้ (ว่าง = หาเองจากวัตถุพ่อ)")]
    public Ladder ladder;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        TopDownPlayerController player = other.GetComponent<TopDownPlayerController>();

        // กำลังปีน/ถูกล็อกอยู่ = ไม่ต้องเริ่มใหม่ (StartClimbing ตอนปีนอยู่จะกลายเป็น "ปล่อยบันได")
        if (player == null || player.IsMovementLocked || player.IsClimbing || topStartPoint == null)
            return;

        if (ladder == null && transform.parent != null)
            ladder = transform.parent.GetComponentInChildren<Ladder>();

        Vector3 lookDirection = ladder != null ? ladder.ClimbFacing : topStartPoint.forward;
        player.StartClimbing(lookDirection, topStartPoint, ladder);
    }
}
