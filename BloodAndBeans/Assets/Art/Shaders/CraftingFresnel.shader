Shader "BB/CraftingFresnel"
{
    Properties
    {
        [HDR] _BaseColor("빛 색", Color) = (1.6, 1.6, 1.6, 1)
        _Power("프레넬 곡선", Range(0.5, 8)) = 2
        _Opacity("불투명도", Range(0, 1)) = 0.85
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Power, _Opacity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; half4 color:COLOR; half fog:TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float rim = pow(1 - saturate(dot(normalize(i.normalWS), GetWorldSpaceNormalizeViewDir(i.positionWS))), _Power);
                half4 c = _BaseColor * i.color;
                c.a *= rim * _Opacity;
                c.rgb = MixFogColor(c.rgb, half3(0,0,0), i.fog);
                return c;
            }
            ENDHLSL
        }
    }
}

