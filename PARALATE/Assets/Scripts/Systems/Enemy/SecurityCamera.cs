using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ศัตรูชนิดใหม่: กล้องวงจรปิด หันกวาดซ้าย-ขวา ถ้าเห็นผู้เล่นนานพอจะเรียกศัตรูมาไล่ล่า
///
/// การทำงาน (State Machine 3 สถานะ):
///   Sweep  : หมุนไปหาปลายซ้าย/ขวาด้วยความเร็ว Turn Speed
///   Hold   : ถึงปลายแล้วค้าง Hold Time วินาที แล้วสลับไปอีกฝั่ง
///   Track  : เห็นผู้เล่น → หยุดกวาด หันตาม และนับเวลา Detect Time ครบแล้ว Alert
///
/// กรวยสายตาแบบ "ตัดปลาย": ใต้กล้องมีจุดบอด (Near Blind Distance) ผู้เล่นเดินลอดใต้กล้องได้
///   ระยะ Near Blind Distance ถึง View Radius เท่านั้นที่กล้องเห็น (รูปสี่เหลี่ยมคางหมูโค้งบนพื้น)
///
/// ศัตรูที่ถูกเรียกเมื่อ Alert (ใช้ข้อแรกที่มี):
///   1) Linked Enemies : ลากศัตรูใส่เองใน Inspector
///   2) Room           : RoomZone ของห้อง (ว่าง = หา RoomZone ที่ผู้เล่น/กล้องยืนอยู่เอง)
///   3) Alert Radius   : ศัตรูทุกตัวในรัศมีรอบกล้อง
/// Act As Enemy Eyes: หลัง Alert ตราบใดที่กล้องยังเห็นผู้เล่น กล้องจะส่งตำแหน่งผู้เล่นให้ศัตรูกลุ่มนั้นทุก Report Interval
///   ศัตรูจะไล่ล่าผู้เล่นตรงๆ เหมือนเห็นเอง (กล้อง = ตาของศัตรู)
///
/// ยิงพังได้: เลือด Max Health (ค่าเริ่ม 10) หมดแล้วกล้องพัง หัวก้มลง กรวยสายตาดับ และไม่ทำอะไรอีก
///   ต้องมี Collider ที่ตัวกล้อง (ถ้าไม่มี สคริปต์ใส่ BoxCollider ให้ชิ้นส่วนที่มี Mesh เอง)
///
/// วิธีใช้: ลาก Prefab SecurityCamera ไปติดผนัง/เพดาน ให้แกน Z (ลูกศรสีน้ำเงิน) ชี้ไปทางกึ่งกลางที่ต้องการให้มอง
/// </summary>
public class SecurityCamera : MonoBehaviour, IShootable
{
    private enum State { Sweep, Hold, Track }

    [Header("References")]
    [Tooltip("ส่วนที่หมุน (ว่าง = ตัววัตถุนี้เอง)")]
    public Transform head;

    [Header("Sweep (หันซ้าย-ขวา)")]
    [Tooltip("องศาที่หันไปแต่ละฝั่งจากทิศตั้งต้น")]
    [Range(0f, 170f)] public float sweepAngle = 50f;
    [Tooltip("ความเร็วการหัน (องศา/วินาที)")]
    public float turnSpeed = 25f;
    [Tooltip("เวลาที่ค้างตอนหันสุดซ้าย/ขวา (วินาที)")]
    public float holdTime = 1.5f;

    [Header("Vision")]
    public float viewRadius = 12f;
    [Tooltip("ระยะจากใต้กล้องที่กล้องมองไม่เห็น (เมตร, วัดบนพื้นราบ) = ทางเดินลอดใต้กล้อง / 0 = กรวยแหลมแบบเดิม")]
    [Min(0f)] public float nearBlindDistance = 2.5f;
    [Range(1f, 180f)] public float viewAngle = 50f;
    [Tooltip("สิ่งที่บังสายตา (ค่าเริ่มต้น = Layer Obstacle)")]
    public LayerMask obstructionMask;
    public string playerTag = "Player";

