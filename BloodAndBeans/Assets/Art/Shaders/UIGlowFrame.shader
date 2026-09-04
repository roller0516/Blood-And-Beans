// 희귀 칸을 감싸는 발광 코너 프레임. 네 모서리에 ㄱ자만 남기고 안쪽으로 빛이 번진다.
//
// 번짐을 셰이더가 직접 그리는 이유는 이 프로젝트 UI 캔버스가 전부 Screen Space - Overlay라
// 블룸이 걸리지 않기 때문이다. Overlay는 카메라 포스트 처리가 끝난 뒤에 그려지므로 HDR
// 색을 넣어도 그냥 흰색으로 잘린다. 월드 쪽 `ItemBoxGlow`가 블룸에 실릴 수 있는 것과
// 다른 조건이다.
//
// 밝기와 알파는 `Image.color`(정점 색)로 조절한다. uGUI는 `MaterialPropertyBlock`을
// 받지 않아서(CanvasRenderer가 배칭한다) 칸마다 값을 다르게 하려면 머티리얼을 복제해야
// 하는데, 그러면 칸 수만큼 머티리얼이 생기고 배칭이 깨진다. 정점 색은 공짜로 칸마다
// 다르고 트윈도 `Image.color` 하나로 끝난다.
Shader "BB/UI Glow Frame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Radius ("모서리 둥글기", Range(0, 0.5)) = 0.12
        _Thickness ("선 두께", Range(0.001, 0.2)) = 0.012
        _GlowWidth ("번짐 폭", Range(0.001, 0.5)) = 0.09
        _GlowPower ("번짐 감쇠", Range(0.5, 8)) = 2.5
        _CoreBoost ("선 심지 밝기", Range(1, 8)) = 3

        // 테두리를 따라 도는 조각들. 4개에 머리 길이를 키우면 예전의 코너 브래킷이 된다.
        [IntRange] _Count ("조각 수 (최대 4)", Range(1, 4)) = 4
        _HeadLength ("머리 길이 (둘레 비율)", Range(0.005, 0.25)) = 0.06
        _TrailLength ("꼬리 길이 (둘레 비율)", Range(0.005, 0.5)) = 0.08
        _Speed ("도는 속도 (바퀴/초, 음수면 반대)", Float) = 0.35
        _Offset ("시작 위치", Range(0, 1)) = 0

        // 1이면 조각을 무시하고 테두리 전체를 고르게 그린다. 예전 `Outline` 컴포넌트
        // 자리를 대신하는 정지 테두리가 이 모드다.
        _Ring ("완전한 테두리", Range(0, 1)) = 0

        // RectTransform의 가로세로 비. 넣지 않으면 세로로 긴 칸에서 코너가 늘어난다.
        _Aspect ("가로/세로 비", Float) = 1

        // uGUI 마스크(Mask/RectMask2D)가 쓰는 값. UI 셰이더는 이걸 갖고 있어야
        // 마스크 안에서 잘린다. 지금 이 창에는 마스크가 없지만 나중에 생겨도 동작한다.
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        // 더하기 혼합이다. 발광은 뒤를 가리는 것이 아니라 얹히는 것이라, 알파 혼합으로
        // 그리면 어두운 배경 위에서 회색 판처럼 보인다.
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Radius;
                float  _Thickness;
                float  _GlowWidth;
                float  _GlowPower;
                float  _CoreBoost;
                float  _Aspect;
                float  _Count;
                float  _HeadLength;
                float  _TrailLength;
                float  _Speed;
                float  _Offset;
                float  _Ring;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            /// 둥근 사각형까지의 거리. 바깥이 양수다.
            float RoundedBox(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            /// 테두리 위 한 점이 둘레의 어디인가 (0~1). 오른쪽 변 가운데 아래에서 시작해
            /// 반시계로 돈다. 정사각형이면 네 모서리가 정확히 0 / 0.25 / 0.5 / 0.75다 —
            /// 그래서 조각 4개에 `_Offset` 0이면 예전의 코너 브래킷과 같은 자리에 선다.
            ///
            /// 정확한 호 길이가 아니라 변을 따라 잰 길이다. 둥근 모서리에서 아주 조금
            /// 어긋나지만 눈으로 보이지 않고, 정확히 재려면 타원 적분이 필요하다.
            float PerimeterCoord(float2 p, float2 halfSize)
            {
                float a = halfSize.x;
                float b = halfSize.y;
                float perimeter = 4.0 * (a + b);

                // 어느 변에 붙은 점인가. 정규화해서 비교해야 납작한 칸에서도 맞다.
                float2 n = p / max(halfSize, 1e-4);

                float s;
                if (abs(n.x) >= abs(n.y))
                    s = p.x >= 0.0 ? (p.y + b)
                                   : (2.0 * b + 2.0 * a + (b - p.y));
                else
                    s = p.y >= 0.0 ? (2.0 * b + (a - p.x))
                                   : (4.0 * b + 2.0 * a + (p.x + a));

                return s / max(perimeter, 1e-4);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // uv를 중심 원점으로 옮기고 가로세로 비를 편다. 펴지 않으면 세로로 긴
                // 칸에서 위아래 코너가 좌우보다 길어 보인다.
                float2 p = (input.uv * 2.0 - 1.0) * float2(max(_Aspect, 1e-4), 1.0);
                float2 halfSize = float2(max(_Aspect, 1e-4), 1.0) - _Radius;

                // 테두리까지의 거리. 선 위에서 0이다.
                float edge = abs(RoundedBox(p, halfSize, _Radius)) - _Thickness;

                // 심지는 또렷하게, 바깥은 지수로 번진다. smoothstep 하나로 하면 번짐이
                // 딱 끊겨서 발광이 아니라 두꺼운 선으로 읽힌다.
                float core = 1.0 - saturate(edge / max(_Thickness, 1e-4));
                float halo = exp(-max(edge, 0.0) / max(_GlowWidth, 1e-4) * _GlowPower);

                // 조각들은 둘레를 따라 돈다. 위치를 시간에서 만들기 때문에 C#이
                // 매 프레임 값을 밀어 넣지 않아도 되고, 칸이 몇 개든 머티리얼 하나다.
                float t = PerimeterCoord(p, halfSize);
                float lead = _Offset + _Time.y * _Speed;

                int count = (int)round(clamp(_Count, 1.0, 4.0));
                float ride = 0.0;

                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    if (k >= count) break;

                    // 조각들은 둘레에 고르게 나눠 선다.
                    float head = lead + (float)k / (float)count;

                    // 머리에서 뒤로 얼마나 떨어져 있는가. frac이 한 바퀴를 이어 준다.
                    float behind = frac(head - t);

                    // 머리는 또렷한 토막, 그 뒤로 지수로 옅어지는 꼬리.
                    float body = smoothstep(_HeadLength, 0.0, behind);
                    float tail = exp(-behind / max(_TrailLength, 1e-4));

                    ride = max(ride, saturate(body + tail));
                }

                // 테두리 전체 모드. 조각과 섞이지 않도록 큰 쪽을 쓴다 — 더하면 조각이
                // 지나가는 자리만 밝아져 정지 테두리에 얼룩이 생긴다.
                ride = max(ride, saturate(_Ring));

                float glow = (halo + core * _CoreBoost) * ride;

                half4 tint = half4(_Color.rgb * input.color.rgb, 1.0);
                return half4(tint.rgb * glow, glow * _Color.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
