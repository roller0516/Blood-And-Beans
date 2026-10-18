Shader "BB/Dash Air"
{
    Properties
    {
        _Tint("공기 색",Color)=(0.55,0.9,0.83,1)
        _Opacity("강도",Range(0,1))=0.65
        _DistortionPixels("굴절 픽셀",Float)=2.5
        _FlowSpeed("흐르는 속도",Float)=2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Opacity;
                float _DistortionPixels;
                float _FlowSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv;output.color=input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal=normalize(input.normalWS);
                float3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                float rim=pow(1-saturate(abs(dot(normal,view))),2.5);
                float back=1-smoothstep(0.67,1,input.uv.y);
                float filaments=pow(saturate(sin(input.uv.x*37.6991+sin(input.uv.y*5)*0.3)),28);
                float flow=pow(saturate(0.5+0.5*sin(input.uv.y*15-_Time.y*_FlowSpeed*6.2831853)),10);
                float2 uv=GetNormalizedScreenSpaceUV(input.positionCS);
                float2 bend=TransformWorldToViewDir(normal).xy*_DistortionPixels/_ScaledScreenParams.xy;
                half3 scene=SampleSceneColor(uv+bend*rim);
                half3 tint=lerp(_Tint.rgb,half3(0.94,1,0.96),rim);
                half3 color=lerp(scene,tint,saturate(0.12+rim*0.68+filaments*0.15));
                half alpha=(0.055+rim*0.72+filaments*flow*0.22)*back*_Opacity*input.color.a;
                clip(alpha-0.001);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
