Shader "BB/ReturnMagic"
{
    Properties
    {
        _MainTex("빛 마스크", 2D) = "white" {}
        [HDR] _BaseColor("빛 색", Color) = (1.5, 0.55, 2.6, 1)
        _Opacity("불투명도", Range(0,1)) = 1
        _Pulse("맥동", Range(0,1)) = 0.15
        _PulseSpeed("맥동 속도", Float) = 2
        _Rim("잔상 윤곽", Range(0,1)) = 0
        _Flow("안개 흐름", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Opacity, _Pulse, _PulseSpeed, _Rim, _Flow;
            CBUFFER_END
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; half4 color:COLOR; half fog:TEXCOORD2; float2 uv:TEXCOORD3; };
            float hash(float3 p) { return frac(sin(dot(p, float3(127.1,311.7,74.7))) * 43758.5453); }
            float noise(float3 p)
            {
                float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+float3(1,1,1)),f.x),f.y),f.z);
            }
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = lerp(v.color, half4(1,1,1,1), _Rim);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float pulse = 1 - _Pulse * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed + i.positionWS.x * 2));
                float rim = pow(1 - abs(dot(normalize(i.normalWS), GetWorldSpaceNormalizeViewDir(i.positionWS))), 2);
                float shimmer = 0.8 + 0.2 * sin(i.positionWS.y * 38 - _Time.y * 6);
                half4 c = _BaseColor * i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                c.a *= i.color.a; // 번짐 띠를 제곱으로 줄여 겹치는 곳이 하얗게 뜨지 않게 한다.
                c.a *= _Opacity * pulse * lerp(1, (0.15 + rim * 0.85) * shimmer, _Rim);
                float flow = noise(float3(i.uv.x*14-_Time.y*.45,i.uv.y*5,_Time.y*.17));
                c.a *= lerp(1,smoothstep(.22,.72,flow),_Flow);
                // 잔상은 전체 투명도만 낮추지 않고 작은 빛 조각으로 깎아낸다.
                float dissolve = noise(i.positionWS*5+float3(0,-_Time.y*.35,0));
                float edge = dissolve-(1-_Opacity)*1.1;
                c.a *= lerp(1,smoothstep(0,.075,edge),_Rim);
                c.rgb += _BaseColor.rgb * (1-smoothstep(.02,.12,abs(edge))) * _Rim * .6;
                c.rgb = MixFogColor(c.rgb, half3(0,0,0), i.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
