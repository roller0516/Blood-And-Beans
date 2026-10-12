Shader "BB/ScentRoute"
{
    Properties
    {
        [HDR] _ScentColor("붉은 냄새 색", Color) = (1.8, 0.055, 0.025, 0.75)
        _FlowSpeed("흐름 속도", Float) = 3
        _NoiseScale("연기 굴곡", Float) = 1.5
        _EndFade("양 끝 페이드 거리", Float) = 0.6
        [HideInInspector] _RouteLength("경로 길이", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _ScentColor;
                float _FlowSpeed, _NoiseScale, _EndFade, _RouteLength;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float distance = input.uv.x * _RouteLength;
                float flow = distance * _NoiseScale - _Time.y * _FlowSpeed;
                float curl = 0.1 * sin(flow * 0.7) + 0.06 * sin(flow * 1.9 + 2);
                float crossSection = saturate(1 - abs((input.uv.y - 0.5 - curl) * 2));
                float smoke = pow(crossSection, 2) * (0.4 + 0.6 * (sin(flow) * 0.5 + 0.5));
                float core = pow(crossSection, 10) * pow(saturate(sin(flow * 2) * 0.5 + 0.5), 3);
                float endFade = smoothstep(0, max(_EndFade, 0.001), min(distance, _RouteLength - distance));
                half alpha = saturate((smoke * 0.65 + core * 0.35) * endFade * _ScentColor.a * input.color.a);
                return half4(_ScentColor.rgb * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
