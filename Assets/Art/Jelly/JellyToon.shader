// 점액 젤리(색 문·고정 장애물).
// 뒤 풍경을 굴절시켜 비추고(_CameraOpaqueTexture), 두꺼운 가운데일수록 _Color 쪽으로 진하게 흡수,
// 가장자리는 환경 반사(프레넬). 메인 라이트로 명암·광택, 거리 안개(Render Fog) 적용, 그림자 드리움.
// 굴절은 URP 에셋의 Opaque Texture가 켜져 있어야 한다(PC_RPAsset은 켜져 있음). 꺼져 있으면 뒤가 검게 나온다.
// 알파 블렌딩 대신 뒤 풍경을 직접 섞어 그리므로 결과는 불투명 — 젤리끼리 겹치면 뒤쪽 젤리는 안 비친다.
// 조명·광택·반사·얇은 곳 색 농도는 2026-09-29 T.Stage1에서 맞춘 값으로 고정(아래 static const). 인스펙터엔 색·농도·굴절·출렁임만.
Shader "Stage/JellyToon"
{
    Properties
    {
        _Color ("Color (플레이어 고유색)", Color) = (0.137, 0.518, 0.769, 1)
        _Density ("Density (클수록 덜 비침)", Range(0, 1)) = 0.85
        _Refraction ("Refraction (뒤가 휘는 정도, 화면 비율)", Range(0, 0.15)) = 0.04
        _WobbleAmp ("Wobble Amp (출렁임 크기, 오브젝트 공간, 0이면 정지)", Range(0, 0.2)) = 0.03
        _WobbleSpeed ("Wobble Speed (출렁임 속도)", Range(0, 10)) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Density;
            float _Refraction;
            float _WobbleAmp;
            float _WobbleSpeed;
        CBUFFER_END

        static const float kTint           = 0.8;  // 얇은 곳에서 뒤 풍경에 입히는 색 농도
        static const float kThicknessPower = 0.8;  // 클수록 진한 부분이 가운데로 모임
        static const float kDeepDarken     = 0.35; // 두꺼운 가운데 색 어둡게
        static const float kWrap           = 0.6;  // 명암을 뒷면까지 부드럽게
        static const float kGloss          = 160;  // 광택 날카로움
        static const float kSpecStrength   = 1.2;
        static const float kFresnelPower   = 4;
        static const float kReflStrength   = 0.45; // 가장자리 환경 반사
        static const float kWobbleFreq     = 1.2;

        // 오브젝트 공간 출렁임 — 높이에 따라 위상이 달라 위쪽이 더 늦게 따라온다.
        float3 Wobble(float3 positionOS, float3 normalOS)
        {
            float phase = _Time.y * _WobbleSpeed + positionOS.y * kWobbleFreq + positionOS.x * 0.7;
            return positionOS + normalOS * sin(phase) * _WobbleAmp;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend Off
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 posOS = Wobble(v.positionOS.xyz, v.normalOS);
                VertexPositionInputs pos = GetVertexPositionInputs(posOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor  = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float nv = saturate(dot(n, v));

                // 두께: 정면(가운데)일수록 두껍다고 본다.
                float thick = pow(nv, kThicknessPower);

                // 굴절된 뒤 풍경 → 얇은 곳은 색만 입히고, 두꺼운 곳은 젤리 색으로 흡수.
                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                float3 nVS = mul((float3x3)UNITY_MATRIX_V, n);
                float3 behind = SampleSceneColor(suv - nVS.xy * _Refraction);
                float3 thin = behind * lerp(float3(1, 1, 1), _Color.rgb * 1.6, kTint);

                Light mainLight = GetMainLight();
                float3 L = mainLight.direction;
                float ndl = saturate((dot(n, L) + kWrap) / (1.0 + kWrap));
                float3 lit = mainLight.color * ndl + SampleSH(n);
                float3 deep = _Color.rgb * (1.0 - kDeepDarken) * lit;

                float3 col = lerp(thin, deep, thick * _Density);

                // 가장자리 환경 반사 + 메인 라이트 광택.
                float fres = pow(1.0 - nv, kFresnelPower);
                float3 refl = GlossyEnvironmentReflection(reflect(-v, n), 0.05, 1.0);
                col = lerp(col, refl, fres * kReflStrength);

                float3 h = normalize(L + v);
                float spec = pow(saturate(dot(n, h)), kGloss) * kSpecStrength;
                col += mainLight.color * spec;

                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                float3 posWS = TransformObjectToWorld(Wobble(v.positionOS.xyz, v.normalOS));
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return cs;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
