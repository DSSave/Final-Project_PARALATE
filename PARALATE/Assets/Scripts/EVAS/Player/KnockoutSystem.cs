using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// ระบบจู่โจมจากด้านหลัง (ทำได้เมื่อศัตรูยังไม่รู้ตัว: ไม่ได้ Alert และผู้เล่นอยู่นอกกรวยสายตา)
///
///   1) กด F ด้านหลังศัตรู → เข้าสถานะ Strangle (ล็อกตัวกันทั้งคู่) แล้วเลือกต่อ:
///   2a) กด F อีกครั้ง      → เอามีดแทงคอ ศัตรูตาย ศพนอนอยู่บนพื้น (มีมีดในคลัง = เร็วขึ้น)
///   2b) กด Ctrl ค้าง X วินาที → รัดคอจนหมดสติ ร่างหายไปจากฉาก (X ตั้งที่ EnemyHealth.chokeHoldSeconds ตามชนิดศัตรู)
///       ปล่อย Ctrl ก่อนครบ = หลอดเริ่มใหม่ แต่ยังรัดคออยู่
///   ไม่ทำอะไรนานเกิน Strangle Timeout → ศัตรูดิ้นหลุดและไล่ล่าผู้เล่น
///
/// อนิเมชั่น: ตั้งชื่อ Trigger ไว้ด้านล่าง ถ้าใน Animator Controller ของผู้เล่น/ศัตรูมี Trigger ชื่อนั้นจะเล่นให้เอง
/// (ยังไม่มีคลิป = ข้ามไป ระบบยังทำงานด้วยเวลาที่ตั้งไว้)
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class KnockoutSystem : MonoBehaviour
{
    private enum State { Idle, Strangle, Stab, Choke }

    [Header("Range")]
    public float actionRange = 2f;
    public float actionRadius = 0.45f;
    public Vector3 actionRaycastOffset = new Vector3(0f, 1f, 0f);
    public LayerMask targetMask = ~0;

    [Header("Stealth Rules")]
    public bool requireTargetOutsideVisionCone = true;
    [Tooltip("ศัตรูที่ Alert/ไล่ล่าอยู่ จู่โจมไม่ได้")]
    public bool requireEnemyUnaware = true;

    [Header("Strangle (F)")]
    [Tooltip("ค้างอยู่ในท่า Strangle โดยไม่เลือกอะไรนานเท่านี้ ศัตรูจะดิ้นหลุด (0 = ไม่หลุด)")]
    [Min(0f)] public float strangleTimeout = 6f;
    [Tooltip("ศัตรูดิ้นหลุดแล้วไล่ล่าอย่างน้อยกี่วินาที")]
    public float breakFreeChaseTime = 4f;

    [Header("Kill: แทงคอ (F ระหว่าง Strangle)")]
    public float stabDuration = 1.4f;
    [Tooltip("มีมีดในคลัง: เวลาแทงคูณค่านี้")]
    [Range(0.1f, 1f)] public float knifeOwnedMultiplier = 0.6f;

    [Header("Knockout: รัดคอ (Ctrl ค้างระหว่าง Strangle)")]
    [Tooltip("ใช้เมื่อศัตรูไม่มี EnemyHealth.chokeHoldSeconds")]
    public float defaultChokeHoldSeconds = 3f;
    [Tooltip("ครบเวลาแล้ว รออนิเมชั่นจบกี่วินาทีก่อนร่างหาย")]
    public float chokeFinishDelay = 0.8f;

    [Header("Animation Triggers (ผู้เล่น)")]
    public string playerStrangleTrigger = "Strangle";
    public string playerStabTrigger = "StabKill";
    public string playerChokeTrigger = "Choke";
    public string playerReleaseTrigger = "StrangleRelease";

    [Header("Animation Triggers (ศัตรู)")]
    public string enemyStrangledTrigger = "Strangled";
    public string enemyStabbedTrigger = "StabbedDeath";
    public string enemyChokedTrigger = "Choked";

    [Header("UI")]
    [Tooltip("ไม่บังคับ: ถ้าเว้นว่างจะหา KnockoutPromptUI ใน Scene เอง")]
    public KnockoutPromptUI promptUI;
    public string approachPrompt = "[F] to Strangle";
    public string stranglePrompt = "[F] Kill   [Ctrl hold] Knockout";
    [Tooltip("ความถี่ในการเช็คว่าอยู่ข้างหลังศัตรูหรือไม่ (วินาที)")]
    public float promptCheckInterval = 0.1f;

    private static readonly RaycastHit[] actionHits = new RaycastHit[16];

    private PlayerInput playerInput;
    private InputAction killAction;
    private InputAction knockOutAction;
    private TopDownPlayerController movementController;

    private State state = State.Idle;
    private EnemyHealth target;
    private float strangleTimer;
    private float chokeTimer;
    private bool chokeHolding;
    private float promptCheckTimer;
    private bool promptShown;

    /// <summary>กำลังจู่โจมอยู่ (ระบบอื่นใช้กันการเปลี่ยนอาวุธ/ยิง)</summary>
    public bool IsKnockout => state != State.Idle;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        killAction = playerInput.actions["Kill"];
        knockOutAction = playerInput.actions["KnockOut"];
        movementController = GetComponent<TopDownPlayerController>();
    }

    void Start()
    {
        if (promptUI == null)
            promptUI = FindAnyObjectByType<KnockoutPromptUI>(FindObjectsInactive.Include);
    }

    void OnDisable()
    {
        if (state != State.Idle)
            Release(false);

        SetPromptVisible(false);
        promptUI?.EndCountdown();
    }

    void Update()
    {
        if (PlayerHealth.PlayerIsDead)
        {
            if (state != State.Idle)
                Release(false);
            SetPromptVisible(false);
            return;
        }

        switch (state)
        {
            case State.Idle: UpdateIdle(); break;
            case State.Strangle: UpdateStrangle(); break;
        }
    }

    // ------------------------------------------------------------ Idle: หาเป้าหมายด้านหลัง

    void UpdateIdle()
    {
        if (movementController != null && movementController.IsMovementLocked)
        {
            SetPromptVisible(false);
            return;
        }

        if (killAction.WasPressedThisFrame() && Time.timeScale > 0f && TryGetTarget(out EnemyHealth enemy))
        {
            BeginStrangle(enemy);
            return;
        }

        promptCheckTimer -= Time.deltaTime;
        if (promptCheckTimer > 0f)
            return;

        promptCheckTimer = promptCheckInterval;
        bool canAct = TryGetTarget(out _);
        if (canAct)
            promptUI?.SetPromptText(approachPrompt);
        SetPromptVisible(canAct);
    }

    void BeginStrangle(EnemyHealth enemy)
    {
        state = State.Strangle;
        target = enemy;
        strangleTimer = 0f;
        chokeTimer = 0f;
        chokeHolding = false;

        SetPlayerMovementLocked(true);
        SetEnemyMovementLocked(enemy, true);
        FaceTarget(enemy);

        Trigger(PlayerAnimator, playerStrangleTrigger);
        Trigger(EnemyAnimator(enemy), enemyStrangledTrigger);
        SfxLibrary.Play("strangle", enemy.transform.position);

        promptUI?.SetPromptText(stranglePrompt);
        SetPromptVisible(true, true);
    }

    // ------------------------------------------------------------ Strangle: เลือก Kill / Knockout

    void UpdateStrangle()
    {
        if (target == null || target.IsDead)
        {
            Release(false);
            return;
        }

        if (Time.timeScale <= 0f)
            return; // เมนูเปิดอยู่

        if (killAction.WasPressedThisFrame())
        {
            StartCoroutine(StabRoutine());
            return;
        }

        HandleChokeHold();
        if (state != State.Strangle)
            return;

        // นับเวลาดิ้นหลุดเฉพาะตอนไม่ได้กด Ctrl
        if (!chokeHolding && strangleTimeout > 0f)
        {
            strangleTimer += Time.deltaTime;
            if (strangleTimer >= strangleTimeout)
                Release(true);
        }
    }

    void HandleChokeHold()
    {
        float holdSeconds = target.chokeHoldSeconds > 0f ? target.chokeHoldSeconds : defaultChokeHoldSeconds;

        if (knockOutAction.IsPressed())
        {
            if (!chokeHolding)
            {
                chokeHolding = true;
                chokeTimer = 0f;
                promptUI?.BeginCountdown(false, holdSeconds);
            }

            chokeTimer += Time.deltaTime;
            promptUI?.SetRemaining(holdSeconds - chokeTimer);

            if (chokeTimer >= holdSeconds)
                StartCoroutine(ChokeRoutine());

            return;
        }

        if (chokeHolding)
        {
            // ปล่อยก่อนครบ: หลอดเริ่มใหม่ แต่ยังรัดคออยู่
            chokeHolding = false;
            chokeTimer = 0f;
            promptUI?.ReleaseEarly();
        }
    }

    IEnumerator StabRoutine()
    {
        state = State.Stab;
        EnemyHealth enemy = target;
        SetPromptVisible(false, true);

        bool ownsKnife = PersistentInventory.Instance != null && PersistentInventory.Instance.Has(ItemCatalog.WeaponKnife);
        float duration = stabDuration * (ownsKnife ? knifeOwnedMultiplier : 1f);

        Trigger(PlayerAnimator, playerStabTrigger);
        Trigger(EnemyAnimator(enemy), enemyStabbedTrigger);
        promptUI?.BeginCountdown(true, duration);

        float remaining = duration;
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            promptUI?.SetRemaining(Mathf.Max(0f, remaining));
            yield return null;
        }

        promptUI?.EndCountdown();

        if (enemy != null && !enemy.IsDead)
        {
            enemy.Kill(EnemyHealth.DeathAction.FallOver); // ศพนอนบนพื้น
            SfxLibrary.Play("stealth_kill", enemy.transform.position);
        }

        Finish(enemy);
    }

    IEnumerator ChokeRoutine()
    {
        state = State.Choke;
        EnemyHealth enemy = target;
        SetPromptVisible(false, true);
        promptUI?.EndCountdown();

        Trigger(PlayerAnimator, playerChokeTrigger);
        Trigger(EnemyAnimator(enemy), enemyChokedTrigger);

        yield return new WaitForSeconds(chokeFinishDelay);

        if (enemy != null && !enemy.IsDead)
        {
            SfxLibrary.Play("knockout", enemy.transform.position);
            enemy.Vanish(); // ร่างจากการรัดคอหายไปจากฉาก
        }

        Finish(enemy);
    }

    // จบการจู่โจม (สำเร็จ): ปลดล็อกผู้เล่น
    void Finish(EnemyHealth enemy)
    {
        SetEnemyMovementLocked(enemy, false);
        target = null;
        state = State.Idle;
        chokeHolding = false;
        SetPlayerMovementLocked(false);
    }

    /// <summary>ปล่อยศัตรู (ยกเลิก) breakFree = ศัตรูดิ้นหลุดแล้วไล่ล่าผู้เล่น</summary>
    void Release(bool breakFree)
    {
        StopAllCoroutines();
        EnemyHealth enemy = target;

        promptUI?.EndCountdown();
        SetPromptVisible(false, true);
        Trigger(PlayerAnimator, playerReleaseTrigger);

        SetEnemyMovementLocked(enemy, false);
        target = null;
        state = State.Idle;
        chokeHolding = false;
        SetPlayerMovementLocked(false);

        if (breakFree)
            StartEnemyChase(enemy, breakFreeChaseTime);
    }

    // ------------------------------------------------------------ เป้าหมาย

    bool TryGetTarget(out EnemyHealth enemy)
    {
        Vector3 origin = transform.position
                         + (transform.right * actionRaycastOffset.x)
                         + (transform.up * actionRaycastOffset.y)
                         + (transform.forward * actionRaycastOffset.z);

        return TryFindEnemyInActionArc(origin, transform.forward, out enemy);
    }

    bool CanTarget(EnemyHealth enemy)
    {
        if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
            return false;

        // ศัตรูที่สลบ (จากระบบอื่น): จู่โจมได้จากทุกทิศ
        if (enemy.IsStunned)
            return true;

        if (requireEnemyUnaware)
        {
            AlertState alert = enemy.GetComponent<AlertState>();
            if (alert != null && alert.IsAlert)
                return false;
        }

        if (!requireTargetOutsideVisionCone)
            return true;

        FieldOfView fieldOfView = enemy.GetComponent<FieldOfView>();
        return fieldOfView == null || !fieldOfView.IsTargetInVisionCone(transform);
    }

    bool TryFindEnemyInActionArc(Vector3 origin, Vector3 direction, out EnemyHealth enemy)
    {
        enemy = null;
        direction = direction.normalized;

        if (actionRadius <= 0f)
        {
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, actionRange, targetMask, QueryTriggerInteraction.Ignore))
                return false;

            if (hit.collider.transform.IsChildOf(transform))
                return false;

            enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            return CanTarget(enemy);
        }

        int hitCount = Physics.SphereCastNonAlloc(origin, actionRadius, direction, actionHits, actionRange, targetMask, QueryTriggerInteraction.Ignore);
        if (hitCount == 0)
            return false;

        System.Array.Sort(actionHits, 0, hitCount, RaycastHitDistanceComparer.Instance);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = actionHits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                continue;

            enemy = hitCollider.GetComponentInParent<EnemyHealth>();
            return CanTarget(enemy);
        }

        enemy = null;
        return false;
    }

    // ------------------------------------------------------------ ตัวช่วย

    void FaceTarget(EnemyHealth enemy)
    {
        Vector3 toEnemy = enemy.transform.position - transform.position;
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(toEnemy);
    }

    Animator PlayerAnimator => movementController != null ? movementController.animator : GetComponentInChildren<Animator>();

    static Animator EnemyAnimator(EnemyHealth enemy) => enemy != null ? enemy.GetComponentInChildren<Animator>() : null;

    /// <summary>เล่น Trigger ถ้า Animator มีพารามิเตอร์ชื่อนี้ (ยังไม่ได้ใส่คลิป = ข้าม ไม่ขึ้น Warning)</summary>
    static void Trigger(Animator animator, string trigger)
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

    void SetPromptVisible(bool visible, bool force = false)
    {
        if (promptUI == null || (!force && promptShown == visible))
            return;

        promptShown = visible;
        promptUI.SetPromptVisible(visible);
    }

    void SetPlayerMovementLocked(bool locked)
    {
        if (movementController == null)
            movementController = GetComponent<TopDownPlayerController>();

        if (movementController != null)
            movementController.SetMovementLocked(this, locked);
    }

    void SetEnemyMovementLocked(EnemyHealth enemy, bool locked)
    {
        if (enemy == null)
            return;

        EnemyController enemyController = enemy.GetComponent<EnemyController>();
        if (enemyController != null)
            enemyController.SetMovementLocked(this, locked);
    }

    static void StartEnemyChase(EnemyHealth enemy, float minimumChaseTime)
    {
        if (enemy == null || enemy.IsDead || enemy.IsStunned)
            return;

        AlertState alert = enemy.GetComponent<AlertState>();
        TopDownPlayerController player = FindAnyObjectByType<TopDownPlayerController>();

        if (alert != null && player != null)
            alert.ReceiveSpotterReport(player.transform.position, minimumChaseTime);
        else
            enemy.GetComponent<EnemyController>()?.StartChase(minimumChaseTime);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 actionOrigin = transform.position
                               + (transform.right * actionRaycastOffset.x)
                               + (transform.up * actionRaycastOffset.y)
                               + (transform.forward * actionRaycastOffset.z);

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(actionOrigin, actionOrigin + transform.forward * actionRange);

        if (actionRadius <= 0f)
            return;

        Gizmos.DrawWireSphere(actionOrigin, actionRadius);
        Gizmos.DrawWireSphere(actionOrigin + transform.forward * actionRange, actionRadius);
    }
}
