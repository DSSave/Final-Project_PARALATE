
using UnityEngine;

/// <summary>
/// จุดที่กล้องห้อง (CinemachineCamera + Position Composer) ตาม: ขยับตามผู้เล่นในแกนที่เลือก
///
/// ปัญหาเดิม "จัดมุมใน Editor แล้วตอนเล่นไม่ตรง": ถ้าไม่ติ๊ก Follow Y สคริปต์เดิมบังคับ Anchor ไปอยู่ที่ Fixed Y (2.5)
///   ทั้งที่ใน Editor วาง Anchor ไว้ที่ความสูงอื่น (เช่น -5) กล้องจึงกระโดดสูงขึ้นหลายเมตรตอนกด Play
/// ตอนนี้เลือกได้ด้วย Height Mode:
///   SceneHeight (ค่าเริ่มต้นของ Anchor ใหม่) : ใช้ความสูงที่วางไว้ใน Scene → เห็นใน Editor ยังไง ตอนเล่นสูงเท่านั้น
///   FixedWorldY                           : แบบเดิม ใช้ค่า Fixed Y
///   FollowPlayer                          : ตามความสูงผู้เล่น (เหมือนติ๊ก Follow Y)
/// คลิกขวาที่คอมโพเนนต์ → "Match Runtime Height" = ย้าย Anchor ไปความสูงที่มันจะอยู่ตอนเล่นจริง แล้วสลับเป็น SceneHeight
///   (ตอนเล่นเหมือนเดิมทุกอย่าง แต่ภาพใน Editor/Solo ตรงกับตอนเล่นแล้ว)
/// แกน X/Z: ตอนเล่น Anchor = ตำแหน่งผู้เล่น ดังนั้นตอนจัดมุมใน Editor ให้คิดว่า "ตำแหน่ง Anchor = ผู้เล่นยืนตรงนี้"
/// </summary>
public class CameraFollowAnchor : MonoBehaviour
{
    public enum HeightMode { FixedWorldY, SceneHeight, FollowPlayer }

    [Header("Player to Follow")]
    [SerializeField] private Transform player;

    [Header("Follow Axes")]
    [SerializeField] private bool followX = true;
    [Tooltip("ถ้าติ๊ก = ตามความสูงผู้เล่น (ทับ Height Mode)")]
    [SerializeField] private bool followY = false;
    [SerializeField] private bool followZ = true;

    [Header("ความสูงของ Anchor (เมื่อไม่ติ๊ก Follow Y)")]
    [Tooltip("SceneHeight = ใช้ความสูงที่วางไว้ใน Scene (ตรงกับที่เห็นใน Editor) / FixedWorldY = ใช้ Fixed Y ด้านล่าง (แบบเดิม)")]
    [SerializeField] private HeightMode heightMode = HeightMode.FixedWorldY; // Anchor เดิมคงพฤติกรรมเดิม (Anchor ใหม่ได้ SceneHeight จาก Reset)

    [Header("Follow Offset")]
    [SerializeField] private Vector3 followOffset = Vector3.zero;

    [Header("Fixed World Height (ใช้เมื่อ Height Mode = FixedWorldY)")]
    [SerializeField] private float fixedY = 2.5f;

    [Header("Optional World Bounds")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 xBounds = new Vector2(-10f, 10f);
    [SerializeField] private Vector2 zBounds = new Vector2(-10f, 10f);

    private float sceneY;

    private bool FollowsHeight => followY || heightMode == HeightMode.FollowPlayer;

    private void Reset()
    {
        // เพิ่มคอมโพเนนต์ใหม่: ใช้ความสูงตามที่วางใน Scene
        heightMode = HeightMode.SceneHeight;
    }

    private void Awake()
    {
        sceneY = transform.position.y;
    }

    private void LateUpdate()
    {
        if (player == null)
            return;

        Vector3 pos = transform.position;

        // เลือกว่าจะตามตำแหน่ง Player ในแต่ละแกนหรือไม่
        if (followX)
            pos.x = player.position.x + followOffset.x;

        pos.y = RuntimeY(player.position.y);

        if (followZ)
            pos.z = player.position.z + followOffset.z;

        // จำกัดพื้นที่ที่ Anchor สามารถเคลื่อนที่ได้
        if (useBounds)
        {
            pos.x = Mathf.Clamp(pos.x, xBounds.x, xBounds.y);
            pos.z = Mathf.Clamp(pos.z, zBounds.x, zBounds.y);
        }

        transform.position = pos;
    }

    private float RuntimeY(float playerY)
    {
        if (FollowsHeight)
            return playerY + followOffset.y;

        return heightMode == HeightMode.SceneHeight ? sceneY : fixedY;
    }

    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
    }

#if UNITY_EDITOR
    [ContextMenu("Match Runtime Height (ให้ภาพใน Editor ตรงกับตอนเล่น)")]
    private void MatchRuntimeHeight()
    {
        if (FollowsHeight)
        {
            Debug.Log($"{name}: ตามความสูงผู้เล่นอยู่แล้ว ไม่ต้องปรับ", this);
            return;
        }

        UnityEditor.Undo.RecordObject(this, "Match Runtime Height");
        UnityEditor.Undo.RecordObject(transform, "Match Runtime Height");

        if (heightMode == HeightMode.FixedWorldY)
        {
            Vector3 p = transform.position;
            p.y = fixedY;
            transform.position = p;
        }

        heightMode = HeightMode.SceneHeight;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    // เส้นสีส้ม = Anchor จะไปอยู่ที่ความสูงนี้ตอนเล่น (ถ้าไม่ตรงกับตำแหน่งใน Editor มุมกล้องจะเพี้ยน)
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying || FollowsHeight || heightMode == HeightMode.SceneHeight)
            return;

        Vector3 runtime = transform.position;
        runtime.y = fixedY;

        if (Mathf.Abs(runtime.y - transform.position.y) < 0.05f)
            return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        Gizmos.DrawLine(transform.position, runtime);
        Gizmos.DrawWireSphere(runtime, 0.4f);
    }
#endif
}
