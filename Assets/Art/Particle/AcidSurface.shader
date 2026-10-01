// 위액 수면(T.Stage3 차오르는 위액 · T.Stage4 · T.Boss 바닥 아래 수면). GastricAcidPlane.mat이 쓴다.
// 물결 노멀 두 겹을 월드 XZ 기준으로 서로 다른 방향으로 흘려 광택·색 얼룩이 일렁이게 한다.
// 화면 이미지(_CameraOpaqueTexture)는 절대 가져오지 않는다 — 2026-10-02 파티클 Distortion이 플레이어 모습을
// 수면에 찍어 멀미를 유발해서 이 셰이더로 교체했다. 굴절·화면 샘플링을 되살리지 말 것.
// 월드 좌표로 무늬를 깔아서 씬마다 다른 Plane 스케일(T3 구간 길이 · T4 34×360 · T.Boss 250×250)과 상관없이 무늬 크기가 같다.
// 기하와 닿는 곳은 깊이 텍스처로 부드럽게 흐려진다(PC_RPAsset Depth Texture 켜져 있음).
Shader "Stage/AcidSurface"
{
    Properties
    {
        _Color ("Color (알파 = 불투명도)", Color) = (0.55, 0.66, 0.05, 0.62)
        _EmissionColor ("Emission (약한 자체 발광)", Color) = (0.12, 0.18, 0, 1)
        [Normal][NoScaleOffset] _BumpMap ("Wave Normal", 2D) = "bump" {}
        _Tiling ("Tiling (무늬 한 장 크기, m)", Float) = 8
        _NormalStrength ("Normal Strength (물결 세기)", Range(0, 2)) = 1.4
        _ScrollA ("Scroll A (첫 겹 흐름, m/초 xz)", Vector) = (0.35, 0.2, 0, 0)
        _ScrollB ("Scroll B (둘째 겹 흐름, m/초 xz)", Vector) = (-0.25, 0.3, 0, 0)
        _Blotch ("Blotch (물결 따라 색 얼룩)", Range(0, 1)) = 0.7
        _Gloss ("Gloss (광택 날카로움)", Range(8, 512)) = 96
        _SpecStrength ("Spec Strength (광택 밝기)", Range(0, 3)) = 0.8
        _SoftFade ("Soft Fade (닿는 곳 흐림 거리, m)", Range(0.01, 3)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            // 화면 공간 그림자(_MAIN_LIGHT_SHADOWS_SCREEN)는 투명에 안 맞아 뺌.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _EmissionColor;
                float _Tiling;
                float _NormalStrength;
                float4 _ScrollA;
                float4 _ScrollB;
                float _Blotch;
                float _Gloss;
                float _SpecStrength;
                float _SoftFade;
            CBUFFER_END

            static const float kWrap = 0.5; // 명암을 부드럽게

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float  fogFactor  : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.fogFactor  = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // 물결 두 겹 — 크기·방향이 달라 반복 무늬가 덜 보인다.
                float tiling = max(_Tiling, 0.01);
                float2 uvA = (i.positionWS.xz + _ScrollA.xy * _Time.y) / tiling;
                float2 uvB = (i.positionWS.xz + _ScrollB.xy * _Time.y) / (tiling * 1.7);
                float3 nA = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvA));
                float3 nB = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvB));
                float2 slope = (nA.xy + nB.xy) * _NormalStrength;
                float3 n = normalize(float3(slope.x, 1.0, slope.y)); // 수평 Plane 기준 탄젠트→월드

                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 L = mainLight.direction;
                float atten = mainLight.shadowAttenuation;
                float ndl = saturate((dot(n, L) + kWrap) / (1.0 + kWrap));

                // 물결 기울기 따라 색을 밝게/어둡게 — 얼룩이 흘러가 보인다.
                float blotch = 1.0 + (slope.x + slope.y) * _Blotch;
                float3 albedo = _Color.rgb * blotch;
                float3 col = albedo * (mainLight.color * ndl * atten + SampleSH(n)) + _EmissionColor.rgb;

                // 메인 라이트 광택 + 가장자리 환경 반사(스카이박스·프로브만 — 플레이어는 안 비친다).
                float3 h = normalize(L + v);
                float spec = pow(saturate(dot(n, h)), _Gloss) * _SpecStrength * atten;
                col += mainLight.color * spec;
                float fres = pow(1.0 - saturate(dot(n, v)), 5);
                col = lerp(col, GlossyEnvironmentReflection(reflect(-v, n), 0.2, 1.0), fres * 0.3);

                // 기하와 닿는 곳 부드럽게.
                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float surfEye = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float soft = saturate((sceneEye - surfEye) / _SoftFade);

                float alpha = saturate(_Color.a + spec * 0.5) * soft;
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
