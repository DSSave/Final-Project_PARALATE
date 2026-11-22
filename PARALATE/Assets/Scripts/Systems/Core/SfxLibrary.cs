using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คลังเสียงเอฟเฟกต์แบบ "วางไฟล์แล้วใช้ได้เลย": โหลดจาก Resources/Audio/SFX/ ตามชื่อ
/// - Play("weapon_switch") จะหาไฟล์ weapon_switch, weapon_switch_1, weapon_switch_2 ... แล้วสุ่มเล่น (ไม่ซ้ำอันเดิมติดกัน)
/// - ไม่มีไฟล์ = เงียบ ไม่ error จึงเรียกใช้ได้ก่อนที่จะมีไฟล์เสียงจริง
/// - เล่นผ่านกลุ่ม SFX ของ AudioManager (Slider SFX คุมได้) ถ้าไม่มี AudioManager ใช้ PlayClipAtPoint
/// - ความดัง: ทุกคลิปในคลังถูกคูณด้วย MasterVolume และค่าปรับเฉพาะเสียง (Trims) → GainOf(clip)
///   ไฟล์ CC0 ส่วนใหญ่ถูกอัดมาดังเกือบเต็มสเกล (peak ~0 dB) จึงต้องลดลงให้เข้ากับเสียงอื่นของเกม
/// รายชื่อเสียงและที่มา (CC0) ดูที่ Assets/Resources/Audio/CREDITS.md
/// </summary>
public static class SfxLibrary
{
    private const string Folder = "Audio/SFX/";

    /// <summary>ตัวคูณความดังรวมของเสียงทั้งคลัง (0.5 = เบาลงประมาณ 6 dB) อยากให้ดัง/เบาทั้งหมดแก้ที่นี่ที่เดียว</summary>
    public const float MasterVolume = 0.5f;

    // ค่าปรับเฉพาะเสียง (คูณกับ MasterVolume): วัดความดังเฉลี่ยของแต่ละไฟล์แล้วปรับให้ใกล้กัน
    //   < 1 = ไฟล์ที่อัดมาดังกว่าเพื่อน, > 1 = ไฟล์ที่อัดมาเบามาก
    // key เป็นได้ทั้ง "ชื่อกลุ่ม" (footstep) และ "ชื่อไฟล์" (footstep_6) โดยชื่อไฟล์มาก่อน
    private static readonly Dictionary<string, float> Trims = new Dictionary<string, float>
    {
        { "eyewide_on", 0.5f },
        // pickup = เสียงเก็บของเสียงเดียวของทั้งเกม (ไฟล์เดียวกับเสียงหยิบปืน อัดมาเบาอยู่แล้ว จึงชดเชยให้ดังเท่าต้นฉบับ)
        { "pickup", 2f },
        { "knockout", 0.65f }, { "player_hurt", 0.65f }, { "headshot", 0.6f }, { "stealth_kill", 0.7f },
        { "knife_hit", 0.8f }, { "menu_close", 0.6f }, { "weapon_equip", 0.7f },
        { "ui_hover", 0.5f }, { "wheel_tick", 0.6f }, { "craft_success", 0.8f },
        { "zoom_in", 0.7f }, { "zoom_out", 0.7f },
        // เสียงเดิน: ผู้เล่น = รองเท้าบนพื้นคอนกรีต (หนักแน่น แบบชุดแรก) / ศัตรู = ไม้ (หนัก ได้ยินชัดว่าอยู่ทางไหน)
        { "footstep", 1f }, { "enemy_footstep", 0.9f },

        { "knife_swing", 1.6f }, { "scope_in", 1.6f }, { "weapon_switch", 1.6f }, { "menu_open", 1.6f },
        { "note_close", 1.5f }, { "empty_click", 1.4f }, { "scope_out", 1.3f }, { "weapon_unequip", 1.2f },
    };

