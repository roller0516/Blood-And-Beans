Shader "BloodAndBean/GhostPackedSurface"
{
 Properties
 {
  _BaseMap("BaseColorMap (sRGB)", 2D) = "white" {}
  _SurfaceMap("SurfaceMap (linear RGBA)", 2D) = "white" {}
  _BaseColor("Color tint", Color) = (1,1,1,1)
  _SpecularStrength("Specular multiplier", Range(0,2)) = 1
  _RoughnessScale("Roughness multiplier", Range(0.1,2)) = 1
  _ShadowStrength("Shadow strength", Range(0,1)) = 0.75
  _DiffuseWrap("Soft diffuse wrap", Range(0,0.5)) = 0.12
  _RimColor("Rim color", Color) = (1,0.88,0.70,1)
  _RimStrength("Rim strength", Range(0,2)) = 0.15
  _RimPower("Rim power", Range(1,8)) = 3
  [HDR] _EmissionColor("Emission color", Color) = (1,0.83,0.5,1)
  _EmissionStrength("Emission strength", Range(0,10)) = 3
  [Header(Soft Toon)]
  [Toggle(_SOFT_TOON)] _SoftToon("Soft Toon Lighting", Float) = 0
  _ToonSoftness("Terminator Softness", Range(0.01,1)) = 0.35
  _ToonShadowTint("Shadow Tint", Color) = (0.42,0.44,0.62,1)
  _ToonAmbient("Ambient Strength", Range(0,2)) = 0.45
  _ToonCastShadow("Cast Shadow", Range(0,1)) = 0.4
  _ToonGradient("Lit Gradient", Range(0,1)) = 0.35
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
  TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
  CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST, _BaseColor, _RimColor, _EmissionColor;
   float _SpecularStrength, _RoughnessScale, _ShadowStrength, _DiffuseWrap;
   float _RimStrength, _RimPower, _EmissionStrength;
   // 부드러운 툰(_SOFT_TOON). 켠 머티리얼만 쓰지만 SRP Batcher 때문에 같은 블록에 둔다.
   float4 _ToonShadowTint;
   float _ToonSoftness, _ToonAmbient, _ToonCastShadow, _ToonGradient;
  CBUFFER_END
  struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
  struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float fog:TEXCOORD3; };
  Varyings Vert(Attributes i)
  {
   Varyings o; VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);
   o.positionCS=p.positionCS; o.positionWS=p.positionWS;
   o.normalWS=TransformObjectToWorldNormal(i.normalOS);
   o.uv=TRANSFORM_TEX(i.uv,_BaseMap); o.fog=ComputeFogFactor(p.positionCS.z); return o;
  }
  half3 ShadeLight(Light light,half3 n,half3 v,half3 albedo,half4 surface)
  {
   half ndl=saturate(dot(n,light.direction));
   half diffuse=saturate((dot(n,light.direction)+_DiffuseWrap)/(1+_DiffuseWrap));
   half rough=clamp(surface.g*_RoughnessScale,0.06,1);
   half exponent=clamp(2/(rough*rough)-2,2,256);
   half spec=pow(saturate(dot(n,SafeNormalize(light.direction+v))),exponent)*ndl*surface.r*_SpecularStrength;
   half shadow=lerp(1,light.shadowAttenuation,_ShadowStrength);
   return (albedo*diffuse+spec)*light.color*light.distanceAttenuation*shadow;
  }
  // 「빛을 받는 정도」. 경계는 빛과 수직인 곳(N·L 0)에 둔다 — 0.5에 두면 정수리만 밝은 띠가 된다.
  // 드리운 그림자는 경계를 만들지 않게 따로 약하게 곱한다. 섞으면 가방 그림자가 잘린 조각으로 뜬다.
  half ToonRamp(Light light,half3 n)
  {
   half halfWidth=_ToonSoftness*0.5;
   half ramp=smoothstep(-halfWidth,halfWidth,dot(n,light.direction)+_DiffuseWrap);
   return ramp*lerp(1,light.shadowAttenuation,_ToonCastShadow);
  }
  // 빛 받는 쪽에도 남기는 완만한 명암. 없으면 둥근 몸이 평평해 보인다.
  half ToonLit(Light light,half3 n)
  {
   return lerp(1,saturate(dot(n,light.direction)*0.5+0.5),_ToonGradient);
  }
  half3 ToonSpecular(Light light,half3 n,half3 v,half4 surface)
  {
   half rough=clamp(surface.g*_RoughnessScale,0.06,1);
   half exponent=clamp(2/(rough*rough)-2,2,256);
   return pow(saturate(dot(n,SafeNormalize(light.direction+v))),exponent)*surface.r*_SpecularStrength*light.color;
  }
  ENDHLSL
  Pass
  {
   Name "ForwardLit"
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   // 파티 애니멀즈 풍 부드러운 명암. 끄면 기존 조명 그대로다.
   #pragma shader_feature_local_fragment _SOFT_TOON
   half4 Frag(Varyings i):SV_Target
   {
    half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
    half4 surface=SAMPLE_TEXTURE2D(_SurfaceMap,sampler_SurfaceMap,i.uv);
    half3 n=normalize(i.normalWS),v=GetWorldSpaceNormalizeViewDir(i.positionWS);
    Light main=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    #if defined(_SOFT_TOON)
    // 그림자를 검게 빼지 않고 틴트 색으로 물들인다. 림은 빛을 받는 쪽이 더 밝다.
    half ramp=ToonRamp(main,n);
    half3 color=albedo*max(SampleSH(n),half3(0.025,0.025,0.025))*_ToonAmbient;
    color+=albedo*main.color*main.distanceAttenuation*lerp(_ToonShadowTint.rgb,ToonLit(main,n),ramp)+ToonSpecular(main,n,v,surface)*ramp;
    #ifdef _ADDITIONAL_LIGHTS
    uint count=GetAdditionalLightsCount();
    for(uint k=0;k<count;k++){Light light=GetAdditionalLight(k,i.positionWS);color+=albedo*light.color*light.distanceAttenuation*ToonRamp(light,n)*ToonLit(light,n);}
    #endif
    color+=pow(1-saturate(dot(n,v)),_RimPower)*surface.b*_RimStrength*_RimColor.rgb*lerp(0.4,1,ramp);
    #else
    half3 color=albedo*max(SampleSH(n),half3(0.025,0.025,0.025))+ShadeLight(main,n,v,albedo,surface);
    #ifdef _ADDITIONAL_LIGHTS
    uint count=GetAdditionalLightsCount();
    for(uint k=0;k<count;k++)color+=ShadeLight(GetAdditionalLight(k,i.positionWS),n,v,albedo,surface);
    #endif
    color+=pow(1-saturate(dot(n,v)),_RimPower)*surface.b*_RimStrength*_RimColor.rgb;
    #endif
    color+=surface.a*_EmissionStrength*_EmissionColor.rgb*albedo;
    return half4(MixFog(color,i.fog),1);
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster"
   Tags {"LightMode"="ShadowCaster"}
   ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma vertex ShadowVert
   #pragma fragment ShadowFrag
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   float3 _LightDirection, _LightPosition;
   float4 ShadowVert(Attributes i):SV_POSITION
   {
    float3 p=TransformObjectToWorld(i.positionOS.xyz),n=TransformObjectToWorldNormal(i.normalOS);
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 direction=normalize(_LightPosition-p);
    #else
    float3 direction=_LightDirection;
    #endif
    float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
    #if UNITY_REVERSED_Z
    clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE);
    #else
    clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE);
    #endif
    return clip;
   }
   half4 ShadowFrag():SV_Target {return 0;}
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly"
   Tags {"LightMode"="DepthOnly"}
   ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment DepthFrag
   half4 DepthFrag(Varyings i):SV_Target {return 0;}
   ENDHLSL
  }
 }
}
