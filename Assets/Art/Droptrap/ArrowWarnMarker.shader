Shader "Custom/ArrowWarnMarker"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1, 0.92, 0.05, 0.8)
        _Fill ("Fill", Range(0, 1)) = 0
        _RingWidth ("Ring Width", Range(0.02, 0.45)) = 0.14
        _CapWidth ("End Cap Width", Range(0.005, 0.15)) = 0.03
        _Softness ("Softness", Range(0.001, 0.12)) = 0.02
        _Glow ("Glow", Range(0, 4)) = 1.4
        _InvertAlong ("Invert Along", Float) = 0
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
                float4 _BaseColor;
                float _Fill;
                float _RingWidth;
                float _CapWidth;
                float _Softness;
                float _Glow;
                float _InvertAlong;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Unity cube is ±0.5. X = width, Z = length (fire forward when local rot is 0).
                float ax = abs(input.positionOS.x) * 2.0;
                float az = abs(input.positionOS.z) * 2.0;
                float soft = max(_Softness, 0.001);
                float ringW = max(_RingWidth, 0.02);
                float capW = max(_CapWidth, 0.005);

                float insideX = 1.0 - smoothstep(1.0 - soft, 1.0, ax);
                float insideZ = 1.0 - smoothstep(1.0 - soft, 1.0, az);
                float inside = insideX * insideZ;

                float sideRail = smoothstep(1.0 - ringW - soft, 1.0 - ringW, ax);
                float endCap = smoothstep(1.0 - capW - soft, 1.0 - capW, az);
                float ring = saturate(max(sideRail, endCap)) * inside;

                float along = saturate(input.positionOS.z + 0.5);
                if (_InvertAlong > 0.5)
                    along = 1.0 - along;

                float fill = saturate(_Fill);
                float fillMask = 1.0 - smoothstep(fill, fill + soft, along);
                float interior = (1.0 - sideRail) * inside;
                float fillAlpha = interior * fillMask;

                float alphaMask = saturate(ring + fillAlpha);
                half4 col = _BaseColor;
                col.rgb *= 1.0 + _Glow;
                col.a *= alphaMask;
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
