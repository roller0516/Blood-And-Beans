Shader "BloodAndBean/VFX/Island Cloud"
{
 Properties {
  _LightColor("Sunlit Cloud",Color)=(1,0.8,0.66,1)
  _ShadeColor("Cloud Shade",Color)=(0.48,0.39,0.55,1)
  _Opacity("Opacity",Range(0,1))=0.38
  _Seed("Pattern Seed",Float)=0
 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
 Pass {
 Blend SrcAlpha OneMinusSrcAlpha
 ZWrite Off
 Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _LightColor,_ShadeColor;
 float _Opacity,_Seed;
 CBUFFER_END
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
 V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
 half4 frag(V i):SV_Target{
 float2 uv=i.uv;float2 p=uv*float2(6,3)+_Seed+float2(_Time.y*0.008,0);
 float n=noise(p)*0.65+noise(p*2.1)*0.25+noise(p*4.2)*0.1;
 float edge=1-smoothstep(0.35,1,length((uv-0.5)*2));
 float a=edge*smoothstep(0.22,0.65,n)*_Opacity;
 return half4(lerp(_ShadeColor.rgb,_LightColor.rgb,saturate(uv.y*0.65+n*0.35)),a);
 }
 ENDHLSL
 }
 }
}
