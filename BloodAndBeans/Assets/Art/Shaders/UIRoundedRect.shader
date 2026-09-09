// uGUI 이미지의 모서리를 둥글게 깎는다. 9-slice 스프라이트를 깔던 방식을 대신한다.
//
// 9-slice 방식은 `Image.sprite` 자리를 라운드용 스프라이트가 차지해 버려서, 자기 그림이
// 있는 이미지(초상·썸네일·아이콘)는 애초에 둥글게 만들 수 없었다. 여기서는 그림을 그대로
// 그리고 알파만 깎으므로 무엇이 깔려 있든 상관하지 않는다.
//
// **반지름은 머티리얼이 아니라 정점이 나른다.** uGUI는 `MaterialPropertyBlock`을 받지
// 않아서(CanvasRenderer가 배칭한다) 칸마다 다른 값을 주려면 머티리얼을 복제해야 하고,
// 그러면 칸 수만큼 머티리얼이 생기고 배칭이 깨진다. `UIGlowFrame`이 밝기를 정점 색으로
// 나르는 것과 같은 이유이고, 그래서 이 셰이더를 쓰는 이미지는 반지름과 크기가 제각각이어도
// **머티리얼 하나를 공유한다**.
//
// 정점이 나르는 것은 둘 다 "반지름을 1로 놓은 좌표계"의 값이다.
//   TEXCOORD1 : 사각형 중심에서 이 정점까지의 거리 (정점마다 다르다)
//   TEXCOORD2 : 중심에서 변까지의 거리, 곧 SDF의 half size (네 정점 모두 같다)
// 나눗셈을 채우는 쪽에서 한 번에 끝내려는 것이다. 화면 좌표나 `uv`로 재지 않는 이유는
// 스프라이트 아틀라스를 타면 `uv`가 0~1이 아니고, 9-slice·타일이면 정점이 넷보다 많기
// 때문이다 — 정점 위치로 재면 둘 다 상관없다.
//
// 채우는 쪽은 `UIRoundImage.ModifyMesh`이고, 캔버스의 `additionalShaderChannels`에
// TexCoord1·TexCoord2를 켜 주는 것도 그쪽이다 — 켜지 않으면 두 채널이 통째로 버려진다.
Shader "BB/UI Rounded Rect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // uGUI 마스크(Mask/RectMask2D)가 쓰는 값. UI 셰이더가 이걸 갖고 있지 않으면
        // 마스크 안에서 잘리지 않고 삐져나온다. `StencilMaterial`이 이 프로퍼티들을 채운
        // 사본을 만들어 쓰므로, 이름이 유니티 기본 UI 셰이더와 같아야 한다.
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
            #include "UIRounded.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float2 local      : TEXCOORD1;   // 중심 기준 위치 (반지름 = 1 단위)
                float2 halfSize   : TEXCOORD2;   // 중심에서 변까지. 음수면 깎지 않는다
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float2 local      : TEXCOORD1;
                float2 halfSize   : TEXCOORD2;
                // RectMask2D가 자르는 기준이 캔버스 좌표계라 들어온 정점을 그대로 넘긴다.
                float4 clipPosition : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _ClipRect;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.clipPosition = input.positionOS;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.local = input.local;
                output.halfSize = input.halfSize;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                // 반지름이 0이거나 채널이 꺼져 있으면 채우는 쪽이 음수를 실어 보낸다.
                // 각진 채로 두는 것이 맞고, 채널이 죽었을 때도 그림이 사라지지 않는다.
                if (input.halfSize.x > 0.0 && input.halfSize.y > 0.0)
                {
                    // 좌표를 반지름으로 나눠 두었으므로 반지름은 늘 1이다.
                    float distance = BB_RoundedBox(input.local, input.halfSize, 1.0);

                    // 화면 픽셀 하나가 이 좌표계에서 얼마인지를 미분으로 얻는다. 캔버스
                    // 배율이나 반지름이 바뀌어도 테두리가 늘 한 픽셀만 부드럽다.
                    float pixel = fwidth(distance);
                    color.a *= saturate(0.5 - distance / max(pixel, 1e-5));
                }

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
