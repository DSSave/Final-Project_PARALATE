// พอร์ตจาก HDRP Custom Pass → URP Full Screen Pass
// วิธีเปิดใช้: เลือก "New Universal Render Pipeline Asset_Renderer" > Add Renderer Feature > Full Screen Pass Renderer Feature
//   ใส่ Material ที่ใช้ Shader นี้ (PS1Signalis_URP.mat), Injection Point = Before Rendering Post Processing,
//   Requirements = Color (URP จะส่งภาพกล้องมาใน _BlitTexture ให้)
// สูตรภาพเหมือนเดิม: ลดความละเอียด → Chromatic Aberration → Color grading → Grain → Dither → Scanline → Vignette
Shader "FullScreen/PS1SignalisURP"
{
    Properties
    {
        _PixelHeight ("Pixel Height", Float) = 240
        _ColorLevels ("Color Levels", Range(4, 64)) = 16
        _DitherStrength ("Dither Strength", Range(0, 1)) = 0.6
        _Saturation ("Saturation", Range(0, 1.5)) = 0.75
        _Contrast ("Contrast", Range(0.5, 1.5)) = 1.1
        _Tint ("Tint", Color) = (0.85, 0.95, 1, 1)
        _Aberration ("Chromatic Aberration", Range(0, 0.02)) = 0.006
        _GrainStrength ("Grain Strength", Range(0, 0.3)) = 0.06
        _ScanlineStrength ("Scanline Strength", Range(0, 0.6)) = 0.18
        _Vignette ("Vignette", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off
        ZTest Always
        Blend Off
        Cull Off

        Pass
        {
            Name "PS1Signalis"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FullScreenPass

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Vert + _BlitTexture + _BlitTexture_TexelSize มาจากไฟล์นี้ (ระบบ Blit มาตรฐานของ URP/RTHandle)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelHeight;
            float _ColorLevels;
            float _DitherStrength;
            float _Saturation;
            float _Contrast;
            float4 _Tint;
            float _Aberration;
            float _GrainStrength;
            float _ScanlineStrength;
            float _Vignette;

            // ตาราง Bayer 4x4 สำหรับ ordered dithering
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
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            // อ่านสีภาพกล้องแบบ point โดย "ปัด" ตำแหน่งเป็นช่องพิกเซลหยาบๆ
            float3 SamplePixelated(float2 uv, float2 res)
            {
                float2 snapped = (floor(uv * res) + 0.5) / res;
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, saturate(snapped), 0).rgb;
            }

            float4 FullScreenPass(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // 1) ความละเอียดต่ำ
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 res = float2(_PixelHeight * aspect, _PixelHeight);
                float2 pixel = floor(uv * res);

                // 2) Chromatic Aberration
                float2 dir = uv - 0.5;
                float2 off = dir * _Aberration;
                float3 col;
                col.r = SamplePixelated(uv + off, res).r;
                col.g = SamplePixelated(uv, res).g;
                col.b = SamplePixelated(uv - off, res).b;

                // 3) Color grading
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(lum.xxx, col, _Saturation);
                col = (col - 0.5) * _Contrast + 0.5;
                col *= _Tint.rgb;

                // 4) Film grain (เปลี่ยนลายประมาณ 12 ครั้ง/วินาที)
                float t = floor(_Time.y * 12.0);
                col += (Hash(pixel + t * 17.0) - 0.5) * _GrainStrength;

                // 5) Dithering + ลดจำนวนระดับสี
                float steps = _ColorLevels - 1.0;
                col += Bayer4x4(pixel) * _DitherStrength / steps;
                col = round(saturate(col) * steps) / steps;

                // 6) Scanline
                float scan = 0.5 + 0.5 * cos(uv.y * res.y * 6.28318);
                col *= 1.0 - _ScanlineStrength * scan;

                // 7) Vignette
                float d = length(dir);
                col *= 1.0 - _Vignette * smoothstep(0.3, 0.85, d);

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
