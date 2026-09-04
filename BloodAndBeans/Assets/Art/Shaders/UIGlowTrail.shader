// 희귀 재료가 가방으로 날아갈 때 남기는 발광 꼬리.
//
// `TrailRenderer`도, 사본을 잘게 떨구는 방식도 쓰지 않는다. 전자는 월드 렌더러라
// Screen Space - Overlay 캔버스에서 그려지지 않고, 후자는 비행 한 번에 GameObject가
// 열댓 개 생겼다 사라진다. 여기서는 출발점과 머리를 잇는 쿼드 **한 장**을 늘이고,
// 꼬리 모양은 전부 이 셰이더가 UV로 그린다.
//
// uv.x = 0이 꼬리 끝, 1이 머리다. uv.y는 두께 방향이다.
// 번짐을 직접 그리는 이유는 `UIGlowFrame`과 같다 — Overlay에는 블룸이 걸리지 않는다.
Shader "BB/UI Glow Trail"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _HeadBias ("머리 쏠림", Range(0.5, 6)) = 2.2
        _Softness ("두께 번짐", Range(0.05, 1)) = 0.55
        _CoreWidth ("심지 두께", Range(0.01, 0.6)) = 0.12
        _CoreBoost ("심지 밝기", Range(0, 6)) = 2

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
                float  _HeadBias;
                float  _Softness;
                float  _CoreWidth;
                float  _CoreBoost;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 머리로 갈수록 진해진다. 선형으로 두면 꼬리 끝이 뭉툭하게 잘려 보인다.
                float along = pow(saturate(input.uv.x), _HeadBias);

                // 두께 방향. 가운데가 1, 가장자리가 0인 띠를 만들고 지수로 번지게 한다.
                float across = 1.0 - abs(input.uv.y * 2.0 - 1.0);
                float halo = pow(saturate(across), 1.0 / max(_Softness, 1e-4));

                // 가운데 심지. 꼬리에 선이 한 줄 살아 있어야 흐릿한 얼룩이 아니라
                // 지나간 자취로 읽힌다. 꼬리 끝에서는 심지도 같이 사라진다.
                float core = smoothstep(_CoreWidth, 0.0, abs(input.uv.y - 0.5));

                float glow = (halo + core * _CoreBoost) * along;

                half3 tint = _Color.rgb * input.color.rgb;
                return half4(tint * glow, glow * _Color.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