    [Header("Alert")]
    [Tooltip("ต้องเห็นผู้เล่นต่อเนื่องกี่วินาทีจึงจะ Alert")]
    public float detectTime = 1.2f;
    [Tooltip("หลัง Alert แล้วต้องรอกี่วินาทีจึง Alert ซ้ำได้")]
    public float alertCooldown = 6f;
    public AudioClip alertSound;

    [Header("ศัตรูที่ถูกเรียก (ใช้ข้อแรกที่มี)")]
    [Tooltip("1) ลากศัตรูที่ต้องการให้กล้องนี้เรียกมาใส่ตรงนี้")]
    public AlertState[] linkedEnemies;
    [Tooltip("2) ห้องของกล้องนี้ (ว่าง = หา RoomZone ที่ผู้เล่นหรือกล้องอยู่ให้เอง)")]
    public RoomZone room;
    [Tooltip("3) ถ้าไม่มีทั้งสองข้อบน: เรียกศัตรูทุกตัวในรัศมีนี้รอบกล้อง")]
    public float alertRadius = 25f;

    [Header("กล้อง = ตาของศัตรู")]
    [Tooltip("หลัง Alert ถ้ากล้องยังเห็นผู้เล่น ส่งตำแหน่งให้ศัตรูไล่ล่าต่อเนื่อง")]
    public bool actAsEnemyEyes = true;
    [Tooltip("ส่งตำแหน่งทุกกี่วินาที")]
    [Min(0.1f)] public float reportInterval = 0.5f;
    [Tooltip("แต่ละครั้งที่ได้รายงาน ศัตรูจะไล่ล่าอย่างน้อยกี่วินาที (แม้ตัวเองยังไม่เห็นผู้เล่น)")]
    [Min(0f)] public float chaseTimePerReport = 4f;

    [Header("ยิงพังได้")]
    [Min(0.1f)] public float maxHealth = 10f;
    [Tooltip("พังแล้วหัวกล้องก้มลงกี่องศา")]
    public float brokenTiltAngle = 45f;
    [Tooltip("เสียงตอนพัง (ว่าง = ไฟล์ camera_break ในคลังเสียง ถ้ามี)")]
    public AudioClip breakSound;
    [Tooltip("เอฟเฟกต์ตอนพัง เช่น ประกายไฟ/ควัน (ไม่บังคับ) จะสร้างที่ตำแหน่งหัวกล้อง")]
    public GameObject breakEffect;

    [Header("Vision Cone Visual")]
    public bool showVisionCone = true;
    public Color searchingColor = new Color(0.2f, 0.6f, 1f, 0.22f);
    public Color detectingColor = new Color(1f, 0.85f, 0.1f, 0.3f);
    public Color alertColor = new Color(1f, 0f, 0f, 0.38f);
    [Range(3, 60)] public int coneSegments = 24;

    private State state = State.Sweep;
    private Quaternion baseLocalRotation;
    private float currentYaw;
    private int direction = 1;
    private float holdTimer;
    private float detectTimer;
    private float nextAlertTime;
    private float alertFlashUntil;
    private float nextVisionCheck;
    private float nextReportTime;
    private bool seesPlayer;
    private Transform player;
    private readonly List<AlertState> calledEnemies = new List<AlertState>();

    private Transform coneTransform;
    private Mesh coneMesh;
    private Material coneMaterial;
    private Vector3[] coneVertices;
    private float groundY;

    private float health;
    private bool broken;

    public bool SeesPlayer => seesPlayer;
    public bool IsBroken => broken;

    private void Reset()
    {
        obstructionMask = LayerMask.GetMask("Obstacle");
    }

    private void Awake()
    {
        if (head == null)
            head = transform;

        if (obstructionMask.value == 0)
            obstructionMask = LayerMask.GetMask("Obstacle");

        baseLocalRotation = head.localRotation;
        health = maxHealth;
        EnsureColliders();
    }

    // กระสุนต้องชนอะไรสักอย่าง: ชิ้นส่วนที่มี Mesh แต่ยังไม่มี Collider ได้ BoxCollider ขนาดพอดี Mesh
    private void EnsureColliders()
    {
        if (GetComponentInChildren<Collider>() != null)
            return;

        foreach (MeshFilter mesh in GetComponentsInChildren<MeshFilter>())
        {
            if (mesh.transform == coneTransform)
                continue;

            mesh.gameObject.AddComponent<BoxCollider>();
        }
    }

