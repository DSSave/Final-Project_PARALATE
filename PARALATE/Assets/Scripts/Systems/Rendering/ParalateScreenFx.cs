using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ค่าของฟิลเตอร์ภาพรวมทั้งเกม (อยู่ใน Volume Profile)
///   ทั้งเกม: Assets/DefaultVolumeProfile.asset (Project Settings > Graphics > URP > Default Volume Profile)
///   เฉพาะห้อง: วาง Volume แบบ Local + Box Collider แล้ว Add Override > Paralate > Screen FX ติ๊กเฉพาะค่าที่อยากต่าง
/// ตัววาดภาพจริงคือ ParalateScreenFxFeature (Renderer Feature) ส่วนโค้ดเกมเพลย์คุมผ่าน ScreenFx (Pulse / SetBoost)
/// </summary>
[Serializable]
[VolumeComponentMenu("Paralate/Screen FX")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class ParalateScreenFx : VolumeComponent
{
    [Header("รวม")]
    [Tooltip("คูณทุกเอฟเฟกต์ (0 = ปิดฟิลเตอร์ทั้งหมด)")]
    public ClampedFloatParameter masterIntensity = new ClampedFloatParameter(1f, 0f, 1f);

    [Header("Grain")]
    [Tooltip("ความแรงของเม็ด (0.05–0.15 = เบาๆ)")]
    public ClampedFloatParameter grainIntensity = new ClampedFloatParameter(0f, 0f, 1f);
    [Tooltip("ขนาดเม็ด (พิกเซลจอ)")]
    public ClampedFloatParameter grainSize = new ClampedFloatParameter(1.5f, 1f, 6f);
    [Tooltip("เปลี่ยนลาย Grain กี่ครั้งต่อวินาที (24 = ฟิล์ม)")]
    public ClampedFloatParameter grainFps = new ClampedFloatParameter(24f, 1f, 60f);
    [Tooltip("0 = เท่ากันทั้งภาพ, 1 = เม็ดชัดในที่มืด จางในที่สว่าง")]
    public ClampedFloatParameter grainShadowResponse = new ClampedFloatParameter(0.6f, 0f, 1f);
    [Tooltip("เม็ดสี (ปิด = เม็ดขาวดำ)")]
    public BoolParameter grainColored = new BoolParameter(false);

    [Header("Distort: คลื่น / ขอบจอ")]
    [Tooltip("ความแรงของคลื่นแนวนอน (0.001–0.003 = นิดๆ)")]
    public ClampedFloatParameter waveAmplitude = new ClampedFloatParameter(0f, 0f, 0.02f);
    [Tooltip("จำนวนคลื่นต่อความสูงจอ")]
    public ClampedFloatParameter waveFrequency = new ClampedFloatParameter(3f, 0.5f, 40f);
    [Tooltip("ความเร็วคลื่น (รอบต่อวินาที)")]
    public ClampedFloatParameter waveSpeed = new ClampedFloatParameter(0.3f, 0f, 5f);
    [Tooltip("โค้งขอบจอ (+ = นูนแบบจอ CRT, - = เว้า)")]
    public ClampedFloatParameter edgeWarp = new ClampedFloatParameter(0f, -0.3f, 0.3f);

    [Header("Distort: เส้นกระตุก")]
    [Tooltip("โอกาสที่แถบภาพจะเลื่อนข้าง (0 = ปิด)")]
    public ClampedFloatParameter jitterChance = new ClampedFloatParameter(0f, 0f, 1f);
    [Tooltip("ระยะเลื่อนสูงสุด (สัดส่วนความกว้างจอ)")]
    public ClampedFloatParameter jitterStrength = new ClampedFloatParameter(0.006f, 0f, 0.05f);
    [Tooltip("สุ่มแถบใหม่กี่ครั้งต่อวินาที")]
    public ClampedFloatParameter jitterFps = new ClampedFloatParameter(10f, 1f, 60f);
    [Tooltip("จำนวนแถบต่อความสูงจอ")]
    public ClampedFloatParameter jitterBands = new ClampedFloatParameter(48f, 4f, 240f);

    [Header("สี")]
    public ClampedFloatParameter chromaticAberration = new ClampedFloatParameter(0f, 0f, 0.02f);
    public ClampedFloatParameter saturation = new ClampedFloatParameter(1f, 0f, 1.5f);
    public ClampedFloatParameter contrast = new ClampedFloatParameter(1f, 0.5f, 1.5f);
    public ColorParameter tint = new ColorParameter(Color.white, false, false, true);

    [Header("PS1 (เดิม PS1Signalis)")]
    [Tooltip("ลดความละเอียด + Dither + ลดระดับสี")]
    public BoolParameter ps1Enabled = new BoolParameter(false);
    public ClampedIntParameter pixelHeight = new ClampedIntParameter(240, 90, 1080);
    public ClampedIntParameter colorLevels = new ClampedIntParameter(32, 4, 64);
    public ClampedFloatParameter dither = new ClampedFloatParameter(0.5f, 0f, 1f);

    [Header("กรอบภาพ")]
    public ClampedFloatParameter scanline = new ClampedFloatParameter(0f, 0f, 0.6f);
    public ClampedFloatParameter vignette = new ClampedFloatParameter(0f, 0f, 1f);

    [Header("Boost (ตอนโค้ดเรียก ScreenFx.Pulse / SetBoost)")]
    [Tooltip("Grain ที่บวกเพิ่มเมื่อ Boost = 1")]
    public ClampedFloatParameter boostGrain = new ClampedFloatParameter(0.15f, 0f, 1f);
    [Tooltip("คลื่นที่บวกเพิ่มเมื่อ Boost = 1")]
    public ClampedFloatParameter boostWave = new ClampedFloatParameter(0.006f, 0f, 0.02f);
    [Tooltip("โอกาสเส้นกระตุกที่บวกเพิ่มเมื่อ Boost = 1")]
    public ClampedFloatParameter boostJitter = new ClampedFloatParameter(0.6f, 0f, 1f);
    [Tooltip("Chromatic Aberration ที่บวกเพิ่มเมื่อ Boost = 1")]
    public ClampedFloatParameter boostAberration = new ClampedFloatParameter(0.006f, 0f, 0.02f);
}
