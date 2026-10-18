Shader "BB/Wash Water"
{
    Properties
    {
        _Tint("물 색", Color) = (0.26, 0.68, 0.72, 0.42)
        _Highlight("반사광", Color) = (0.82, 0.98, 1, 1)
        _FlowSpeed("흐르는 속도", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _Highlight;
                float _FlowSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                input.positionOS.xz*=1+0.07*sin(input.uv.y*9+_Time.y*_FlowSpeed*4);
                input.positionOS.xz*=1+0.07*sin(input.uv.y*9+_Time.y*_FlowSpeed*4);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv;
                output.color=input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                half edge=pow(1-saturate(abs(dot(normalize(input.normalWS),view))),2);
                half streak=pow(saturate(0.5+0.5*sin(input.uv.x*6.2831853+0.6)),12);
                half flow=0.5+0.5*sin(input.uv.y*26+_Time.y*_FlowSpeed*6.2831853);
                half highlight=saturate(edge*0.65+streak*0.55+pow(flow,8)*0.15);
                half3 color=lerp(_Tint.rgb,_Highlight.rgb,highlight);
                half alpha=saturate(_Tint.a+edge*0.2+streak*0.15)*input.color.a;
                return half4(color*input.color.rgb,alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
