// ฟิลเตอร์ภาพรวมทั้งเกม (Paralate Screen FX)
// ไม่ต้องสร้าง Material เอง: ParalateScreenFxFeature (Renderer Feature) สร้างให้และส่งค่าจาก Volume มาทุกเฟรม
// ปรับค่าที่ Assets/DefaultVolumeProfile.asset > Paralate Screen FX (หรือ Local Volume ในฉาก)
// ลำดับ: Distort (โค้งขอบ → คลื่น → เส้นกระตุก) → Pixelate (PS1) → Chromatic Aberration → สี → Grain → Dither/ระดับสี (PS1) → Scanline → Vignette
Shader "Hidden/Paralate/ScreenFx"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off
        ZTest Always
        Blend Off
        Cull Off

        Pass
        {
            Name "ParalateScreenFx"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Vert + _BlitTexture มาจากไฟล์นี้ (Blit มาตรฐานของ URP)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Grain: x = ความแรง, y = ขนาดเม็ด (พิกเซล), z = เปลี่ยนลายกี่ครั้ง/วิ, w = เน้นในที่มืด (0-1)
            float4 _GrainParams;
            float _GrainColored;
            // คลื่น: x = ความแรง (UV), y = จำนวนคลื่นต่อจอ, z = ความเร็ว, w = โค้งขอบจอ
            float4 _WaveParams;
            // เส้นกระตุก: x = โอกาส, y = ระยะเลื่อน (UV), z = สุ่มใหม่กี่ครั้ง/วิ, w = จำนวนแถบต่อจอ
            float4 _JitterParams;
            // สี: x = Chromatic Aberration, y = Saturation, z = Contrast
            float4 _ColorParams;
            float4 _Tint;
            // PS1: x = เปิด (0/1), y = ความสูงพิกเซล, z = จำนวนระดับสี, w = Dither
            float4 _Ps1Params;
            // x = Scanline, y = Vignette
            float4 _FrameParams;

            static const float BAYER4[16] =
            {
                 0,  8,  2, 10,
                12,  4, 14,  6,
                 3, 11,  1,  9,
                15,  7, 13,  5
            };

            float Bayer4x4(float2 pixel)
            {
                int x = (int)fmod(pixel.x, 4.0);
                int y = (int)fmod(pixel.y, 4.0);
                return BAYER4[y * 4 + x] / 16.0 - 0.5;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float3 SampleScene(float2 uv, float2 res, bool pixelate)
            {
                if (pixelate)
                {
                    float2 snapped = (floor(uv * res) + 0.5) / res;
                    return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, saturate(snapped), 0).rgb;
                }
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(uv), 0).rgb;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUv = input.texcoord;
                float2 uv = screenUv;
                float t = _Time.y;

                // ---------- 1) Distort
                // โค้งขอบจอ (+ = นูนแบบจอ CRT, - = เว้า)
                float2 c = uv - 0.5;
                uv = 0.5 + c * (1.0 + _WaveParams.w * dot(c, c));

                // คลื่นแนวนอนเคลื่อนช้าๆ (ภาพย้วย)
                uv.x += sin((uv.y * _WaveParams.y + t * _WaveParams.z) * 6.28318) * _WaveParams.x;

                // เส้นกระตุก: แบ่งจอเป็นแถบ สุ่มบางแถบให้เลื่อนข้าง (เปลี่ยนตาม FPS ที่ตั้ง)
                float jitterTick = floor(t * _JitterParams.z);
                float band = floor(uv.y * _JitterParams.w);
                float roll = Hash(float2(band, jitterTick));
                float hit = step(1.0 - _JitterParams.x * 0.25, roll); // โอกาส 1 = ประมาณ 1/4 ของแถบ
                uv.x += hit * (Hash(float2(jitterTick, band + 7.7)) - 0.5) * 2.0 * _JitterParams.y;

                // ---------- 2) อ่านภาพ (+ Pixelate ถ้าเปิด PS1) + Chromatic Aberration
                bool ps1 = _Ps1Params.x > 0.5;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 res = float2(_Ps1Params.y * aspect, _Ps1Params.y);

                float2 off = (uv - 0.5) * _ColorParams.x;
                float3 col;
                col.r = SampleScene(uv + off, res, ps1).r;
                col.g = SampleScene(uv, res, ps1).g;
                col.b = SampleScene(uv - off, res, ps1).b;

                // ---------- 3) สี
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(lum.xxx, col, _ColorParams.y);
                col = (col - 0.5) * _ColorParams.z + 0.5;
                col *= _Tint.rgb;

                // ---------- 4) Grain (ขนาดเม็ดตามพิกเซลจอจริง ไม่ขึ้นกับ PS1)
                float grainTick = floor(t * _GrainParams.z);
                float2 grainCell = floor(screenUv * _ScreenParams.xy / max(1.0, _GrainParams.y));
                float3 noise;
                noise.r = Hash(grainCell + grainTick * 17.13);
                noise.g = _GrainColored > 0.5 ? Hash(grainCell + grainTick * 31.71 + 5.3) : noise.r;
                noise.b = _GrainColored > 0.5 ? Hash(grainCell + grainTick * 47.37 + 9.1) : noise.r;
                float grainWeight = lerp(1.0, saturate(1.0 - lum) * 1.5, _GrainParams.w);
                col += (noise - 0.5) * _GrainParams.x * grainWeight;

                // ---------- 5) PS1: Dither + ลดระดับสี
                if (ps1)
                {
                    float2 pixel = floor(uv * res);
                    float steps = max(1.0, _Ps1Params.z - 1.0);
                    col += Bayer4x4(pixel) * _Ps1Params.w / steps;
                    col = round(saturate(col) * steps) / steps;
                }

                // ---------- 6) Scanline (ตามความสูงพิกเซล PS1 ถ้าเปิด ไม่เปิดใช้ 1 เส้นต่อ 3 พิกเซลจอ)
                float lines = ps1 ? res.y : _ScreenParams.y / 3.0;
                float scan = 0.5 + 0.5 * cos(screenUv.y * lines * 6.28318);
                col *= 1.0 - _FrameParams.x * scan;

                // ---------- 7) Vignette
                col *= 1.0 - _FrameParams.y * smoothstep(0.3, 0.85, length(screenUv - 0.5));

                return float4(max(col, 0.0), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
