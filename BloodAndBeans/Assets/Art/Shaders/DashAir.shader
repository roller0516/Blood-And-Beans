Shader "BB/Dash Air"
{
    Properties
    {
        [HDR] _Tint("공기 색",Color)=(1,1,1,1)
        _Opacity("강도",Range(0,3))=1.8
        _Brightness("발광 강도",Range(0,5))=2.6
        _FlowSpeed("흐르는 속도",Float)=7
        _EdgeNoiseScale("끝 노이즈 크기",Range(2,40))=22
        _EdgeNoiseStrength("끝 노이즈 강도",Range(0,1))=1
        _EdgeNoiseSpeed("끝 노이즈 흐름",Float)=1.5
        _CausticTex("일렁이는 표면 결",2D)="white"{}
        _WaveMask("찢어지는 꼬리 마스크",2D)="white"{}
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
            TEXTURE2D(_CausticTex);SAMPLER(sampler_CausticTex);
            TEXTURE2D(_WaveMask);SAMPLER(sampler_WaveMask);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Opacity;
                float _Brightness;
                float _FlowSpeed;
                float _EdgeNoiseScale;
                float _EdgeNoiseStrength;
                float _EdgeNoiseSpeed;
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
            float EdgeHash(float2 p)
            {
                p=frac(p*0.1031);
                p+=dot(p,p.yx+33.33);
                return frac((p.x+p.y)*p.x);
            }
            float EdgeNoise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(EdgeHash(cell),EdgeHash(cell+float2(1,0)),f.x),
                    lerp(EdgeHash(cell+float2(0,1)),EdgeHash(cell+1),f.x),f.y);
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal=normalize(input.normalWS);
                float3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                float rim=pow(1-saturate(abs(dot(normal,view))),2.5);
                float travel=input.uv.y*5-_Time.y*_FlowSpeed;
                float warp=EdgeNoise(float2(input.uv.x*8,travel*0.45))-0.5;
                float2 noiseUV=float2(input.uv.x*_EdgeNoiseScale,travel*_EdgeNoiseSpeed*0.45);
                float noise=EdgeNoise(noiseUV)*0.7+EdgeNoise(noiseUV*2.1+13.7)*0.3;
                // 긴 세로 결을 남기고 뒤로 갈수록 노이즈에 찢겨 사라진다.
                float threshold=lerp(0.12,0.65,smoothstep(0.15,1,input.uv.y));
                float erosion=lerp(1,smoothstep(threshold,threshold+0.18,noise),_EdgeNoiseStrength);
                float threads=pow(saturate(0.5+0.5*sin((input.uv.x+warp*0.035)*175.9292)),13);
                float fine=pow(saturate(0.5+0.5*sin((input.uv.x-warp*0.018)*307.8761)),25);
                float head=1-smoothstep(0.06,0.32,input.uv.y);
                float tail=pow(saturate(1-input.uv.y),0.65);
                float caustic=SAMPLE_TEXTURE2D(_CausticTex,sampler_CausticTex,
                    float2(input.uv.x*3+warp*0.07,input.uv.y*0.16-_Time.y*_FlowSpeed*0.12)).r;
                float wave=SAMPLE_TEXTURE2D(_WaveMask,sampler_WaveMask,float2(input.uv.x+warp*0.025,1-input.uv.y)).r;
                float density=(caustic*1.1+threads*0.09+fine*0.04+rim*head*0.18+head*0.025)*wave*tail*erosion;
                half3 color=_Tint.rgb*_Brightness;
                half alpha=saturate(density*_Opacity*input.color.a);
                clip(alpha-0.001);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
