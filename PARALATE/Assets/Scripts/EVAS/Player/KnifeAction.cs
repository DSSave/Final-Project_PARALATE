using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// มีด (ช่อง Knife ของ WeaponWheel ติดตัวตั้งแต่เริ่ม): คลิกซ้ายเพื่อฟัน "ด้านหน้าตัวละคร"
/// ระหว่างฟันผู้เล่นหยุดอยู่กับที่ (ล็อกการเดินด้วยระบบล็อกแบบมีเจ้าของ) และเล่น Animator Trigger "KnifeSlash" ถ้ามี
/// จังหวะ: กดฟัน → รอ Hit Delay (ง้างมีด) → ตรวจโดนศัตรูตรงหน้า → รอจนครบ Slash Duration → เดินได้อีกครั้ง
/// </summary>
public class KnifeAction : MonoBehaviour
{
    [Header("Knife Settings")]
    public float damage = 25f;
    public float attackRange = 2f; // ระยะมีด (หน่วยเมตร) ถ้า prefab เคย override ไว้ต้องแก้ใน Inspector ด้วย
    [Tooltip("เวลาขั้นต่ำระหว่างการฟันแต่ละครั้ง (วินาที)")]
    public float attackRate = .5f;

    [Header("Slash")]
    [Tooltip("ช่วงเวลาที่ผู้เล่นหยุดเดินระหว่างฟัน (วินาที) ควรเท่ากับความยาวคลิปฟัน")]
    public float slashDuration = 0.45f;
    [Tooltip("ฟันไปแล้วกี่วินาทีถึงนับว่าโดน (จังหวะใบมีดผ่านหน้า)")]
    public float hitDelay = 0.15f;
    [Tooltip("ความกว้างของการฟัน (รัศมี SphereCast) ให้ฟันโดนง่ายขึ้นเล็กน้อย")]
    public float hitRadius = 0.35f;
    [Tooltip("ชื่อ Trigger ใน Animator ของผู้เล่น (ยังไม่มีคลิป = ข้าม)")]
    public string slashTrigger = "KnifeSlash";

    [Header("Raycast Settings")]
    public Vector3 raycastOffset = new Vector3(0f, 1f, 0f); // X=ซ้ายขวา, Y=ขึ้นลง, Z=หน้าหลัง

    [Header("State")]
    public bool isHeld = false;

    [Header("Input Actions")]
    [Tooltip("ไม่บังคับ: ถ้าเว้นว่างจะใช้ action \"Fire\" จาก PlayerInput ของผู้ถือ")]
    public InputActionReference attackAction;

    [Header("Player Reference")]
    public Transform playerTransform;

    private float nextAttackTime;
    private TopDownPlayerController ownerController;
    private InputAction resolvedAttackAction;
    private Transform resolvedFor;
    private Coroutine slashRoutine;

    public bool IsSlashing => slashRoutine != null;

    private void OnDisable()
    {
        // ถูกเก็บกลางการฟัน: ปลดล็อกการเดินคืน
        if (slashRoutine != null)
        {
            StopCoroutine(slashRoutine);
            slashRoutine = null;
            ResolveOwner()?.SetMovementLocked(this, false);
        }
    }

    private void Update()
    {
        if (!isHeld || slashRoutine != null)
            return;

        InputAction action = ResolveAttackAction();
        if (action == null || !action.WasPressedThisFrame())
            return;

        if (Time.time < nextAttackTime || Time.timeScale <= 0f || IsOwnerLocked() || CommandLineUI.IsTyping)
            return;

        nextAttackTime = Time.time + Mathf.Max(attackRate, slashDuration);
        slashRoutine = StartCoroutine(Slash());
    }

    private InputAction ResolveAttackAction()
    {
        Transform owner = playerTransform != null ? playerTransform : transform.parent;

        if (resolvedAttackAction != null && resolvedFor == owner)
            return resolvedAttackAction;

        resolvedFor = owner;
        resolvedAttackAction = null;
        ownerController = null;

        // ใช้ PlayerInput ของผู้ถือก่อน (action map ของผู้เล่นเปิดใช้งานอยู่แน่นอน)
        PlayerInput playerInput = owner != null ? owner.GetComponentInParent<PlayerInput>() : null;
        if (playerInput != null && playerInput.actions != null)
            resolvedAttackAction = playerInput.actions.FindAction("Fire", false);

        if (resolvedAttackAction == null && attackAction != null && attackAction.action != null)
        {
            resolvedAttackAction = attackAction.action;
            resolvedAttackAction.Enable();
        }

        return resolvedAttackAction;
    }

    private TopDownPlayerController ResolveOwner()
    {
        if (ownerController == null)
        {
            ownerController = playerTransform != null
                ? playerTransform.GetComponent<TopDownPlayerController>()
                : GetComponentInParent<TopDownPlayerController>();
        }

        return ownerController;
    }

    // กำลังถูกล็อก (อ่านโน้ต, รัดคอ, ตาย, เปิดเมนู ฯลฯ) ห้ามฟัน
    private bool IsOwnerLocked()
    {
        TopDownPlayerController owner = ResolveOwner();
        return owner != null && owner.IsMovementLocked;
    }

    private IEnumerator Slash()
    {
        TopDownPlayerController owner = ResolveOwner();
        owner?.SetMovementLocked(this, true);

        if (owner != null)
            TriggerIfExists(owner.animator, slashTrigger);

        Transform attackTransform = playerTransform != null ? playerTransform : transform;
        SfxLibrary.Play("knife_swing", attackTransform.position);

        yield return new WaitForSeconds(hitDelay);
        Attack(attackTransform);

        yield return new WaitForSeconds(Mathf.Max(0f, slashDuration - hitDelay));

        owner?.SetMovementLocked(this, false);
        slashRoutine = null;
    }

    // ฟันตรงหน้าตัวละคร (ทิศที่ตัวละครหันอยู่ ไม่ใช่ทิศเมาส์)
    private void Attack(Transform attackTransform)
    {
        Vector3 attackOrigin = attackTransform.position
                               + (attackTransform.right * raycastOffset.x)
                               + (attackTransform.up * raycastOffset.y)
                               + (attackTransform.forward * raycastOffset.z);

        Vector3 attackDirection = attackTransform.forward;
        Debug.DrawRay(attackOrigin, attackDirection * attackRange, Color.red, 2f);

        RaycastHit[] hits = Physics.SphereCastAll(attackOrigin, hitRadius, attackDirection, attackRange,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        // โดนศัตรูตัวที่ใกล้ที่สุดตัวเดียว (ข้ามตัวผู้เล่นเอง)
        EnemyHealth closest = null;
        float closestDistance = float.MaxValue;
        Vector3 hitPoint = attackOrigin;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.transform.IsChildOf(attackTransform))
                continue;

            EnemyHealth enemy = hits[i].collider.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead || hits[i].distance >= closestDistance)
                continue;

            closest = enemy;
            closestDistance = hits[i].distance;
            hitPoint = hits[i].point;
        }

        if (closest != null)
        {
            closest.TakeDamage(damage);
            SfxLibrary.Play("knife_hit", hitPoint);
        }
    }

    private static void TriggerIfExists(Animator animator, string trigger)
    {
        if (animator == null || string.IsNullOrEmpty(trigger) || animator.runtimeAnimatorController == null)
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == trigger)
            {
                animator.SetTrigger(trigger);
                return;
            }
        }
    }
}
