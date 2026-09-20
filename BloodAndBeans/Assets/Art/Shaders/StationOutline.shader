// 상호작용 대상의 테두리 (기획서 5.7.4). `TargetOutline`이 대상마다 두 벌을 세운다.
//
// **스텐실로 본체 실루엣을 빼는 방식이다.** 마스크 벌이 원래 크기로 스텐실 비트를 찍고,
// 테두리 벌이 살짝 키운 같은 메시를 그 비트가 없는 자리에만 그린다. 남는 것이 테두리다.
//
// 고전적인 역헐(앞면 컬링)을 쓰지 않는 이유는 이 프로젝트의 키트 모델 때문이다. 캐비닛
// 본체처럼 뒷면·바닥이 없는 단면 메시는 실루엣 바깥에 그릴 뒷면이 아예 없어서 테두리가
// 한쪽에만 나거나 통째로 사라진다. 스텐실은 앞면만으로 성립하므로 그 차이를 타지 않는다.
//
// 한 셰이더에 머티리얼 둘(`StationOutlineMask`, `StationOutline`)이 붙는다. 렌더 상태를
// 프로퍼티로 뽑아 둔 것이 그래서다 — 마스크는 색을 쓰지 않고 비트를 쓰고, 테두리는
// 반대다. 셰이더를 둘로 가르면 같은 정점 코드가 두 벌이 된다.
Shader "BB/StationOutline"
{
    Properties
    {
        [HDR] _BaseColor("색", Color) = (1.5, 1.5, 1.75, 1)

        // 32번 비트 하나만 쓴다. URP의 데칼·기본 패스와 겹치지 않게 높은 비트를 고른 것이다.
        _StencilRef("스텐실 Ref", Float) = 32
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("스텐실 비교", Float) = 6   // NotEqual
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp("스텐실 연산", Float) = 0           // Keep
        _ColorWrite("색 기록 마스크", Float) = 15                                              // RGBA
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "StationOutline"
            Tags { "LightMode" = "UniversalForward" }

            Stencil
            {
                Ref [_StencilRef]
                ReadMask 32
                WriteMask 32
                Comp [_StencilComp]
                Pass [_StencilOp]
            }

            ColorMask [_ColorWrite]
            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _StencilRef;
                float _StencilComp;
                float _StencilOp;
                float _ColorWrite;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(_BaseColor.rgb, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
