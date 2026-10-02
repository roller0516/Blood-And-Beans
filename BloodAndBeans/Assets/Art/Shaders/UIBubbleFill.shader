// 말풍선 스프라이트를 아래에서부터 채움 색으로 물들인다 (기획서 5.7.2 — 손님 인내심).
//
// 채움선 아래 픽셀에 색을 곱한다. 흰 안쪽은 채움 색이 되고 갈색 테두리는 어두운 채로 남아
// 말풍선 모양이 유지된다. 따로 채움 이미지를 깔지 않으므로 아이콘 뒤에 그대로 깔린다.
//
// 값은 `UIRoundedRect`와 같은 이유로 머티리얼이 아니라 정점이 나른다 — 손님마다 채움이
// 다른데 머티리얼을 복제하면 배칭이 깨진다. 채우는 쪽은 `UIBubbleFill.ModifyMesh`다.
//   TEXCOORD1 : x = 스프라이트 안 높이 비율(바닥 0 · 꼭대기 1), y = 채움선 높이,
//               z = 가로 비율(왼쪽 0 · 오른쪽 1), w = 출렁임 위상(말풍선마다 다르다)
//   TEXCOORD2 : 채움 색. 채널이 꺼져 있으면 0이 와서 아무것도 칠하지 않는다
//
// 출렁임은 `_Time`으로 셰이더가 혼자 움직인다. 값이 그대로면 메시를 다시 만들지 않는다.
Shader "BB/UI Bubble Fill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // 출렁임. 진폭은 말풍선 높이 대비 비율, 파장 수는 가로 한 폭에 들어가는 물결 수다.
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.1)) = 0.025
        _WaveCount ("Wave Count", Range(0, 4)) = 1.5
        _WaveSpeed ("Wave Speed", Range(0, 10)) = 3

        // uGUI 마스크가 채우는 값. 이름이 유니티 기본 UI 셰이더와 같아야 한다 (`UIRoundedRect` 참고).
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 fill       : TEXCOORD1;
                float4 fillColor  : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 fill       : TEXCOORD1;
                float4 fillColor  : TEXCOORD2;
                float4 clipPosition : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _ClipRect;
                float _WaveAmplitude;
                float _WaveCount;
                float _WaveSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.clipPosition = input.positionOS;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fill = input.fill;
                output.fillColor = input.fillColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                // 물결 둘을 겹쳐 규칙적인 톱니처럼 보이지 않게 한다. 채움이 0이면 흔들지 않는다 —
                // 선이 바닥 밑으로 내려가 있어야 꼬리 끝이 물들지 않는다.
                float angle = input.fill.z * _WaveCount * 6.2831853 + input.fill.w;
                float t = _Time.y * _WaveSpeed;
                float wave = (sin(angle + t) * 0.7 + sin(angle * 1.9 - t * 1.3) * 0.3) * _WaveAmplitude;
                float level = input.fill.y > 0.0 ? input.fill.y + wave : 0.0;

                // 채움선을 화면 픽셀 하나 폭으로만 부드럽게 한다. 물결의 기울기까지 미분에 들어가
                // 크기나 진폭이 바뀌어도 선이 계단지지 않는다.
                float distance = level - input.fill.x;
                float below = saturate(distance / max(fwidth(distance), 1e-5) + 0.5);
                color.rgb = lerp(color.rgb, color.rgb * input.fillColor.rgb, below * input.fillColor.a);

                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy, input.clipPosition.xy)
                              * step(input.clipPosition.xy, _ClipRect.zw);
                color.a *= inside.x * inside.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDHLSL
        }
    }

    Fallback "UI/Default"
}
