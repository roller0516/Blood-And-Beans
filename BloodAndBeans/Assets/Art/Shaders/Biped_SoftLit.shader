Shader "BloodAndBean/Style/BipedSoft"
{
 Properties
 {
  [Header(Texture Maps)] [Enum(GhostPacked,0,PartyPacked,1,AO_Smooth_Metal,2,Uniform,3,URP_Metallic,4)] _SurfaceLayout("Surface Map Layout",Float)=3
  _Roughness("Uniform Roughness",Range(0.1,1))=0.7
  _Metallic("Metallic",Range(0,1))=0
  _TextureSaturation("Texture Saturation",Range(0,1.5))=0.95
  _TextureContrast("Texture Contrast",Range(0,1.5))=0.9
  _OcclusionMap("Occlusion Map",2D)="white"{} _BaseMap("BaseColorMap (sRGB)",2D)="white"{}
  _SurfaceMap("SurfaceMap R Spec G Rough B AO A Emission",2D)="white"{}
  [Normal] _NormalMap("NormalMap",2D)="bump"{}
  _BaseColor("Base Tint",Color)=(1,1,1,1)
  _NormalStrength("Normal Strength",Range(0,2))=0.4
  [Header(Soft Material)] _RoughnessScale("Roughness Scale",Range(0.1,2))=1
  _SpecularStrength("Specular Strength",Range(0,2))=0.6
  _DiffuseWrap("Diffuse Wrap",Range(0,0.5))=0.16
  _FillColor("Fill Color",Color)=(0.6,0.7,0.85,1)
  _FillStrength("Shadow Fill Strength",Range(0,0.4))=0.16
  _ShadowStrength("Shadow Strength",Range(0,1))=0.85
  _AOStrength("AO Strength",Range(0,1))=0.5
  [Header(Team Area)] _TeamColor("Team Color",Color)=(1,0.65,0.2,1)
  _TeamStrength("Team Color Strength",Range(0,1))=1
  [Header(Emission)] [HDR] _EmissionColor("Emission Color",Color)=(1,0.66,0.2,1)
  [Enum(SurfaceMapAlpha,0,EmissionMask,1,WholeSurface,2)] _EmissionMaskSource("Emission Area Source",Float)=0
  _EmissionMap("Emission Mask (R white emits)",2D)="white"{}
  _EmissionStrength("Emission Strength",Range(0,12))=0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
  TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
  TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
  TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
  TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
  TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
  CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST, _BaseColor, _TeamColor, _EmissionColor, _FillColor;
   float _FillStrength;
   float _SurfaceLayout,_Roughness,_Metallic,_TextureSaturation,_TextureContrast;
   float _NormalStrength,_RoughnessScale,_SpecularStrength,_DiffuseWrap;
   float _ShadowStrength,_AOStrength,_TeamStrength,_EmissionStrength,_EmissionMaskSource;
  CBUFFER_END
  struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;float4 color:COLOR;};
  struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;half fog:TEXCOORD4;half team:TEXCOORD5;};
  Varyings Vert(Attributes i){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.tangentWS=half4(TransformObjectToWorldDir(i.tangentOS.xyz),i.tangentOS.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.team=i.color.r;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
  half3 Normal(Varyings i){half3 n=normalize(i.normalWS),t=SafeNormalize(i.tangentWS.xyz),b=cross(n,t)*i.tangentWS.w;half3 nt=UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,i.uv),_NormalStrength);return normalize(TransformTangentToWorld(nt,half3x3(t,b,n)));}
  half3 Shade(Light l,half3 n,half3 view,half3 albedo,half4 surface){half ndl=saturate(dot(n,l.direction));half diffuse=saturate((dot(n,l.direction)+_DiffuseWrap)/(1+_DiffuseWrap));half rough=clamp(surface.g*_RoughnessScale,0.18,1);half exponent=clamp(2/(rough*rough)-2,2,60);half spec=pow(saturate(dot(n,SafeNormalize(l.direction+view))),exponent)*ndl*surface.r*_SpecularStrength;return (albedo*diffuse+spec)*l.color*l.distanceAttenuation*lerp(1,l.shadowAttenuation,_ShadowStrength);}
  ENDHLSL
  Pass
  {
   Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #pragma multi_compile_fog
   half4 Frag(Varyings i):SV_Target{
    half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
    albedo*=lerp(half3(1,1,1),_TeamColor.rgb,saturate(i.team*_TeamStrength));
    half4 surface=SAMPLE_TEXTURE2D(_SurfaceMap,sampler_SurfaceMap,i.uv);
    if(_SurfaceLayout<0.5)surface.b=1; else if(_SurfaceLayout>1.5&&_SurfaceLayout<2.5)surface=half4(lerp(0.22,0.8,surface.b),1-surface.g,surface.r,0); else if(_SurfaceLayout>2.5&&_SurfaceLayout<3.5)surface=half4(lerp(0.22,0.8,_Metallic),_Roughness,1,0); else if(_SurfaceLayout>3.5)surface=half4(lerp(0.22,0.8,surface.r*_Metallic),1-surface.a,1,0);
    surface.b*=SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,i.uv).g;
    half gray=dot(albedo,half3(0.2126,0.7152,0.0722));albedo=lerp(gray.xxx,albedo,_TextureSaturation);albedo=max(0,lerp(half3(0.5,0.5,0.5),albedo,_TextureContrast));
    half3 n=Normal(i),view=GetWorldSpaceNormalizeViewDir(i.positionWS);
    half3 color=albedo*max(SampleSH(n),_FillColor.rgb*_FillStrength)*lerp(1,surface.b,_AOStrength);
    #if defined(_SCREEN_SPACE_OCCLUSION)
    color*=lerp(1,GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS)).indirectAmbientOcclusion,0.6);
    #endif
    color+=Shade(GetMainLight(TransformWorldToShadowCoord(i.positionWS)),n,view,albedo,surface);
    InputData inputData=(InputData)0;inputData.positionWS=i.positionWS;inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
    #if USE_CLUSTER_LIGHT_LOOP
    UNITY_LOOP for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++){CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK color+=Shade(GetAdditionalLight(lightIndex,i.positionWS),n,view,albedo,surface);}
    #endif
    #if defined(_ADDITIONAL_LIGHTS)
    uint count=GetAdditionalLightsCount();LIGHT_LOOP_BEGIN(count) color+=Shade(GetAdditionalLight(lightIndex,i.positionWS),n,view,albedo,surface);LIGHT_LOOP_END
    #endif
    half emissionMask=surface.a;
    if(_EmissionMaskSource>0.5)emissionMask=_EmissionMaskSource<1.5?SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,i.uv).r:1;
    color+=emissionMask*_EmissionColor.rgb*_EmissionStrength;
    return half4(MixFog(color,i.fog),1);
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ColorMask 0
   HLSLPROGRAM
   #pragma vertex ShadowVert
   #pragma fragment ShadowFrag
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   float3 _LightDirection,_LightPosition;
   float4 ShadowVert(Attributes i):SV_POSITION{float3 p=TransformObjectToWorld(i.positionOS.xyz),n=TransformObjectToWorldNormal(i.normalOS);
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 direction=normalize(_LightPosition-p);
    #else
    float3 direction=_LightDirection;
    #endif
    float4 c=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
    #if UNITY_REVERSED_Z
    c.z=min(c.z,c.w*UNITY_NEAR_CLIP_VALUE);
    #else
    c.z=max(c.z,c.w*UNITY_NEAR_CLIP_VALUE);
    #endif
    return c;}
   half4 ShadowFrag():SV_Target{return 0;}
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment DepthFrag
   half4 DepthFrag(Varyings i):SV_Target{return 0;}
   ENDHLSL
  }
  Pass
  {
   Name "DepthNormals" Tags {"LightMode"="DepthNormals"} ZWrite On
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment DepthNormalsFrag
   #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
   half4 DepthNormalsFrag(Varyings i):SV_Target{half3 n=Normal(i);
    #if defined(_GBUFFER_NORMALS_OCT)
    float2 oct=PackNormalOctQuadEncode(n);return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
    #else
    return half4(n,0);
    #endif
   }
   ENDHLSL
  }
 }
}

