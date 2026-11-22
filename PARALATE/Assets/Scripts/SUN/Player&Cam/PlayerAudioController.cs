using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class PlayerAudioController : MonoBehaviour
{
    [Serializable]
    public class SurfaceSound
    {
        public string surfaceTag = "Untagged"; // Tag ของพื้นผิว เช่น "Grass", "Wood", "Stone", "Water"
        public AudioClip[] footstepClips;
    }

    [Header("Footstep Sync (ตามก้าวของอนิเมชั่น)")]
    [Tooltip("เปิด = เสียงเดินดังตอนเท้าในอนิเมชั่นแตะพื้นจริง (ใช้กระดูกเท้าของ Humanoid)\n" +
             "ปิด / ตัวละครไม่ใช่ Humanoid = ใช้จังหวะเวลาแบบเดิม (Footstep Timing)")]
    public bool syncWithAnimation = true;
    [Tooltip("เท้าต่ำลงถึงสัดส่วนนี้ของช่วงยกเท้า = นับว่าแตะพื้น (0 = จุดต่ำสุด, 1 = ยกสูงสุด)")]
    [Range(0.05f, 0.5f)] public float plantThreshold = 0.2f;
    [Tooltip("เท้าต้องยกขึ้นเกินสัดส่วนนี้ก่อน ถึงจะนับก้าวถัดไปของเท้านั้น (กันเสียงซ้ำ)")]
    [Range(0.3f, 0.9f)] public float liftThreshold = 0.5f;
    [Tooltip("เวลาขั้นต่ำระหว่างเสียงเดินสองครั้ง (กันซ้อนตอนเปลี่ยนท่า)")]
    public float minStepGap = 0.12f;

    [Header("Footstep Timing (ใช้เมื่อปิด Sync)")]
    public float baseFootstepInterval = 0.4f;
    [Tooltip("ความเร็วอ้างอิงที่ interval เท่ากับ baseFootstepInterval พอดี")]
    public float referenceSpeed = 5f; // ควรเท่ากับ walkSpeed ใน TopDownPlayerController
    public float minFootstepInterval = 0.2f; // กันไม่ให้ถี่เกินไปตอนวิ่งเร็วมาก

    [Header("Surface-Based Footsteps")]
    [Tooltip("ชุดเสียงเดินตามพื้นผิว ต่อ Tag ของ Collider พื้น")]
    public SurfaceSound[] surfaceSounds;
    [Tooltip("ชุดเสียงสำรอง ถ้า raycast ไม่เจอพื้น หรือไม่เจอ Tag ที่ตรงกัน")]
    public AudioClip[] defaultFootstepClips;

    [Header("Ground Detection")]
    public float groundCheckDistance = 1.2f;
    public LayerMask groundLayer = ~0; // default: ชนได้ทุก layer, ปรับใน Inspector ให้เหลือเฉพาะพื้น

    [Header("Audio Clips")]
    public AudioClip whistleClip;

    [Header("Footstep Variation")]
    [Range(0f, 0.2f)] public float pitchVariance = 0.08f;
    [Range(0f, 0.3f)] public float volumeVariance = 0.15f;
    [Range(0f, 1f)] public float footstepBaseVolume = 1f;

    // รัศมีเสียงสำหรับ AI ศัตรูตั้งที่ PlayerNoiseEmitter ที่เดียว (แหล่งข้อมูลเดียวกับเสียงปืน)
    private PlayerNoiseEmitter noiseEmitter;
    private AudioSource audioSource;
    private float nextFootstepTime = 0f;
    private int lastFootstepIndex = -1;
    private Dictionary<string, AudioClip[]> surfaceLookup;

    // ตรวจก้าวจากกระดูกเท้า: จำช่วงความสูงต่ำสุด/สูงสุดของแต่ละเท้า (ปรับตามท่าเดิน/วิ่ง/ขนาดตัวละครเอง)
    private Animator footAnimator;
    private Transform[] feet;
    private readonly float[] footMin = new float[2];
    private readonly float[] footMax = new float[2];
    private readonly bool[] footLifted = new bool[2];
    private float lastStepTime;
    private float lastFootUpdate = -1f;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        AudioManager.RouteToSfx(audioSource);

        noiseEmitter = GetComponent<PlayerNoiseEmitter>();
        if (noiseEmitter == null)
            noiseEmitter = gameObject.AddComponent<PlayerNoiseEmitter>();

        BuildSurfaceLookup();
        AddLibraryFootsteps();
    }

    // เพิ่มความหลากหลายของเสียงเดิน: รวมไฟล์ footstep_1, footstep_2, ... จาก Resources/Audio/SFX เข้ากับชุดเสียงสำรอง
    // (วางไฟล์เพิ่มได้เรื่อยๆ ไม่ต้องลากใส่ Inspector)
    void AddLibraryFootsteps()
    {
        AudioClip[] extra = SfxLibrary.GetClips("footstep");
        if (extra.Length == 0)
            return;

        List<AudioClip> merged = new List<AudioClip>();
        if (defaultFootstepClips != null)
            merged.AddRange(defaultFootstepClips);

        for (int i = 0; i < extra.Length; i++)
        {
            if (!merged.Contains(extra[i]))
                merged.Add(extra[i]);
        }

        defaultFootstepClips = merged.ToArray();
    }

    void BuildSurfaceLookup()
    {
        surfaceLookup = new Dictionary<string, AudioClip[]>();
        if (surfaceSounds == null) return;

        foreach (var entry in surfaceSounds)
        {
            if (entry == null || string.IsNullOrEmpty(entry.surfaceTag))
                continue;

            // ถ้ามี tag ซ้ำ ใช้ตัวแรกที่เจอ ไม่ทับด้วยตัวหลัง
            if (!surfaceLookup.ContainsKey(entry.surfaceTag))
                surfaceLookup.Add(entry.surfaceTag, entry.footstepClips);
        }
    }

    /// <summary>
    /// เรียกทุกเฟรมที่ตัวละครกำลังเดิน/วิ่งบนพื้น
    /// จัดการ cooldown ระหว่างฝีเท้าให้เอง
    /// </summary>
    public void HandleFootstep(bool isGrounded, bool isSprinting, bool isCrouching, float currentSpeed)
    {
        if (syncWithAnimation && ResolveFeet())
        {
            if (DetectFootPlant() && isGrounded && !isCrouching)
            {
                noiseEmitter.EmitFootstep(isSprinting);
                PlayFootstepSound(GetClipsForCurrentSurface());
            }

            return;
        }

        if (!isGrounded || Time.time < nextFootstepTime) return;

        if (!isCrouching)
        {
            noiseEmitter.EmitFootstep(isSprinting);

            AudioClip[] clips = GetClipsForCurrentSurface();
            PlayFootstepSound(clips);
        }

        float speedRatio = Mathf.Max(currentSpeed, 0.1f) / referenceSpeed;
        float interval = baseFootstepInterval / speedRatio;
        nextFootstepTime = Time.time + Mathf.Max(interval, minFootstepInterval);
    }

    bool ResolveFeet()
    {
        if (feet != null)
            return true;

        if (footAnimator == null)
        {
            TopDownPlayerController controller = GetComponent<TopDownPlayerController>();
            footAnimator = controller != null && controller.animator != null ? controller.animator : GetComponentInChildren<Animator>();
        }

        if (footAnimator == null || !footAnimator.isHuman)
            return false;

        Transform left = footAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform right = footAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (left == null || right == null)
            return false;

        feet = new[] { left, right };
        return true;
    }

    // คืน true ในเฟรมที่เท้าข้างใดข้างหนึ่งเพิ่งแตะพื้น
    // หลักการ: ความสูงเท้า (เทียบกับตัวผู้เล่น) ลดลงต่ำใกล้จุดต่ำสุดหลังจากเคยยกขึ้น = 1 ก้าว
    // ช่วงต่ำสุด/สูงสุดค่อยๆ ปรับตามท่าที่เล่นอยู่ จึงใช้ได้ทั้งเดิน วิ่งช้า วิ่งเร็ว โดยไม่ต้องใส่ Animation Event
    bool DetectFootPlant()
    {
        bool resumed = Time.time - lastFootUpdate > 0.25f; // เพิ่งเริ่มเดินใหม่: เริ่มจำช่วงใหม่
        lastFootUpdate = Time.time;
        bool stepped = false;
        float adapt = 1f - Mathf.Exp(-Time.deltaTime * 2f);

        for (int i = 0; i < 2; i++)
        {
            float height = feet[i].position.y - transform.position.y;

            if (resumed)
            {
                footMin[i] = height;
                footMax[i] = height + 0.01f;
                footLifted[i] = false;
            }

            // ขยายช่วงทันทีเมื่อเกิน แล้วค่อยๆ หดเข้าหาค่าปัจจุบัน (ท่าเปลี่ยน = ช่วงเปลี่ยนตาม)
            footMin[i] = height < footMin[i] ? height : Mathf.Lerp(footMin[i], height, adapt * 0.25f);
            footMax[i] = height > footMax[i] ? height : Mathf.Lerp(footMax[i], height, adapt * 0.25f);

            float range = footMax[i] - footMin[i];
            if (range < 0.02f)
                continue; // เท้าแทบไม่ขยับ (ยืนนิ่ง / ไถล)

            float normalized = (height - footMin[i]) / range;

            if (normalized > liftThreshold)
            {
                footLifted[i] = true;
            }
            else if (footLifted[i] && normalized < plantThreshold)
            {
                footLifted[i] = false;
                if (Time.time - lastStepTime >= minStepGap)
                {
                    lastStepTime = Time.time;
                    stepped = true;
                }
            }
        }

        return stepped;
    }

    AudioClip[] GetClipsForCurrentSurface()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            if (surfaceLookup.TryGetValue(hit.collider.tag, out AudioClip[] clips) && clips != null && clips.Length > 0)
                return clips;
        }

        return defaultFootstepClips;
    }

    void PlayFootstepSound(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0)
            return;

        int index;
        if (clips.Length == 1)
        {
            index = 0;
        }
        else
        {
            do
            {
                index = UnityEngine.Random.Range(0, clips.Length);
            } while (index == lastFootstepIndex);
        }
        lastFootstepIndex = index;

        AudioClip clip = clips[index];
        if (clip == null) return;

        audioSource.pitch = 1f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
        float volume = footstepBaseVolume + UnityEngine.Random.Range(-volumeVariance, volumeVariance);
        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume) * SfxLibrary.GainOf(clip));
    }

    public void PlayWhistle()
    {
        if (audioSource != null && whistleClip != null)
        {
            audioSource.PlayOneShot(whistleClip);
        }

        noiseEmitter.EmitWhistling();
    }
}
