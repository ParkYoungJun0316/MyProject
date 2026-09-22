Shader "Custom/ArrowWarnMarker"
{
    Properties
    {
        // 2026-09-22 재설계: "노랑→빨강 채움 막대" 폐기 → 폭 표시(테두리, 경고 내내 고정) +
        // 발사 타이밍 표시(흰 섬광이 입→끝으로 이동, 끝에 닿는 순간 = 발사). ArrowWarnSign이
        // 여전히 _Fill(0→1, 경고 시작→발사)만 매 프레임 갱신 — 셰이더 쪽 해석만 바뀜.
        [MainColor] _BaseColor ("Border / Backdrop Color", Color) = (0.93, 0.16, 0.22, 0.95)
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _Fill ("Progress (0=경고 시작, 1=발사)", Range(0, 1)) = 0
        _RingWidth ("Ring Width", Range(0.02, 0.45)) = 0.14
        _CapWidth ("End Cap Width", Range(0.005, 0.15)) = 0.03
        _Softness ("Softness", Range(0.001, 0.12)) = 0.02
        _Glow ("Glow", Range(0, 4)) = 1.4
        _InvertAlong ("Invert Along", Float) = 0
        _TrailWidth ("Flash Trail Width (0~1, 진행 축 기준)", Range(0.05, 0.6)) = 0.32
        _BackdropAlpha ("Backdrop Alpha (경고 내내 고정)", Range(0, 0.6)) = 0.14
        _FlashAlpha ("Flash Alpha", Range(0, 1)) = 0.95
        _RingAlpha ("Ring Alpha (경고 내내 고정)", Range(0, 1)) = 0.9
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
                float4 _FlashColor;
                float _Fill;
                float _RingWidth;
                float _CapWidth;
                float _Softness;
                float _Glow;
                float _InvertAlong;
                float _TrailWidth;
                float _BackdropAlpha;
                float _FlashAlpha;
                float _RingAlpha;
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

            // Porter-Duff "over": over 레이어(A,C)를 under 레이어(A,C) 위에 합성.
            float4 Over(float aOver, float3 cOver, float aUnder, float3 cUnder)
            {
                float aOut = aOver + aUnder * (1.0 - aOver);
                float3 cOut = (cOver * aOver + cUnder * aUnder * (1.0 - aOver)) / max(aOut, 1e-4);
                return float4(cOut, aOut);
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

                // 폭 표시: 양옆 테두리 + 끝 선. 경고 내내 고정 알파로 계속 보인다.
                float sideRail = smoothstep(1.0 - ringW - soft, 1.0 - ringW, ax);
                float endCap = smoothstep(1.0 - capW - soft, 1.0 - capW, az);
                float ring = saturate(max(sideRail, endCap)) * inside;

                float along = saturate(input.positionOS.z + 0.5);
                if (_InvertAlong > 0.5)
                    along = 1.0 - along;

                // 발사 타이밍 표시: 진행도(_Fill)만큼 이동한 위치에서 뒤로 trail만큼 옅어지는
                // 흰 섬광. 머리(along==fill)가 밝고, 아직 안 지난 앞쪽(along>fill)은 안 보인다.
                float fillHead = saturate(_Fill);
                float trail = max(_TrailWidth, 0.001);
                float dist = fillHead - along; // >=0: 섬광이 이미 지나간 뒤쪽
                float gate = smoothstep(-soft * 4.0, soft * 4.0, dist); // dist=0 부근을 부드럽게
                float flash = saturate(1.0 - dist / trail) * gate;
                float flashAlpha = flash * inside * _FlashAlpha;

                // 배경 은은한 틴트: 경고 구간 전체(폭)를 항상 옅게 보여줘 위험 범위를 계속 인지시킴.
                float backdropAlpha = _BackdropAlpha * inside;

                // 합성 순서(아래→위): 배경 틴트 → 섬광 → 테두리(폭 선이 항상 맨 위)
                float4 layer1 = float4(_BaseColor.rgb, backdropAlpha);
                float4 layer2 = Over(flashAlpha, _FlashColor.rgb, layer1.a, layer1.rgb);
                float4 layer3 = Over(ring * _RingAlpha, _BaseColor.rgb, layer2.a, layer2.rgb);

                half4 col;
                col.rgb = layer3.rgb * (1.0 + _Glow);
                col.a = layer3.a * _BaseColor.a;
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
