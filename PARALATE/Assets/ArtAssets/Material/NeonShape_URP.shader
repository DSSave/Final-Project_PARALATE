// พอร์ตจาก HDRP → URP: เปลี่ยน include ไปใช้ ShaderLibrary ของ URP, Tag RenderPipeline/LightMode เป็นของ URP
// สูตรสีเหมือนเดิมทุกอย่าง (Unlit เรืองแสง: สีใน + เส้นกริด UV + ขอบ Fresnel)
Shader "Codex/Neon Shape URP"
{
    Properties
    {
        [HDR] _BaseColor ("Inside Color", Color) = (0.015, 0.045, 0.07, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.05, 0.65, 1, 1)
        _EdgeWidth ("UV Edge Width", Range(0.001, 0.2)) = 0.025
        _FresnelWidth ("Silhouette Width", Range(0.01, 1)) = 0.25
        _FresnelPower ("Silhouette Falloff", Range(0.5, 8)) = 3
        _EmissionStrength ("Glow Strength", Range(0, 20)) = 4
        _GridScale ("UV Grid Scale", Range(1, 20)) = 1
        _GridWidth ("Grid Line Width", Range(0.001, 0.1)) = 0.012
        [HDR] _GridColor ("Grid Color", Color) = (0.02, 0.25, 0.42, 1)
        _GridStrength ("Grid Strength", Range(0, 10)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _EdgeColor;
            float4 _GridColor;
            float _EdgeWidth;
            float _FresnelWidth;
            float _FresnelPower;
            float _EmissionStrength;
            float _GridScale;
            float _GridWidth;
            float _GridStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend One Zero
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetCameraPositionWS() - input.positionWS);
                float fresnel = pow(saturate(1.0 - dot(n, v)), _FresnelPower);
                float silhouette = smoothstep(1.0 - _FresnelWidth, 1.0, fresnel);
                float2 cell = frac(input.uv * _GridScale);
                float edgeDistance = min(min(cell.x, 1.0 - cell.x), min(cell.y, 1.0 - cell.y));
                float grid = 1.0 - smoothstep(_GridWidth, _GridWidth * 1.8, edgeDistance);
                float3 color = _BaseColor.rgb;
                color += _GridColor.rgb * grid * _GridStrength;
                color += _EdgeColor.rgb * silhouette * _EmissionStrength;
                color += _EdgeColor.rgb * grid * _EmissionStrength;
                return float4(color, 1.0);
            }
            ENDHLSL
        }

        // ให้วัตถุนี้เขียน Depth (จำเป็นต่อ SSAO / Depth Texture ของ URP)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            float4 DepthVert(float3 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS); }
            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
