Shader "PainterlyCafe/IllustratedOpaque"
{
 Properties
 {
  [MainTexture] _BaseMap("Base Map (optional)",2D)="white"{}
  [MainColor] _BaseColor("Base Color",Color)=(0.65,0.72,0.36,1)
  _ShadowColor("Shadow Tint",Color)=(0.24,0.20,0.46,1)
  _MidColor("Midtone Tint",Color)=(0.72,0.76,0.62,1)
  _LightColor("Light Tint",Color)=(1,0.94,0.65,1)
  _ShadowHue("Shadow Color Replacement",Range(0,1))=0.35
  _ThresholdLow("Shadow To Mid",Range(0,1))=0.28
  _ThresholdHigh("Mid To Light",Range(0,1))=0.7
  _BandSoftness("Band Softness",Range(0.005,0.3))=0.07
  _BrushScale("Brush UV Scale",Range(1,100))=18
  _BrushStrength("Brush Color Strength",Range(0,0.25))=0.06
  _BandBreakup("Brush Band Breakup",Range(0,0.3))=0.05
  _FacetStrength("Triangle Faceting",Range(0,1))=0
  _Fill("Painted Fill",Range(0,1))=0.18
  _MainGain("Main Light Gain",Range(0,2))=1
  _AdditionalGain("Additional Light Gain",Range(0,2))=0.55
  [HDR] _EmissionColor("Emission Color",Color)=(0,0,0,1)
  _EmissionMask("Emission Mask R (optional)",2D)="white"{}
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;
   half4 _BaseColor, _ShadowColor, _MidColor, _LightColor, _EmissionColor;
   half _ShadowHue,_ThresholdLow,_ThresholdHigh,_BandSoftness;
   float _BrushScale;
   half _BrushStrength,_BandBreakup,_FacetStrength,_Fill,_MainGain,_AdditionalGain;
  CBUFFER_END
  TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
  TEXTURE2D(_EmissionMask); SAMPLER(sampler_EmissionMask);
  struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
  struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half fog:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
  Varyings Vert(Attributes v)
  {
   Varyings o=(Varyings)0;
   UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
   VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
   o.positionCS=p.positionCS; o.positionWS=p.positionWS;
   o.normalWS=TransformObjectToWorldNormal(v.normalOS);
   o.uv=TRANSFORM_TEX(v.uv,_BaseMap); o.fog=ComputeFogFactor(p.positionCS.z); return o;
  }
  ENDHLSL
  Pass
  {
   Name "PainterlyForward"
   Tags { "LightMode"="UniversalForwardOnly" }
   Cull Back ZWrite On ZTest LEqual
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
   float Noise(float2 p)
   {
    float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
   }
   half3 Extra(Light l,half3 n,half3 baseColor)
   {
    half nd=saturate(dot(n,l.direction));
    half diffuse=smoothstep(0.05h,0.85h,nd);
    return baseColor*l.color*(l.distanceAttenuation*l.shadowAttenuation)*diffuse*_AdditionalGain;
   }
   half4 Frag(Varyings i):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half3 n=normalize(i.normalWS);
    float3 fn=cross(ddy(i.positionWS),ddx(i.positionWS));
    fn*=rsqrt(max(dot(fn,fn),1e-12)); if(dot(fn,n)<0)fn=-fn;
    n=normalize(lerp(n,fn,_FacetStrength));
    float brush=Noise(i.uv*_BrushScale*float2(1,2.7))*0.7+Noise(i.uv*_BrushScale*2.1)*0.3;
    half3 baseColor=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
    baseColor*=1+(brush-.5)*2*_BrushStrength;
    float4 sc;
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
     sc=ComputeScreenPos(i.positionCS);
     // SV_POSITION is raster position in fragment; use normalized screen UV instead.
     sc=float4(GetNormalizedScreenSpaceUV(i.positionCS),0,1);
    #else
     sc=TransformWorldToShadowCoord(i.positionWS);
    #endif
    Light main=GetMainLight(sc);
    half mainEnergy=saturate(dot(main.color,half3(.2126,.7152,.0722))*_MainGain);
    half t=saturate(dot(n,main.direction));
    t=saturate(t*main.shadowAttenuation*main.distanceAttenuation*mainEnergy+(brush-.5)*_BandBreakup);
    half low=smoothstep(_ThresholdLow-_BandSoftness,_ThresholdLow+_BandSoftness,t);
    half high=smoothstep(_ThresholdHigh-_BandSoftness,_ThresholdHigh+_BandSoftness,t);
    half3 shadow=lerp(baseColor*_ShadowColor.rgb,_ShadowColor.rgb,_ShadowHue);
    half3 mid=baseColor*_MidColor.rgb;
    half3 lit=baseColor*_LightColor.rgb*lerp(half3(1,1,1),main.color*_MainGain,0.35h);
    half3 c=lerp(lerp(shadow,mid,low),lit,high)+baseColor*_Fill;
    InputData inputData=(InputData)0;
    inputData.positionWS=i.positionWS; inputData.normalWS=n;
    inputData.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
    inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
    #if defined(_ADDITIONAL_LIGHTS)
     #if USE_CLUSTER_LIGHT_LOOP
      UNITY_LOOP for(uint lightIndex=0; lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS); lightIndex++)
      {
       CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
       Light l=GetAdditionalLight(lightIndex,i.positionWS,half4(1,1,1,1)); c+=Extra(l,n,baseColor);
      }
     #endif
     uint pixelLightCount=GetAdditionalLightsCount();
     LIGHT_LOOP_BEGIN(pixelLightCount)
      Light l=GetAdditionalLight(lightIndex,i.positionWS,half4(1,1,1,1)); c+=Extra(l,n,baseColor);
     LIGHT_LOOP_END
    #endif
    c+=SAMPLE_TEXTURE2D(_EmissionMask,sampler_EmissionMask,i.uv).r*_EmissionColor.rgb;
    return half4(MixFog(c,i.fog),1);
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
   ZWrite On ZTest LEqual ColorMask 0 Cull Back
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex ShadowVert
   #pragma fragment ShadowFrag
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
   float3 _LightDirection; float3 _LightPosition;
   Varyings ShadowVert(Attributes v)
   {
    Varyings o=Vert(v);
    float3 dir=_LightDirection;
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
     dir=normalize(_LightPosition-o.positionWS);
    #endif
    o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,dir));
    #if UNITY_REVERSED_Z
     o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
    #else
     o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
    #endif
    return o;
   }
   half4 ShadowFrag(Varyings i):SV_Target { return 0; }
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
   ZWrite On ColorMask R Cull Back
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment DepthFrag
   #pragma multi_compile_instancing
   half DepthFrag(Varyings i):SV_Target { return i.positionCS.z; }
   ENDHLSL
  }
 }
 Fallback Off
}
