// 상호작용 대상의 테두리 (기획서 5.7.4). `OutlineFeature`가 대상 렌더러를 이 셰이더로 두 번 더 그린다.
//
// **스텐실로 본체 실루엣을 빼는 방식이다.** 마스크 패스가 원래 크기로 스텐실 비트를 찍고,
// 테두리 패스가 `_OutlineScale`만큼 키운 같은 메시를 그 비트가 없는 자리에만 그린다.
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
        // _BaseColor가 아니다 — 카페는 팀 색을 MaterialPropertyBlock의 _BaseColor로 입히는데(TeamColors),
        // 대체 머티리얼로 그려도 그 블록이 그대로 적용돼 테두리까지 팀 색에 물든다.
        [HDR] _OutlineColor("색", Color) = (1.5, 1.5, 1.75, 1)

        // 렌더러 바운드 중심 기준 확대율. 마스크는 1이다.
        _OutlineScale("확대율", Range(1, 1.3)) = 1

        // 비트 하나만 쓴다(읽기·쓰기 마스크도 이 값). 설비 테두리는 32, 다음에 집힐 아이템 테두리는 64.
        // URP의 데칼·기본 패스와 겹치지 않게 높은 비트를 고른 것이다.
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
                ReadMask [_StencilRef]
                WriteMask [_StencilRef]
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
                float4 _OutlineColor;
                float _OutlineScale;
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

                // 피벗이 아니라 바운드 중심으로 키운다 — 피벗이 쏠린 납작한 부품은 테두리가 한쪽에만 난다.
                // 확대율이 1이면 위치가 그대로라 마스크가 본체와 같은 깊이에 정확히 겹친다.
                float3 centerWS = (unity_RendererBounds_Min.xyz + unity_RendererBounds_Max.xyz) * 0.5;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += (positionWS - centerWS) * (_OutlineScale - 1);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(_OutlineColor.rgb, _OutlineColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
