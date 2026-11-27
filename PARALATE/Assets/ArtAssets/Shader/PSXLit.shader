// Shader สำหรับ Material สไตล์ PS1 (King's Field / Sorry We're Closed)
//   1) Vertex Snap   : ปัดตำแหน่งจุดของโมเดลบนจอให้ลงกริดพิกเซลหยาบๆ → โมเดลสั่น/กระตุกเวลากล้องหรือของขยับ
//   2) Affine UV     : ไม่แก้ Perspective ของ Texture → Texture บิดเบี้ยวบนพื้น/ผนังที่มองเฉียง
//   3) Vertex Light  : คำนวณแสงที่จุดของโมเดลแล้วไล่สีบนหน้า (ไม่ใช่ทีละพิกเซล) — แสงหลัก + แสงเสริม (Forward) + Ambient
//   4) Fog ของ Unity (Lighting > Environment > Fog) + เงาจากแสงหลัก
// กริดของ Vertex Snap: ค่า Snap Resolution ของ Material (0 = ใช้ Pixel Height ของ Paralate Screen FX → ตรงกับพิกเซลของฟิลเตอร์)
// Texture แนะนำ Filter Mode = Point (หรือติ๊ก Point Filter ใน Material)
Shader "Paralate/PSX Lit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Toggle] _PointFilter ("Point Filter (บังคับ ไม่สนค่า Import)", Float) = 1
        [Toggle] _UseVertexColor ("คูณสีจาก Vertex Color", Float) = 0

        [Header(PSX)]
        _SnapResolution ("Snap Resolution (เส้นแนวตั้ง, 0 = ตาม Screen FX)", Float) = 0
        _SnapStrength ("Vertex Snap", Range(0, 1)) = 1
        _AffineStrength ("Affine Texture Warp", Range(0, 1)) = 1

        [Header(Lighting)]
        [Toggle(_PSX_UNLIT)] _Unlit ("Unlit (ไม่รับแสง)", Float) = 0
        _AmbientStrength ("Ambient", Range(0, 2)) = 1
        _LightSteps ("Light Steps (0 = ไล่สีเนียน)", Range(0, 16)) = 0
        [Toggle] _ReceiveShadows ("Receive Shadows", Float) = 1

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap ("Emission Map", 2D) = "white" {}

        [Header(Surface)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Unlit" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _PointFilter;
            float _UseVertexColor;
            float _SnapResolution;
            float _SnapStrength;
            float _AffineStrength;
            half _AmbientStrength;
            half _LightSteps;
            float _ReceiveShadows;
            half4 _EmissionColor;
            float4 _EmissionMap_ST;
            half _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap);  SAMPLER(sampler_EmissionMap);
        SAMPLER(psx_point_repeat_sampler); // Inline sampler: ชื่อมี point + repeat

        // ตั้งจาก ParalateScreenFxFeature ทุกเฟรม (= Pixel Height ของ Screen FX)
        float _PSX_GridHeight;

        // ปัดตำแหน่งบนจอ (Clip Space) ให้ลงกริด → อาการจุดสั่นแบบ PS1 (PS1 ไม่มีทศนิยมย่อยพิกเซล)
        float4 PsxSnap(float4 positionCS)
        {
            float lines = _SnapResolution > 0 ? _SnapResolution : (_PSX_GridHeight > 0 ? _PSX_GridHeight : 240.0);
            float2 grid = float2(lines * _ScreenParams.x / _ScreenParams.y, lines) * 0.5;
            float w = max(positionCS.w, 1e-4);
            float2 ndc = positionCS.xy / w;
            float2 snapped = round(ndc * grid) / grid;
            positionCS.xy = lerp(ndc, snapped, _SnapStrength) * w;
            return positionCS;
        }

        half4 SampleBase(float2 uv)
        {
            return _PointFilter > 0.5
                ? SAMPLE_TEXTURE2D(_BaseMap, psx_point_repeat_sampler, uv)
                : SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
        }
        ENDHLSL

        // ------------------------------------------------------------------ Forward
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _PSX_UNLIT
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0; // แบบปกติ (มี Perspective)
                float3 uvAffine   : TEXCOORD1; // xy = uv*w, z = w → หารในพิกเซล = Affine
                float3 positionWS : TEXCOORD2;
                half3 mainLight   : TEXCOORD3; // แสงหลัก (ยังไม่คูณเงา)
                half3 otherLight  : TEXCOORD4; // Ambient + แสงเสริม
                half4 color       : TEXCOORD5;
                half fogFactor    : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 positionCS = PsxSnap(TransformWorldToHClip(positionWS));
                o.positionCS = positionCS;
                o.positionWS = positionWS;

                float2 uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.uv = uv;
                o.uvAffine = float3(uv * positionCS.w, positionCS.w);
                o.color = _UseVertexColor > 0.5 ? input.color : half4(1, 1, 1, 1);
                o.fogFactor = ComputeFogFactor(positionCS.z);

            #if !defined(_PSX_UNLIT)
                // แสงต่อจุด (Gouraud) แบบ PS1
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                Light mainLight = GetMainLight();
                o.mainLight = mainLight.color * saturate(dot(normalWS, mainLight.direction));

                half3 other = SampleSH(normalWS) * _AmbientStrength;
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                    uint count = GetAdditionalLightsCount();
                    for (uint i = 0u; i < count; i++)
                    {
                        Light light = GetAdditionalLight(i, positionWS);
                        other += light.color * light.distanceAttenuation * saturate(dot(normalWS, light.direction));
                    }
                #endif
                o.otherLight = other;
            #endif
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 affine = input.uvAffine.xy / max(input.uvAffine.z, 1e-4);
                float2 uv = lerp(input.uv, affine, _AffineStrength);

                half4 albedo = SampleBase(uv) * _BaseColor * input.color;
            #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
            #endif

                half3 color = albedo.rgb;
            #if !defined(_PSX_UNLIT)
                half shadow = 1;
                if (_ReceiveShadows > 0.5)
                    shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(input.positionWS));
                half3 light = input.mainLight * shadow + input.otherLight;
                if (_LightSteps > 0.5)
                    light = floor(light * _LightSteps + 0.5) / _LightSteps; // แสงเป็นขั้นๆ
                color *= light;
            #endif

                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ เงา
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
            #if defined(_ALPHATEST_ON)
                clip(SampleBase(input.uv).a * _BaseColor.a - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ Depth (ต้องสั่นเหมือนภาพจริง ไม่งั้นความลึกไม่ตรง)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = PsxSnap(TransformObjectToHClip(input.positionOS.xyz));
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
            #if defined(_ALPHATEST_ON)
                clip(SampleBase(input.uv).a * _BaseColor.a - _Cutoff);
            #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