    /// <summary>โดนยิง (IShootable)</summary>
    public void TakeShot(float damage, Vector3 hitPoint)
    {
        if (broken)
            return;

        health -= damage;
        if (health <= 0f)
            Break();
    }

    /// <summary>กล้องพัง: หยุดทุกอย่างถาวร</summary>
    public void Break()
    {
        if (broken)
            return;

        broken = true;
        health = 0f;
        seesPlayer = false;
        detectTimer = 0f;
        calledEnemies.Clear();

        if (coneTransform != null)
            coneTransform.gameObject.SetActive(false);

        if (head != null)
            head.localRotation = baseLocalRotation * Quaternion.Euler(brokenTiltAngle, currentYaw, 0f);

        if (breakSound != null)
            AudioSource.PlayClipAtPoint(breakSound, head.position);
        else
            SfxLibrary.Play("camera_break", head.position);

        if (breakEffect != null)
            Instantiate(breakEffect, head.position, Quaternion.identity);
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject != null)
            player = playerObject.transform;

        // สุ่มจุดเริ่ม ไม่ให้กล้องหลายตัวหันพร้อมกันเป๊ะ
        currentYaw = Random.Range(-sweepAngle, sweepAngle);
        direction = Random.value < 0.5f ? -1 : 1;

        // ระดับพื้นใต้กล้อง: ใช้วาดกรวยสายตาบนพื้น
        groundY = Physics.Raycast(head.position, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point.y + 0.06f
            : head.position.y - 3f;

        CreateCone();
    }

    private void Update()
    {
        if (broken)
            return;

        if (Time.time >= nextVisionCheck)
        {
            nextVisionCheck = Time.time + 0.1f;
            seesPlayer = CheckVision();
        }

        if (seesPlayer)
            UpdateTrack();
        else
            UpdateSweep();

        head.localRotation = baseLocalRotation * Quaternion.Euler(0f, currentYaw, 0f);
    }

    private void LateUpdate()
    {
        if (!broken)
            UpdateCone();
    }

    private void UpdateSweep()
    {
        if (state == State.Track)
        {
            // คลาดสายตา: กลับไปกวาดต่อ ค่อยๆ ลืม
            state = State.Sweep;
        }

        detectTimer = Mathf.Max(0f, detectTimer - Time.deltaTime * 0.5f);

        if (state == State.Hold)
        {
            holdTimer -= Time.deltaTime;
            if (holdTimer <= 0f)
            {
                direction = -direction;
                state = State.Sweep;
            }

            return;
        }

        float target = sweepAngle * direction;
        currentYaw = Mathf.MoveTowards(currentYaw, target, turnSpeed * Time.deltaTime);

        if (Mathf.Approximately(currentYaw, target))
        {
            state = State.Hold;
            holdTimer = holdTime;
            SfxLibrary.Play("camera_servo", head.position, 0.5f);
        }
    }

    private void UpdateTrack()
    {
        if (state != State.Track)
        {
            state = State.Track;
            SfxLibrary.Play("camera_detect", head.position);
        }

        // หันตามผู้เล่น (ภายในช่วงที่กล้องหมุนได้)
        Quaternion baseWorld = (head.parent != null ? head.parent.rotation : Quaternion.identity) * baseLocalRotation;
        Vector3 toPlayer = Quaternion.Inverse(baseWorld) * (player.position - head.position);
        float targetYaw = Mathf.Clamp(Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg, -sweepAngle, sweepAngle);
        currentYaw = Mathf.MoveTowards(currentYaw, targetYaw, turnSpeed * 2f * Time.deltaTime);

        detectTimer += Time.deltaTime;

        if (detectTimer >= detectTime && Time.time >= nextAlertTime)
            Alert();
        else if (actAsEnemyEyes && calledEnemies.Count > 0 && Time.time >= nextReportTime)
            ReportToEnemies(); // Alert ไปแล้ว ยังเห็นอยู่ → เป็นตาให้ศัตรูต่อ
    }

