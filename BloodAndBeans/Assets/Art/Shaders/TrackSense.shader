Shader "BB/TrackSense"
{
    Properties
    {
        [HDR] _ScentColor("파동 색", Color) = (1.4, 0.8, 0.2, 1)
        _Distortion("왜곡", Range(0, 0.02)) = 0.0025
        _Glow("가장자리 빛", Range(0, 1)) = 0.13
        _Frequency("파동 밀도", Float) = 38
        _Speed("파동 속도", Float) = 12
        _EdgeStart("가장자리 시작", Range(0, 1)) = 0.35
        [HideInInspector] _PulseTime("진행", Float) = 0
        [HideInInspector] _PulseStrength("세기", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _ScentColor;
                float _Distortion, _Glow, _Frequency, _Speed, _EdgeStart, _PulseTime, _PulseStrength;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 centered = uv * 2 - 1;
                float radius = length(centered);
                float edge = smoothstep(_EdgeStart, 1, max(abs(centered.x), abs(centered.y)));
                float wave = sin(radius * _Frequency - _PulseTime * _Speed);
                float2 direction = centered / max(radius, 0.001);
                float2 offset = direction * wave * edge * _Distortion * _PulseStrength;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv + offset));
                float ribbon = pow(saturate(wave * 0.5 + 0.5), 6);
                color.rgb += _ScentColor.rgb * ribbon * edge * _Glow * _PulseStrength;
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
