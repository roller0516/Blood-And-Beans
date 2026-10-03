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
half _OverlayTileSize;
half4 _OverlayHeight;
half _OverlayUpFacing;
// 부드러운 툰(_SOFT_TOON). 켠 머티리얼만 쓰지만 SRP Batcher 때문에 같은 블록에 둔다.
half4 _ToonShadowTint, _RimColor;
half _ToonWrap, _ToonSoftness, _ToonAmbient, _ToonSpecular, _RimPower, _RimStrength;
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

// 트라이플래너 투영에 쓰는 표면 정보. 정방향 패스 진입점(DessertLitFragment)이 채운다.
// 오브젝트 공간이라 손에 든 식기가 돌아도 무늬가 따라 돈다. 메시가 ×100으로 들어와도 미터로 잰다.
static float3 dessertOverlayPosition;
static half3 dessertOverlayWeight;
// 얼룩·그을림을 윗부분에만 남길 때 쓴다. 진입점이 채우지 않는 패스에서는 가리지 않는 값으로 남는다.
static float dessertOverlayHeight = 1e4;   // 오브젝트 원점에서 월드 위쪽으로 잰 높이(m)
static half dessertOverlayUp = 1;          // 면이 위를 향한 정도

void DessertSetOverlaySpace(float3 positionWS, float3 normalWS)
{
    float scale = length(GetObjectToWorldMatrix()._m00_m10_m20);
    dessertOverlayPosition = TransformWorldToObject(positionWS) * scale;
    half3 weight = pow(abs(normalize(TransformWorldToObjectDir(normalWS))), 4);
    dessertOverlayWeight = weight / (weight.x + weight.y + weight.z);
    dessertOverlayHeight = positionWS.y - GetObjectToWorldMatrix()._m13;
    dessertOverlayUp = normalize(normalWS).y;
}

// 얼룩·그을림이 앉을 수 있는 정도. 높이 범위(_OverlayHeight.xy)와 위를 향한 면(_OverlayUpFacing)으로 윗부분만 남긴다.
// 상태별 머티리얼이 따로라(ItemVisualConfig) 한 머티리얼에는 오버레이가 하나뿐이고, 마스크도 머티리얼마다 정한다.
half DessertOverlayMask()
{
    half height = _OverlayHeight.y > _OverlayHeight.x ? smoothstep(_OverlayHeight.x, _OverlayHeight.y, dessertOverlayHeight) : 1;
    return height * lerp(1, saturate(dessertOverlayUp), _OverlayUpFacing);
}

// 얼룩·그을림 분포 하나를 읽는다. 키워드가 꺼져 있으면 예전처럼 UV로 읽는다.
half DessertOverlaySample(TEXTURE2D_PARAM(map, samplerMap), float4 st, half bias, float2 uv)
{
#if defined(_OVERLAY_TRIPLANAR)
    // 투영은 타일 경계를 여러 번 넘는다. 텍스처의 Mirror 반복을 따르면 무늬가 거울처럼 뒤집히므로 Repeat로 읽는다.
    float3 p = dessertOverlayPosition / _OverlayTileSize;
    half3 w = dessertOverlayWeight;
    return SAMPLE_TEXTURE2D_BIAS(map, sampler_LinearRepeat, p.zy * st.xy + st.zw, bias).r * w.x
         + SAMPLE_TEXTURE2D_BIAS(map, sampler_LinearRepeat, p.xz * st.xy + st.zw, bias).r * w.y
         + SAMPLE_TEXTURE2D_BIAS(map, sampler_LinearRepeat, p.xy * st.xy + st.zw, bias).r * w.z;
#else
    return SAMPLE_TEXTURE2D_BIAS(map, samplerMap, uv * st.xy + st.zw, bias).r;
#endif
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
    half dirtTex = DessertOverlaySample(TEXTURE2D_ARGS(_DirtyMap,sampler_DirtyMap),_DirtyMap_ST,_DirtyBlur,uv);
    half burnTex = DessertOverlaySample(TEXTURE2D_ARGS(_BurntMap,sampler_BurntMap),_BurntMap_ST,_BurntBlur,uv);
    half placement = DessertOverlayMask();
    half dirt = DessertCoverage(dirtTex,_DirtyAmount,_DirtySoftness)*regions.r*_DirtyOpacity*placement;
    half burn = DessertCoverage(burnTex,_BurntAmount,_BurntSoftness)*regions.g*_BurntOpacity*placement;
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



