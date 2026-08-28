// 안개 평면 셰이더. 걷힘 여부만 담은 셀 격자 마스크(R8, 셀당 텍셀 1개)를 받아
// 부드러운 가장자리를 GPU에서 만든다.
//
// CPU에서 텍셀마다 원을 찍고 박스 블러를 돌리던 것을 대신한다. 그쪽은 한 번 칠할 때마다
// 720x720을 훑느라 100ms 넘게 잡아먹었다. 여기서는 CPU가 240x240 마스크에 0/255만 쓰고,
// 둥글게 만드는 일은 바이리니어 필터와 아래 3x3 탭이 한다.
Shader "BloodAndBeans/FogOfWar"
{
    Properties
    {
        _MainTex ("걷힘 마스크 (R8)", 2D) = "black" {}
        _FogColor ("안개 색", Color) = (0.05, 0.06, 0.10, 0.96)
        _Softness ("가장자리 부드러움", Range(0.01, 0.5)) = 0.35
        _BlurLevel ("뭉개는 정도 (밉 레벨)", Range(0, 4)) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "FogOfWar"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _FogColor;
                float _Softness;
                float _BlurLevel;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float Mask(float2 uv, float lod)
            {
                return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, lod).r;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                // 마스크는 셀당 텍셀 하나뿐이라 바이리니어만으로는 경계가 사각형으로 보인다.
                // 넓은 평균은 밉맵 피라미드가 만들고, 그 밉의 텍셀 간격으로 찍는 3x3
                // 가우시안 탭이 박스 필터가 남기는 사각 블록을 지운다.
                //
                // 간격은 그 밉 텍셀의 절반이다. 탭이 서로 겹쳐야 박스가 텐트가 된다.
                // 온전히 한 텍셀씩 띄우면 탭끼리 겹치는 곳이 없어 박스 필터의 사각 발자국이
                // 그대로 격자로 드러난다 (밉 2.5 부근에서 눈에 띄게 나왔다). 밉 0을 2.5텍셀
                // 간격으로 찍던 그 전 버전은 사이 텍셀을 통째로 건너뛰어 별 모양이 됐다.
                float2 texel = _MainTex_TexelSize.xy * exp2(_BlurLevel) * 0.5;

                float m = Mask(input.uv, _BlurLevel) * 4.0;
                m += Mask(input.uv + float2(-texel.x, 0), _BlurLevel) * 2.0;
                m += Mask(input.uv + float2( texel.x, 0), _BlurLevel) * 2.0;
                m += Mask(input.uv + float2(0, -texel.y), _BlurLevel) * 2.0;
                m += Mask(input.uv + float2(0,  texel.y), _BlurLevel) * 2.0;
                m += Mask(input.uv + float2(-texel.x, -texel.y), _BlurLevel);
                m += Mask(input.uv + float2( texel.x, -texel.y), _BlurLevel);
                m += Mask(input.uv + float2(-texel.x,  texel.y), _BlurLevel);
                m += Mask(input.uv + float2( texel.x,  texel.y), _BlurLevel);
                m *= 0.0625;   // 1/16

                // 하드 클램프가 아니라 smoothstep이다. 딱 잘라내면 셀 격자의 다각형 윤곽이
                // 그대로 드러난다.
                float revealed = smoothstep(0.5 - _Softness, 0.5 + _Softness, m);
                return half4(_FogColor.rgb, _FogColor.a * (1.0 - revealed));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
