Shader "BB/UI Title Motion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Artwork", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Period ("Loop Seconds", Range(1,30)) = 8
        _GhostAmount ("Ghost Motion", Range(0,2)) = 1
        _CloudAmount ("Cloud Motion", Range(0,2)) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 clipPosition : TEXCOORD1; };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _ClipRect;
                float _Period, _GhostAmount, _CloudAmount;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.clipPosition = input.positionOS.xy;
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }
            float Oval(float2 p, float2 center, float2 radius)
            {
                return 1 - smoothstep(0.55, 1, length((p - center) / radius));
            }
            half4 frag(Varyings input) : SV_Target
            {
                // ponytail: 이 타이틀 그림의 영역에 맞춘 UV 연출. 그림을 교체하면 영역도 다시 맞춘다.
                float2 uv = float2(input.uv.x, 1 - input.uv.y);
                float2 q = uv;
                float phase = _Time.y * 6.2831853 / max(_Period, 0.1);
                float ghost = Oval(uv, float2(0.31,0.17), float2(0.155,0.185)) * (1-smoothstep(0.265,0.305,uv.y));
                q.y -= ghost * _GhostAmount * (0.010*sin(phase) + 0.0018*sin(2*phase));
                q.x -= ghost * _GhostAmount * 0.004*sin(phase+0.6);
                float steam = Oval(uv,float2(0.45,0.235),float2(0.085,0.095)) * (1-smoothstep(0.29,0.32,uv.y));
                q.x += steam * _GhostAmount * 0.0035*sin(2*phase+uv.y*28);
                float sides = (1-smoothstep(0.15,0.225,uv.x)) + smoothstep(0.825,0.9,uv.x);
                float haze = max(saturate(sides)*smoothstep(0.06,0.22,uv.y),smoothstep(0.85,0.96,uv.y)*0.65);
                q.x += haze * _CloudAmount * (0.009*sin(phase+uv.y*8) + 0.003*sin(2*phase-uv.y*17));
                q.y += haze * _CloudAmount * 0.005*sin(phase+uv.x*13);
                half4 color = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,saturate(float2(q.x,1-q.y))) * input.color;
                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy,input.clipPosition) * step(input.clipPosition,_ClipRect.zw);
                color.a *= inside.x * inside.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a-0.001);
                #endif
                return color;
            }
            ENDHLSL
        }
    }
    Fallback "UI/Default"
}