    private static readonly Dictionary<AudioClip, float> clipGain = new Dictionary<AudioClip, float>();
    private static readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>();
    private static readonly Dictionary<string, int> lastIndex = new Dictionary<string, int>();
    private static AudioSource fallbackSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cache.Clear();
        clipGain.Clear();
        lastIndex.Clear();
        fallbackSource = null;
    }

    /// <summary>คลิปทั้งหมดของชื่อนี้ (อาจเป็น array ว่าง)</summary>
    public static AudioClip[] GetClips(string name)
    {
        if (cache.TryGetValue(name, out AudioClip[] clips))
            return clips;

        List<AudioClip> found = new List<AudioClip>();

        AudioClip single = Resources.Load<AudioClip>(Folder + name);
        if (single != null)
            found.Add(single);

        // ไล่ครบ _1.._12 เสมอ: ลบไฟล์กลางชุดได้โดยไม่ต้องเปลี่ยนชื่อไฟล์ที่เหลือ (ผลถูกเก็บใน cache ค้นครั้งเดียว)
        for (int i = 1; i <= 12; i++)
        {
            AudioClip variant = Resources.Load<AudioClip>(Folder + name + "_" + i);
            if (variant != null)
                found.Add(variant);
        }

        clips = found.ToArray();
        cache[name] = clips;

        Trims.TryGetValue(name, out float groupTrim);
        for (int i = 0; i < clips.Length; i++)
        {
            float trim = Trims.TryGetValue(clips[i].name, out float fileTrim) ? fileTrim : groupTrim > 0f ? groupTrim : 1f;
            clipGain[clips[i]] = Mathf.Clamp01(MasterVolume * trim);
        }

        return clips;
    }

    /// <summary>
    /// ตัวคูณความดังของคลิปจากคลังนี้ (คลิปอื่นที่ลากใส่ Inspector เอง = 1 ไม่ถูกแตะ)
    /// สคริปต์ที่หยิบคลิปไปเล่นเองด้วย AudioSource ของตัวเอง ต้องคูณค่านี้เข้ากับ volume
    /// </summary>
    public static float GainOf(AudioClip clip)
    {
        return clip != null && clipGain.TryGetValue(clip, out float gain) ? gain : 1f;
    }

    public static AudioClip Pick(string name)
    {
        AudioClip[] clips = GetClips(name);
        if (clips.Length == 0)
            return null;

        if (clips.Length == 1)
            return clips[0];

        lastIndex.TryGetValue(name, out int last);
        int index = Random.Range(0, clips.Length - 1);
        if (index >= last) index++; // ข้ามอันที่เพิ่งเล่น
        lastIndex[name] = index;
        return clips[index];
    }

    public static bool Has(string name) => GetClips(name).Length > 0;

    /// <summary>เล่นเสียง 2D (UI / ผู้เล่น) คืน true ถ้ามีคลิป</summary>
    public static bool Play(string name, float volume = 1f)
    {
        AudioClip clip = Pick(name);
        if (clip == null)
            return false;

        GetSource().PlayOneShot(clip, volume * GainOf(clip));
        return true;
    }

    /// <summary>เล่นเสียง ณ ตำแหน่งในโลก (3D)</summary>
    public static bool Play(string name, Vector3 position, float volume = 1f)
    {
        AudioClip clip = Pick(name);
        if (clip == null)
            return false;

        AudioSource.PlayClipAtPoint(clip, position, volume * GainOf(clip));
        return true;
    }

    /// <summary>
    /// เสียงเก็บของ "เสียงเดียวกันทั้งเกม" (ปืน, มีด, กระสุน, ชิป, Map Data, Keycard, Scrap, Metal)
    /// ไฟล์: Resources/Audio/SFX/pickup — อยากเปลี่ยนเสียงเก็บของทั้งเกม เปลี่ยนไฟล์นี้ไฟล์เดียว
    /// คืน false ถ้าไม่มีไฟล์ (ผู้เรียกจะใช้เสียงเดิมของตัวเองแทน)
    /// </summary>
    public static bool PlayPickup() => Play("pickup");

    /// <summary>เล่นเสียง UI: ใช้ไฟล์ตามชื่อถ้ามี ไม่งั้นใช้เสียง Click/Hover เดิมของ AudioManager</summary>
    public static void PlayUI(string name, bool hoverFallback = false)
    {
        if (Play(name))
            return;

        if (AudioManager.Instance == null)
            return;

        if (hoverFallback) AudioManager.Instance.PlayUIHover();
        else AudioManager.Instance.PlayUIClick();
    }

    private static AudioSource GetSource()
    {
        if (fallbackSource != null)
            return fallbackSource;

        GameObject go = new GameObject("SfxLibrary (Auto)");
        Object.DontDestroyOnLoad(go);
        fallbackSource = go.AddComponent<AudioSource>();
        fallbackSource.playOnAwake = false;
        fallbackSource.spatialBlend = 0f;
        fallbackSource.ignoreListenerPause = true; // เสียง UI ยังดังตอน Pause
        AudioManager.RouteToSfx(fallbackSource);
        return fallbackSource;
    }
}
