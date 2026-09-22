Shader "Custom/WarnMarker"
{
    Properties
    {
        // 모든 경고 사인의 공용 셰이더 (2026-09-22, 구 ArrowWarnMarker — GUID 유지).
        // 색은 C#(WarnMarkerColorFx)이 WarnPalette에서 매 프레임 넣는다:
        //   _BaseColor = Start(탠저린)→End(진홍) 보간색, _BorderColor = WarnPalette.Border,
        //   _Progress = 시간 진행도 0→1 (펄스 기준), _Fill = 채움 위치 (_FillMode=0일 때만 의미).
        // _FillMode 0 = 방향 채움(화살 레인: 입→끝, ArrowWarnMarker.mat)
        // _FillMode 1 = 색만 전환(칸형: 가시·BreakTile·Grid 붕괴·혀·턱·바람 존, WarnMarkerTile.mat)
        // 채움·배경·안쪽 선은 현재 보간색, 바깥 테두리만 _BorderColor.
        //
        // 폭·길이 관련 값은 전부 "미터" 단위다 — 오브젝트 스케일을 셰이더에서 읽어 환산하므로
        // 1.4m×50m 레인이든 2m×2m 칸이든 테두리/머리선 두께가 똑같이 보인다.
        // (이전 버전은 0~1 비율이라 50m 레인에서 꼬리 16m·끝선 1.5m로 부풀었음)
        //
        // 색은 인스펙터 값이 곧 화면 최종색 — Glow 곱셈 없음(Linear 공간에서 R이 먼저 잘려
        // 분홍으로 바래던 문제 제거).
        [MainColor] _BaseColor ("Current Color (ArrowWarnSign이 덮어씀)", Color) = (1, 0.604, 0.239, 1)
        _BorderColor ("Outer Border Color", Color) = (0.557, 0.059, 0.090, 1)
        _Fill ("Fill Position (0=입, 1=끝 — ArrowWarnSign이 곡선 적용 후 넘김)", Range(0, 1)) = 0
        _Progress ("Time Progress (0=경고 시작, 1=발사 — 펄스 기준)", Range(0, 1)) = 0
        _InvertAlong ("Invert Along", Float) = 0
        [Enum(Directional Fill, 0, Color Only, 1)] _FillMode ("Fill Mode", Float) = 0

        _BorderWidth ("Outer Border Width (m)", Range(0.02, 0.5)) = 0.12
        _InnerLineWidth ("Inner Line Width (m)", Range(0, 0.2)) = 0.05
        _HeadWidth ("Fill Head Width (m)", Range(0, 0.5)) = 0.15
        _Softness ("Edge Softness (m)", Range(0.001, 0.1)) = 0.015

        _BackdropAlpha ("Backdrop Alpha (경고 내내 고정)", Range(0, 0.6)) = 0.22
        _FillAlphaStart ("Fill Alpha at Warn Start (시간 진행도 0)", Range(0, 1)) = 0.78
        _FillAlpha ("Fill Alpha at Fire (시간 진행도 1)", Range(0, 1)) = 0.78
        _BorderAlpha ("Border Alpha", Range(0, 1)) = 1

        _PulseStart ("Pulse Start (시간 진행도, 이후 발사까지 한 번 밝아짐)", Range(0.5, 1)) = 0.87
        _PulseTint ("Pulse Tint", Color) = (1, 0.925, 0.878, 1)
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.35
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
                float4 _BorderColor;
                float _Fill;
                float _Progress;
                float _InvertAlong;
                float _FillMode;
                float _BorderWidth;
                float _InnerLineWidth;
                float _HeadWidth;
                float _Softness;
                float _BackdropAlpha;
                float _FillAlphaStart;
                float _FillAlpha;
                float _BorderAlpha;
                float _PulseStart;
                float4 _PulseTint;
                float _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 halfSizeM : TEXCOORD1; // x=폭 절반(m), y=길이 절반(m), z=윗면 여부(1/0)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;

                float4x4 m = GetObjectToWorldMatrix();
                float scaleX = length(float3(m[0].x, m[1].x, m[2].x));
                float scaleZ = length(float3(m[0].z, m[1].z, m[2].z));
                output.halfSizeM = float3(0.5 * scaleX, 0.5 * scaleZ, input.normalOS.y > 0.5 ? 1.0 : 0.0);
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
                // Cube의 윗면만 그린다 — Cull Off로 아랫면까지 겹쳐 그려져 알파가 두 배로 진해지던 문제 제거.
                if (input.halfSizeM.z < 0.5) discard;

                // Unity cube is ±0.5. X = width, Z = length (fire forward when local rot is 0).
                float halfW = max(input.halfSizeM.x, 1e-4);
                float halfL = max(input.halfSizeM.y, 1e-4);
                float soft = max(_Softness, 0.001);

                // 가장 가까운 가장자리까지 거리(m). 0 = 가장자리, 클수록 안쪽.
                float edgeX = halfW - abs(input.positionOS.x) * 2.0 * halfW;
                float edgeZ = halfL - abs(input.positionOS.z) * 2.0 * halfL;
                float edge = min(edgeX, edgeZ);

                float inside = smoothstep(0.0, soft, edge);

                // 바깥 테두리(고정색) + 그 안쪽 얇은 선(현재 보간색) — 모양이 어떤 바닥 위에서도 읽히게.
                float border = (1.0 - smoothstep(_BorderWidth, _BorderWidth + soft, edge)) * inside;
                float innerStart = _BorderWidth + soft;
                float innerLine = smoothstep(innerStart, innerStart + soft, edge)
                                * (1.0 - smoothstep(innerStart + _InnerLineWidth, innerStart + _InnerLineWidth + soft, edge));

                // 진행 축(0=입, 1=끝)을 미터로.
                float along = saturate(input.positionOS.z + 0.5);
                if (_InvertAlong > 0.5)
                    along = 1.0 - along;
                float alongM = along * 2.0 * halfL;
                float fillM = saturate(_Fill) * 2.0 * halfL;

                // 발사 직전 펄스: 시간 진행도 _PulseStart~1 구간에서 sin 한 번 (0→1→0).
                // 채움 위치(_Fill)는 곡선이 적용돼 있으므로 펄스는 반드시 시간(_Progress) 기준.
                float pulse = _Progress >= _PulseStart
                    ? sin(PI * saturate((_Progress - _PulseStart) / max(1.0 - _PulseStart, 1e-4)))
                    : 0.0;
                float3 cur = _BaseColor.rgb;
                float3 lit = lerp(cur, _PulseTint.rgb, pulse * _PulseAmount);

                float filled = 1.0 - smoothstep(fillM - soft, fillM + soft, alongM);
                float head = (_Fill > 0.001 && _Fill < 0.999)
                    ? smoothstep(fillM - _HeadWidth - soft, fillM - _HeadWidth, alongM) * filled
                    : 0.0;

                // 색만 전환 모드: 칸 전체가 늘 채워진 상태(색만 보간), 머리선 없음.
                if (_FillMode > 0.5)
                {
                    filled = 1.0;
                    head = 0.0;
                }

                // 합성 순서(아래→위): 배경 → 채움 → 머리선 → 안쪽 선 → 바깥 테두리
                float4 c = float4(cur, _BackdropAlpha * inside);
                // 채움 불투명도는 시간 진행도(_Progress)에 따라 Start → Fill로 올라간다 — 경고가 번쩍 켜지지 않고
                // 옅게 나타나 진해지게(칸형 멀미 대응, WarnMarkerTile.mat 0.25 → 0.55). 기본값은 둘이 같아 화살은 변화 없음.
                float fillAlpha = lerp(_FillAlphaStart, _FillAlpha, saturate(_Progress));
                c = Over(filled * inside * saturate(fillAlpha + pulse * 0.2), lit, c.a, c.rgb);
                c = Over(head * inside * 0.95, lerp(lit, float3(1, 1, 1), 0.25), c.a, c.rgb);
                c = Over(innerLine * inside, lit, c.a, c.rgb);
                c = Over(border * _BorderAlpha, _BorderColor.rgb, c.a, c.rgb);

                // _BaseColor.a = ArrowWarnSign의 난이도 페이드(fadePhases) 알파 배율.
                return half4(c.rgb, c.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