    private bool CheckVision()
    {
        if (player == null || PlayerStealth.IsInvisible)
            return false;

        Vector3 origin = head.position;
        Vector3 target = player.position + Vector3.up; // กลางลำตัว
        Vector3 flat = target - origin;
        flat.y = 0f;

        float sqr = flat.sqrMagnitude;
        if (sqr > viewRadius * viewRadius || sqr < nearBlindDistance * nearBlindDistance)
            return false; // ไกลเกิน หรืออยู่ในจุดบอดใต้กล้อง

        Vector3 forward = head.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f || Vector3.Angle(forward, flat) > viewAngle * 0.5f)
            return false;

        return !Physics.Linecast(origin, target, obstructionMask, QueryTriggerInteraction.Ignore);
    }

    private void Alert()
    {
        nextAlertTime = Time.time + alertCooldown;
        alertFlashUntil = Time.time + 1.5f;
        detectTimer = 0f;

        if (alertSound != null)
            AudioSource.PlayClipAtPoint(alertSound, head.position);
        else
            SfxLibrary.Play("camera_alert", head.position);

        CollectEnemies(player.position);
        ReportToEnemies();
    }

    // รวบรวมศัตรูที่กล้องนี้จะเรียก (ตามลำดับ Linked → Room → Radius)
    private void CollectEnemies(Vector3 playerPosition)
    {
        calledEnemies.Clear();

        if (linkedEnemies != null)
        {
            for (int i = 0; i < linkedEnemies.Length; i++)
                if (linkedEnemies[i] != null)
                    calledEnemies.Add(linkedEnemies[i]);
        }

        if (calledEnemies.Count > 0)
            return;

        RoomZone zone = room != null ? room : RoomZone.GetAt(playerPosition) ?? RoomZone.GetAt(head.position);
        if (zone != null)
        {
            foreach (AlertState enemy in zone.Enemies)
                if (enemy != null)
                    calledEnemies.Add(enemy);

            if (calledEnemies.Count > 0)
                return;
        }

        float sqrRadius = alertRadius * alertRadius;
        for (int i = 0; i < AlertState.Active.Count; i++)
        {
            AlertState enemy = AlertState.Active[i];
            if (enemy != null && (enemy.transform.position - head.position).sqrMagnitude <= sqrRadius)
                calledEnemies.Add(enemy);
        }
    }

    private void ReportToEnemies()
    {
        nextReportTime = Time.time + reportInterval;
        Vector3 playerPosition = player.position;

        for (int i = calledEnemies.Count - 1; i >= 0; i--)
        {
            AlertState enemy = calledEnemies[i];
            if (enemy == null || !enemy.isActiveAndEnabled)
            {
                calledEnemies.RemoveAt(i);
                continue;
            }

            if (actAsEnemyEyes)
                enemy.ReceiveSpotterReport(playerPosition, chaseTimePerReport); // ไล่ล่าผู้เล่นตรงๆ
            else
                enemy.ReceiveReinforcement(playerPosition);                     // แบบเดิม: ไปตรวจจุดที่เห็น
        }
    }

    // ------------------------------------------------------------ กรวยสายตาบนพื้น (ตัดปลายใต้กล้อง)

    private void CreateCone()
    {
        GameObject go = new GameObject("CameraVisionCone");
        go.transform.SetParent(transform, false);
        coneTransform = go.transform;

        coneMesh = new Mesh { name = "Security Camera Cone" };
        go.AddComponent<MeshFilter>().mesh = coneMesh;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

        coneMaterial = new Material(shader) { renderQueue = 3000 };
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.material = coneMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // แถบโค้ง 2 แถว: แถวใน (ขอบจุดบอด) กับแถวนอก (ปลายสายตา) ต่อกันเป็นสี่เหลี่ยมทีละช่อง
        int segments = Mathf.Max(3, coneSegments);
        coneVertices = new Vector3[(segments + 1) * 2];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i < segments; i++)
        {
            int inner = i * 2;
            int outer = inner + 1;
            int nextInner = inner + 2;
            int nextOuter = inner + 3;
            int t = i * 6;

            triangles[t] = inner;
            triangles[t + 1] = outer;
            triangles[t + 2] = nextOuter;
            triangles[t + 3] = inner;
            triangles[t + 4] = nextOuter;
            triangles[t + 5] = nextInner;
        }

        coneMesh.vertices = coneVertices;
        coneMesh.triangles = triangles;
    }

    private void UpdateCone()
    {
        if (coneTransform == null)
            return;

        if (coneTransform.gameObject.activeSelf != showVisionCone)
            coneTransform.gameObject.SetActive(showVisionCone);

        if (!showVisionCone)
            return;

        Vector3 forward = head.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return;

        // วางกรวยบนพื้นใต้กล้อง หมุนตามทิศที่กล้องหัน (เฉพาะแกน Y)
        Vector3 origin = new Vector3(head.position.x, groundY, head.position.z);
        coneTransform.SetPositionAndRotation(origin, Quaternion.LookRotation(forward.normalized, Vector3.up));

        int segments = coneVertices.Length / 2 - 1;
        float half = viewAngle * 0.5f;
        float near = Mathf.Min(nearBlindDistance, viewRadius);
        Vector3 rayOrigin = origin + Vector3.up * 0.5f;

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-half, half, i / (float)segments) * Mathf.Deg2Rad;
            Vector3 localDirection = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 worldDirection = coneTransform.TransformDirection(localDirection);

            float distance = viewRadius;
            if (Physics.Raycast(rayOrigin, worldDirection, out RaycastHit hit, viewRadius, obstructionMask, QueryTriggerInteraction.Ignore))
                distance = hit.distance;

            // ถ้ากำแพงอยู่ใกล้กว่าขอบจุดบอด แถวในกับแถวนอกจะทับกัน = ช่องนั้นหายไป
            coneVertices[i * 2] = localDirection * Mathf.Min(near, distance);
            coneVertices[i * 2 + 1] = localDirection * distance;
        }

        coneMesh.vertices = coneVertices;
        coneMesh.RecalculateBounds();

        Color color = Time.time < alertFlashUntil ? alertColor
            : seesPlayer ? Color.Lerp(detectingColor, alertColor, Mathf.Clamp01(detectTimer / Mathf.Max(0.01f, detectTime)))
            : searchingColor;

        if (coneMaterial.HasProperty("_Color")) coneMaterial.SetColor("_Color", color);
        if (coneMaterial.HasProperty("_UnlitColor")) coneMaterial.SetColor("_UnlitColor", color);
        if (coneMaterial.HasProperty("_BaseColor")) coneMaterial.SetColor("_BaseColor", color);
    }

    private void OnDestroy()
    {
        if (coneMesh != null) Destroy(coneMesh);
        if (coneMaterial != null) Destroy(coneMaterial);
    }

    private void OnDrawGizmosSelected()
    {
        Transform pivot = head != null ? head : transform;
        Quaternion baseRotation = Application.isPlaying && pivot.parent != null
            ? pivot.parent.rotation * baseLocalRotation
            : Application.isPlaying ? baseLocalRotation : pivot.rotation;

        Vector3 forward = baseRotation * Vector3.forward;
        forward.y = 0f;
        forward.Normalize();

        // ขอบการกวาด: เส้นเริ่มที่ขอบจุดบอด
        Gizmos.color = Color.cyan;
        Vector3 left = Quaternion.Euler(0f, -sweepAngle, 0f) * forward;
        Vector3 right = Quaternion.Euler(0f, sweepAngle, 0f) * forward;
        Gizmos.DrawLine(pivot.position + left * nearBlindDistance, pivot.position + left * viewRadius);
        Gizmos.DrawLine(pivot.position + right * nearBlindDistance, pivot.position + right * viewRadius);

        // วงจุดบอดใต้กล้อง
        if (nearBlindDistance > 0f)
        {
            Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            Vector3 previous = pivot.position + forward * nearBlindDistance;
            for (int i = 1; i <= 24; i++)
            {
                Vector3 next = pivot.position + Quaternion.Euler(0f, i * 15f, 0f) * forward * nearBlindDistance;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        // ศัตรูที่ลากใส่ไว้: เส้นสีแดงไปหา
        bool hasLinked = false;
        if (linkedEnemies != null)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            foreach (AlertState enemy in linkedEnemies)
            {
                if (enemy == null) continue;
                hasLinked = true;
                Gizmos.DrawLine(pivot.position, enemy.transform.position + Vector3.up);
            }
        }

        if (!hasLinked && room == null)
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.6f);
            Gizmos.DrawWireSphere(pivot.position, alertRadius);
        }
    }
}
