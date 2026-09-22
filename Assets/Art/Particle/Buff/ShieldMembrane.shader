// Shield 버프 투명막.
// 가운데는 거의 투명, 가장자리(프레넬)로 갈수록 _Color가 진해진다. 발광/무늬/애니메이션 없음.
// _Color는 플레이어 고유색 — ShieldBubbleFx가 MaterialPropertyBlock으로 넣는다.
Shader "Buff/ShieldMembrane"
{
    Properties
    {
        _Color ("Color (플레이어 고유색)", Color) = (0.137, 0.518, 0.769, 1)
        _FillAlpha ("Fill Alpha (가운데 농도)", Range(0, 1)) = 0.06
        _RimAlpha ("Rim Alpha (가장자리 농도)", Range(0, 1)) = 0.75
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _BackFaceAlpha ("Back Face Alpha", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _FillAlpha;
            float _RimAlpha;
            float _RimPower;
            float _BackFaceAlpha;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS   : TEXCOORD0;
            float3 viewDirWS  : TEXCOORD1;
        };

        Varyings Vert(Attributes v)
        {
            Varyings o;
            VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
            o.positionCS = pos.positionCS;
            o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
            o.viewDirWS  = GetWorldSpaceViewDir(pos.positionWS);
            return o;
        }

        half4 Shade(Varyings i, float faceSign)
        {
            float3 n = normalize(i.normalWS) * faceSign;
            float3 v = normalize(i.viewDirWS);
            float rim = pow(1.0 - saturate(dot(n, v)), _RimPower);
            float alpha = lerp(_FillAlpha, _RimAlpha, rim);
            return half4(_Color.rgb, alpha * _Color.a);
        }
        ENDHLSL

        // 뒷면 먼저 (막의 두께감)
        Pass
        {
            Name "Back"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = Shade(i, -1.0);
                c.a *= _BackFaceAlpha;
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Front"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings i) : SV_Target { return Shade(i, 1.0); }
            ENDHLSL
        }
    }
    FallBack Off
}
