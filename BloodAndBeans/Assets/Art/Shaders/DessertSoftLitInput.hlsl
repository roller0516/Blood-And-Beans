#ifndef BB_DESSERT_SOFT_LIT_INPUT_INCLUDED
#define BB_DESSERT_SOFT_LIT_INPUT_INCLUDED
// The URP Lit pass includes LitInput by name. Supply its input contract here.
#define UNIVERSAL_LIT_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/SurfaceType.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST, _BaseMap_TexelSize;
float4 _PoreMap_ST, _PoreMap_TexelSize, _DirtyMap_ST, _BurntMap_ST;
half4 _BaseColor, _DirtyColor, _ToastColor, _BurntColor;
half _SmoothnessMin, _SmoothnessMax, _AOStrength, _Metallic, _NormalStrength;
half _PoreStrength, _PoreColor, _PoreRoughness, _GlazeAmount, _GlazeSmoothness;
half _UVChannel, _DirtyBlur, _BurntBlur;
half _DirtyAmount, _DirtySoftness, _DirtyOpacity, _DirtySmoothness;
half _BurntAmount, _BurntSoftness, _BurntOpacity, _BurntSmoothness, _Cutoff;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END
TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
TEXTURE2D(_PoreMap); SAMPLER(sampler_PoreMap);
TEXTURE2D(_DetailRegion); SAMPLER(sampler_DetailRegion);
TEXTURE2D(_OverlayRegion); SAMPLER(sampler_OverlayRegion);
TEXTURE2D(_DirtyMap); SAMPLER(sampler_DirtyMap);
TEXTURE2D(_BurntMap); SAMPLER(sampler_BurntMap);

half3 DessertNormalTS(float2 uv)
{
    half3 baseNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalStrength);
    float2 p = uv * _PoreMap_ST.xy + _PoreMap_ST.zw;
    half h = SAMPLE_TEXTURE2D(_PoreMap, sampler_PoreMap, p).r;
    half hx = SAMPLE_TEXTURE2D(_PoreMap, sampler_PoreMap, p + float2(_PoreMap_TexelSize.x,0)).r;
    half hy = SAMPLE_TEXTURE2D(_PoreMap, sampler_PoreMap, p + float2(0,_PoreMap_TexelSize.y)).r;
    half region = SAMPLE_TEXTURE2D(_DetailRegion, sampler_DetailRegion, uv).r;
    // Height gradients are deliberately shallow: baked food pores, not displacement.
    half2 slope = half2(h-hx,h-hy) * (_PoreStrength * 6.0h * region);
    half3 microNormal = normalize(half3(slope,1));
    return normalize(BlendNormalRNM(normalize(baseNormal),microNormal));
}

half DessertCoverage(half pattern, half amount, half softness)
{
    half coverage = smoothstep(1-amount-softness,1-amount+softness,pattern);
    // Exact clean/full endpoints; avoid residual dirt at zero and pinholes at one.
    coverage *= smoothstep(0,0.04h,amount);
    return lerp(coverage,1,smoothstep(0.96h,1,amount));
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    surface = (SurfaceData)0;
    half3 packed = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, uv).rgb;
    half detailRegion = SAMPLE_TEXTURE2D(_DetailRegion, sampler_DetailRegion, uv).r;
    half pore = SAMPLE_TEXTURE2D(_PoreMap, sampler_PoreMap, uv*_PoreMap_ST.xy+_PoreMap_ST.zw).r;
    surface.albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
    surface.albedo *= 1 + (pore-0.5h)*(_PoreColor*detailRegion);
    surface.smoothness = saturate(lerp(_SmoothnessMin,_SmoothnessMax,packed.g) - (1-pore)*_PoreRoughness*detailRegion);
    surface.normalTS = DessertNormalTS(uv);
    surface.occlusion = lerp(1,packed.r,_AOStrength);
    surface.metallic = packed.b*_Metallic;
    surface.clearCoatMask = _GlazeAmount;
    surface.clearCoatSmoothness = _GlazeSmoothness;
    surface.alpha = 1;

    half2 regions = SAMPLE_TEXTURE2D(_OverlayRegion,sampler_OverlayRegion,uv).rg;
    half dirtTex = SAMPLE_TEXTURE2D_BIAS(_DirtyMap,sampler_DirtyMap,uv*_DirtyMap_ST.xy+_DirtyMap_ST.zw,_DirtyBlur).r;
    half burnTex = SAMPLE_TEXTURE2D_BIAS(_BurntMap,sampler_BurntMap,uv*_BurntMap_ST.xy+_BurntMap_ST.zw,_BurntBlur).r;
    half dirt = DessertCoverage(dirtTex,_DirtyAmount,_DirtySoftness)*regions.r*_DirtyOpacity;
    half burn = DessertCoverage(burnTex,_BurntAmount,_BurntSoftness)*regions.g*_BurntOpacity;
    surface.albedo = lerp(surface.albedo,_DirtyColor.rgb,dirt);
    surface.smoothness = lerp(surface.smoothness,_DirtySmoothness,dirt);
    half charLevel = smoothstep(0.30h,0.90h,_BurntAmount);
    half3 burnColor = lerp(_ToastColor.rgb,_BurntColor.rgb,charLevel);
    surface.albedo = lerp(surface.albedo,burnColor,burn);
    surface.smoothness = lerp(surface.smoothness,_BurntSmoothness,burn);
    surface.clearCoatMask *= (1-dirt)*(1-burn);
    surface.albedo = saturate(surface.albedo);
}
#endif



