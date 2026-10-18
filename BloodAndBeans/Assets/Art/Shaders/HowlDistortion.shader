Shader "BB/HowlDistortion"
{
    Properties
    {
        _DistortionPixels("굴절 강도 (화면 픽셀)", Range(0, 32)) = 16
        _RingRadius("파동 반경", Range(0.3, 0.9)) = 0.78
        _RingWidth("파동 폭", Range(0.04, 0.3)) = 0.13
        _RimIntensity("가장자리 빛", Range(0, 0.1)) = 0.008
        _RimColor("가장자리 색", Color) = (0.65, 0.85, 0.78, 1)
    }
    SubShader
    {
        // 불투명 배경을 굴절시킨 뒤 일반 투명 파티클과 UI가 그 위에 그려진다.
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-100" "RenderType" = "Transparent" }
        Pass
        {
            Name "HowlRefraction"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _RimColor;
                float _DistortionPixels;
                float _RingRadius;
                float _RingWidth;
                float _RimIntensity;
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
                float2 centered = input.uv * 2.0 - 1.0;
                float radius = length(centered);
                float band = (radius - _RingRadius) / max(_RingWidth, 0.001);
                float envelope = exp(-band * band * 3.0);
                half alpha = saturate(envelope * input.color.a);
                // 화면의 기울기로 굴절 방향을 구해 빌보드 회전의 영향을 없앤다.
                float2 gradient = float2(ddx(radius), ddy(radius));
                float2 direction = gradient / max(length(gradient), 0.00001);
                clip(alpha - 0.002);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float displacement = -sin(band * 3.141593) * envelope * _DistortionPixels * input.color.a;
                float2 offset = direction * displacement / GetScaledScreenParams().xy;
                half3 refracted = SampleSceneColor(saturate(screenUV + offset));
                half3 rim = _RimColor.rgb * _RimIntensity * envelope * envelope;
                return half4(refracted + rim, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
