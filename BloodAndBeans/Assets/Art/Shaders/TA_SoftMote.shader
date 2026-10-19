Shader "BloodAndBean/VFX/SoftMote"
{
 Properties { [HDR] _Tint("Glow Color",Color)=(0.25,1,1.5,1) }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
 Pass {
 Blend SrcAlpha One
 ZWrite Off
 Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _Tint;
 CBUFFER_END
 struct A {float4 p:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR;};
 struct V {float4 p:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR;};
 V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.color=i.color;return o;}
 half4 frag(V i):SV_Target{float r=length(i.uv*2-1);float a=pow(saturate(1-r),3);return half4(_Tint.rgb*i.color.rgb,a*i.color.a*_Tint.a);}
 ENDHLSL
 }
 }
}
