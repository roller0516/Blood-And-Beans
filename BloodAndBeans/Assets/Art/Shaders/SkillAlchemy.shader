Shader "BB/SkillAlchemy"
{
    Properties
    {
        [Enum(Liquid,0,Bubble,1,Seal,2)] _Kind("모양", Float) = 0
        _Tint("바탕 색", Color) = (0.16, 0.035, 0.3, 0.85)
        [HDR] _Accent("강조 색", Color) = (0.65, 0.28, 0.85, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _Accent;
                float _Kind;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
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
                float2 p = input.uv * 2 - 1;
                float r = length(p);
                float angle = atan2(p.y, p.x);
                float alpha;
                half3 color;
                if (_Kind < 0.5)
                {
                    float contour = 0.86 + 0.035 * sin(angle * 7) + 0.045 * sin(angle * 3 + 1.2);
                    float body = 1 - smoothstep(contour - 0.03, contour + 0.015, r);
                    float rim = exp(-pow((r - contour + 0.04) / 0.035, 2));
                    float swirl = pow(saturate(0.5 + 0.5 * sin(r * 25 + angle * 3)), 5);
                    color = lerp(_Tint.rgb, _Accent.rgb, 0.12 + swirl * 0.22) + _Accent.rgb * rim * 0.8;
                    alpha = body * _Tint.a;
                }
                else if (_Kind < 1.5)
                {
                    float body = 1 - smoothstep(0.88, 0.98, r);
                    float edge = pow(saturate(r), 6);
                    float glint = exp(-dot((p - float2(-0.3, 0.35)) * float2(5, 8), (p - float2(-0.3, 0.35)) * float2(5, 8)));
                    color = lerp(_Tint.rgb, _Accent.rgb, edge) + glint * 0.8;
                    alpha = body * saturate(0.09 + edge * 0.85 + glint * 0.6) * _Tint.a;
                }
                else
                {
                    float ring = 1 - smoothstep(0.018, 0.04, abs(r - 0.76));
                    float inner = (1 - smoothstep(0.01, 0.025, abs(r - 0.62))) * 0.35;
                    float glyph = pow(saturate(cos(angle * 8)), 22) * smoothstep(0.59, 0.66, r) * (1 - smoothstep(0.81, 0.85, r));
                    alpha = saturate(ring + inner + glyph) * _Tint.a;
                    color = _Accent.rgb;
                }
                alpha *= input.color.a;
                clip(alpha - 0.001);
                return half4(color * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
