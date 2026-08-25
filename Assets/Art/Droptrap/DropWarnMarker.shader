Shader "Custom/DropWarnMarker"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.15, 0.1, 0.55)
        _Fill ("Fill", Range(0, 1)) = 0
        _RingWidth ("Ring Width", Range(0.01, 0.2)) = 0.06
        _Softness ("Softness", Range(0.001, 0.1)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Fill;
                float _RingWidth;
                float _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // UV center (0.5,0.5) → radial distance 0..~0.707; normalize to 0..1 circle
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);

                float soft = max(_Softness, 0.001);
                float ringW = max(_RingWidth, 0.01);

                // Outside circle → discard visually
                float circle = 1.0 - smoothstep(1.0 - soft, 1.0, r);

                // Thin outer ring (always visible at Fill=0)
                float ringOuter = 1.0 - smoothstep(1.0 - ringW, 1.0 - ringW + soft, r);
                float ringInner = smoothstep(1.0 - ringW * 2.0 - soft, 1.0 - ringW * 2.0, r);
                float ring = ringOuter * ringInner;

                // Fill from outside inward: filled when r >= (1 - Fill)
                float fill = saturate(_Fill);
                float innerR = 1.0 - fill;
                float fillMask = 1.0 - smoothstep(innerR, innerR + soft, r);
                // Keep a small hole until fill is nearly full
                float centerHole = smoothstep(0.0, soft, r);
                fillMask *= lerp(centerHole, 1.0, saturate(fill / 0.95));

                float alphaMask = saturate(ring + fillMask * fill) * circle;
                half4 col = _Color;
                col.a *= alphaMask;
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
