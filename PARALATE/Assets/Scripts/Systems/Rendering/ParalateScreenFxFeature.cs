using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Renderer Feature ของฟิลเตอร์ภาพรวมทั้งเกม: อยู่ใน "New Universal Render Pipeline Asset_Renderer" จึงทำงานทุก Scene ทุกกล้องเอง
/// อ่านค่าจาก Volume (ParalateScreenFx) ของกล้องที่กำลังเรนเดอร์ + ScreenFx (Boost / ค่าผู้เล่น) แล้ว Blit ทั้งจอด้วย Shader เดียว
/// ปิดทั้งหมด: ติ๊กออกที่ Renderer Asset หรือตั้ง Master Intensity = 0 ใน Volume
/// </summary>
public class ParalateScreenFxFeature : ScriptableRendererFeature
{
    [Tooltip("Shader Hidden/Paralate/ScreenFx (ต้องอ้างอิงไว้ ไม่งั้นตอน Build Shader จะไม่ถูกรวมเข้าเกม)")]
    [SerializeField] private Shader shader;

    [Tooltip("ก่อน Post Processing = Grain/Distort โดน Color Grading/Bloom ของ URP ต่อ, หลัง = ทับบนภาพสุดท้าย")]
    [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

    [Tooltip("ใส่ฟิลเตอร์ในหน้าต่าง Scene ด้วย")]
    [SerializeField] private bool showInSceneView = false;

    private static readonly int PsxGridHeightId = Shader.PropertyToID("_PSX_GridHeight");

    private Material material;
    private ScreenFxPass pass;

    public override void Create()
    {
        if (shader == null)
            shader = Shader.Find("Hidden/Paralate/ScreenFx");

        pass = new ScreenFxPass { renderPassEvent = injectionPoint };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (shader == null)
            return;

        CameraType type = renderingData.cameraData.cameraType;
        if (type == CameraType.Preview || type == CameraType.Reflection)
            return;
        if (type == CameraType.SceneView && !showInSceneView)
            return;

        // กล้องที่เรนเดอร์ลง RenderTexture (เช่น Minimap) ไม่ต้องใส่ฟิลเตอร์
        Camera camera = renderingData.cameraData.camera;
        if (type == CameraType.Game && camera.targetTexture != null)
            return;

        ParalateScreenFx fx = VolumeManager.instance.stack.GetComponent<ParalateScreenFx>();
        if (fx == null)
            return;

        // กริด Vertex Snap ของ Shader "Paralate/PSX Lit" = Pixel Height ของฟิลเตอร์ (จุดสั่นตรงพิกเซลภาพ)
        Shader.SetGlobalFloat(PsxGridHeightId, fx.pixelHeight.value);

        if (material == null)
            material = CoreUtils.CreateEngineMaterial(shader);

        if (!pass.Setup(material, fx))
            return; // ทุกค่าเป็น 0 → ข้าม ไม่เสีย Blit

        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
    }

    // ---------------------------------------------------------------------------------------------

    private class ScreenFxPass : ScriptableRenderPass
    {
        private static readonly int GrainParamsId = Shader.PropertyToID("_GrainParams");
        private static readonly int GrainColoredId = Shader.PropertyToID("_GrainColored");
        private static readonly int WaveParamsId = Shader.PropertyToID("_WaveParams");
        private static readonly int JitterParamsId = Shader.PropertyToID("_JitterParams");
        private static readonly int ColorParamsId = Shader.PropertyToID("_ColorParams");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int Ps1ParamsId = Shader.PropertyToID("_Ps1Params");
        private static readonly int FrameParamsId = Shader.PropertyToID("_FrameParams");

        private Material material;

        public ScreenFxPass()
        {
            // ต้องมีภาพกลาง (ไม่วาดลง Back Buffer ตรงๆ) เพื่อให้อ่านภาพกล้องได้
            requiresIntermediateTexture = true;
        }

        /// <summary>คำนวณค่าจริง (Volume × Master × ค่าผู้เล่น + Boost) แล้วส่งให้ Material; false = ไม่มีอะไรให้วาด</summary>
        public bool Setup(Material mat, ParalateScreenFx fx)
        {
            material = mat;

            float master = fx.masterIntensity.value;
            if (master <= 0f)
                return false;

            float boost = ScreenFx.CurrentBoost;
            float distort = ScreenFx.DistortionEnabled ? master : 0f;

            float grain = (fx.grainIntensity.value + fx.boostGrain.value * boost) * master * ScreenFx.GrainScale;
            float wave = (fx.waveAmplitude.value + fx.boostWave.value * boost) * distort;
            float warp = fx.edgeWarp.value * distort;
            float jitter = Mathf.Clamp01(fx.jitterChance.value + fx.boostJitter.value * boost) * distort;
            float aberration = (fx.chromaticAberration.value + fx.boostAberration.value * boost) * master;

            // สี: เลื่อนจากค่ากลาง (1) ตาม Master
            float saturation = Mathf.Lerp(1f, fx.saturation.value, master);
            float contrast = Mathf.Lerp(1f, fx.contrast.value, master);
            Color tint = Color.Lerp(Color.white, fx.tint.value, master);

            bool ps1 = fx.ps1Enabled.value;
            float scanline = fx.scanline.value * master;
            float vignette = fx.vignette.value * master;

            bool anything = grain > 0f || wave > 0f || warp != 0f || jitter > 0f || aberration > 0f
                || ps1 || scanline > 0f || vignette > 0f
                || !Mathf.Approximately(saturation, 1f) || !Mathf.Approximately(contrast, 1f) || tint != Color.white;
            if (!anything)
                return false;

            material.SetVector(GrainParamsId, new Vector4(grain, fx.grainSize.value, fx.grainFps.value, fx.grainShadowResponse.value));
            material.SetFloat(GrainColoredId, fx.grainColored.value ? 1f : 0f);
            material.SetVector(WaveParamsId, new Vector4(wave, fx.waveFrequency.value, fx.waveSpeed.value, warp));
            material.SetVector(JitterParamsId, new Vector4(jitter, fx.jitterStrength.value, fx.jitterFps.value, fx.jitterBands.value));
            material.SetVector(ColorParamsId, new Vector4(aberration, saturation, contrast, 0f));
            material.SetColor(TintId, tint);
            material.SetVector(Ps1ParamsId, new Vector4(ps1 ? 1f : 0f, fx.pixelHeight.value, fx.colorLevels.value, fx.dither.value * master));
            material.SetVector(FrameParamsId, new Vector4(scanline, vignette, 0f, 0f));
            return true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
                return;

            // Blit ภาพกล้อง → Texture ใหม่ผ่าน Shader แล้วให้ Texture ใหม่เป็นภาพกล้องต่อไป (ไม่ต้อง Blit กลับ)
            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc desc = renderGraph.GetTextureDesc(source);
            desc.name = "_ParalateScreenFx";
            desc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(desc);

            RenderGraphUtils.BlitMaterialParameters blit = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
            renderGraph.AddBlitPass(blit, "Paralate Screen FX");
            resourceData.cameraColor = destination;
        }
    }
}
