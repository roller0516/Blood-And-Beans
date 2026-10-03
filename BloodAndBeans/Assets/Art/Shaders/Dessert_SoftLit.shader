// URP pass scaffold adapted from Unity Graphics URP 17.5 (Unity Companion License).
Shader "BloodAndBeans/Dessert Soft Lit"
{
 Properties
 {
  [Header(Base Surface)]
  [Enum(UV0,0,UV1,1)] _UVChannel("Texture UV Channel", Float) = 0
  [MainTexture] _BaseMap("Base Color (sRGB)", 2D) = "white" {}
  [MainColor] _BaseColor("Base Tint", Color) = (1,1,1,1)
  [NoScaleOffset] _SurfaceMap("Surface Map - R AO G Smoothness B Metallic", 2D) = "white" {}
  _SmoothnessMin("Smoothness Min", Range(0,1)) = 0.12
  _SmoothnessMax("Smoothness Max", Range(0,1)) = 0.28
  _AOStrength("Ambient Occlusion Strength", Range(0,1)) = 0.35
  _Metallic("Metallic (food should be 0)", Range(0,1)) = 0
  [Normal][NoScaleOffset] _NormalMap("Base Normal Map", 2D) = "bump" {}
  _NormalStrength("Base Normal Strength", Range(0,2)) = 1
  [Header(Micro Surface)]
  _PoreMap("Pore Height (linear grayscale)", 2D) = "gray" {}
  [NoScaleOffset] _DetailRegion("Detail Region Mask (white enables)", 2D) = "white" {}
  _PoreStrength("Pore Relief", Range(0,2)) = 0.35
  _PoreColor("Pore Color Variation", Range(0,0.5)) = 0.06
  _PoreRoughness("Pore Roughness Variation", Range(0,0.5)) = 0.12
  [Header(Glaze)]
  _GlazeAmount("Glaze Coat Amount", Range(0,1)) = 0
  _GlazeSmoothness("Glaze Smoothness", Range(0,1)) = 0.65
  [Header(Overlay Regions)]
  [NoScaleOffset] _OverlayRegion("Region Mask - R Dirty G Burnt", 2D) = "white" {}
  [Toggle(_OVERLAY_TRIPLANAR)] _OverlayTriplanar("Overlay Triplanar (ignore UV seams)", Float) = 0
  _OverlayTileSize("Overlay Triplanar Tile Size (m)", Range(0.02,2)) = 1
  [Header(Dirty Overlay)]
  _DirtyMap("Dirt Distribution (linear grayscale)", 2D) = "gray" {}
  _DirtyAmount("Dirty Amount - 0 Clean 1 Full", Range(0,1)) = 0
  _DirtySoftness("Dirty Edge Softness", Range(0.01,0.4)) = 0.2
  _DirtyBlur("Dirty Pattern Blur", Range(0,6)) = 2.5
  _DirtyColor("Dirty Color", Color) = (0.18,0.105,0.052,1)
  _DirtyOpacity("Dirty Opacity", Range(0,1)) = 0.8
  _DirtySmoothness("Dirty Smoothness", Range(0,1)) = 0.12
  [Header(Burnt Overlay)]
  _BurntMap("Burn Distribution (linear grayscale)", 2D) = "gray" {}
  _BurntAmount("Burnt Amount - 0 Clean 1 Full", Range(0,1)) = 0
  _BurntSoftness("Burnt Edge Softness", Range(0.01,0.4)) = 0.2
  _BurntBlur("Burnt Pattern Blur", Range(0,6)) = 2.5
  _ToastColor("Toasted Color", Color) = (0.3,0.12,0.035,1)
  _BurntColor("Charred Color", Color) = (0.022,0.016,0.012,1)
  _BurntOpacity("Burnt Opacity", Range(0,1)) = 1
  _BurntSmoothness("Burnt Smoothness", Range(0,1)) = 0.06
  [Header(Overlay Placement)]
  _OverlayHeight("Overlay Height Range (m) - X start, Y full (Y<=X off)", Vector) = (0,0,0,0)
  _OverlayUpFacing("Overlay Only On Up-Facing", Range(0,1)) = 0
  [Header(Soft Toon)]
  [Toggle(_SOFT_TOON)] _SoftToon("Soft Toon Lighting", Float) = 0
  _ToonWrap("Light Wrap", Range(0,1)) = 0.2
  _ToonSoftness("Terminator Softness", Range(0.01,1)) = 0.35
  _ToonShadowTint("Shadow Tint", Color) = (0.5,0.36,0.48,1)
  _ToonAmbient("Ambient Strength", Range(0,2)) = 0.45
  _ToonSpecular("Soft Highlight", Range(0,1)) = 0.2
  _RimColor("Rim Color", Color) = (1,0.94,0.85,1)
  _RimPower("Rim Power", Range(0.5,8)) = 3
  _RimStrength("Rim Strength", Range(0,1)) = 0.35
  [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
  [HideInInspector] _Cull("Cull", Float) = 2
  [HideInInspector] _SrcBlend("Src", Float) = 1
  [HideInInspector] _DstBlend("Dst", Float) = 0
  [HideInInspector] _SrcBlendAlpha("Src A", Float) = 1
  [HideInInspector] _DstBlendAlpha("Dst A", Float) = 0
  [HideInInspector] _ZWrite("ZWrite", Float) = 1
  [HideInInspector] _AlphaToMask("AlphaToMask", Float) = 0
  [HideInInspector] _AddPrecomputedVelocity("Velocity", Float) = 0
  [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
  [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
  [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
 }
    SubShader
    {
        // Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
        // this Subshader will fail. One can add a subshader below or fallback to Standard built-in to make this
        // material work with both Universal Render Pipeline and Builtin Unity Pipeline
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass. Shades all light in a single pass. GI + emission + Fog
        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForwardOnly"
            }

            // -------------------------------------
            // Render State Commands
            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 3.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex DessertLitVertex
            #pragma fragment DessertLitFragment
            // 얼룩·그을림을 UV 대신 표면에 투영한다. UV 조각이 많은 식기에서 무늬가 끊기지 않게 하려는 것이다.
            #pragma shader_feature_local_fragment _OVERLAY_TRIPLANAR
            // 파티 애니멀즈 풍 부드러운 명암. 끄면 기존 PBR 그대로다.
            #pragma shader_feature_local_fragment _SOFT_TOON

            // -------------------------------------
            // Material Keywords
            
            
            
            
            
            
            
            
            
            
            
            
            
            

            // -------------------------------------
            // Universal Pipeline keywords
#if defined(UNITY_PLATFORM_META_QUEST)
            #pragma multi_compile _ META_QUEST_LIGHTUNROLL
#endif
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#if defined(UNITY_PLATFORM_META_QUEST)
            #pragma multi_compile _ META_QUEST_ORTHO_PROJ
            #pragma multi_compile _ META_QUEST_NO_SPOTLIGHTS_LIGHT_LOOP
#endif
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"


            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            

            #define _NORMALMAP 1
            #define _CLEARCOAT 1
            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR   // 투영에 월드 위치가 필요하다
            #include "DessertSoftLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            Varyings DessertLitVertex(Attributes input) { input.texcoord = lerp(input.texcoord,input.staticLightmapUV,saturate(_UVChannel)); return LitPassVertex(input); }

            #if defined(_SOFT_TOON)
            // 명암 경계를 넓게 감싸 흐리고, 그림자는 검게 빼지 않고 색으로 물들인다.
            half DessertToonRamp(half3 normalWS, Light light)
            {
                half wrapped = saturate((dot(normalWS, light.direction) + _ToonWrap) / (1.0h + _ToonWrap));
                half halfWidth = _ToonSoftness * 0.5h;
                return smoothstep(0.5h - halfWidth, 0.5h + halfWidth, wrapped) * light.shadowAttenuation;
            }

            half3 DessertToonHighlight(half3 normalWS, half3 viewWS, Light light, half smoothness)
            {
                half3 halfDir = SafeNormalize(light.direction + viewWS);
                half power = exp2(10.0h * smoothness + 1.0h);
                return pow(saturate(dot(normalWS, halfDir)), power) * _ToonSpecular * light.color;
            }

            // URP `UniversalFragmentPBR`의 빛 순회를 그대로 따르고, 빛 하나의 계산만 바꾼다.
            half4 DessertSoftToon(InputData inputData, SurfaceData surfaceData)
            {
                half4 shadowMask = CalculateShadowMask(inputData);
                AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
                uint meshRenderingLayers = GetMeshRenderingLayer();
                half3 albedo = surfaceData.albedo;
                half3 normalWS = inputData.normalWS;
                half3 viewWS = inputData.viewDirectionWS;

                Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
                MixRealtimeAndBakedGI(mainLight, normalWS, inputData.bakedGI);
                half3 color = albedo * inputData.bakedGI * _ToonAmbient * aoFactor.indirectAmbientOcclusion;

            #ifdef _LIGHT_LAYERS
                if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
            #endif
                {
                    half ramp = DessertToonRamp(normalWS, mainLight);
                    color += albedo * mainLight.color * mainLight.distanceAttenuation * lerp(_ToonShadowTint.rgb, 1.0h, ramp);
                    color += DessertToonHighlight(normalWS, viewWS, mainLight, surfaceData.smoothness) * ramp;
                    half rim = pow(1.0h - saturate(dot(normalWS, viewWS)), _RimPower) * _RimStrength;
                    color += rim * _RimColor.rgb * mainLight.color * lerp(0.4h, 1.0h, ramp);
                }

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
            #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
            #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
            #endif
                        color += albedo * light.color * light.distanceAttenuation * DessertToonRamp(normalWS, light);
                }
            #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
            #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
            #endif
                        color += albedo * light.color * light.distanceAttenuation * DessertToonRamp(normalWS, light);
                LIGHT_LOOP_END
            #endif

            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                color += inputData.vertexLighting * albedo;
            #endif
                return half4(min(color, HALF_MAX), surfaceData.alpha);
            }

            // `LitPassFragment`와 같은 순서로 표면·입력을 만들고, 마지막 조명만 툰으로 바꾼다.
            void DessertToonFragment(Varyings input, out half4 outColor)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);
            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif
                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
            #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
            #endif
                InitializeBakedGIData(input, inputData);
                half4 color = DessertSoftToon(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent());
                outColor = color;
            }
            #endif

            // URP 표면 함수는 UV만 받는다. 투영에 쓸 위치·법선을 먼저 넘겨 두고 원래 진입점을 부른다.
            void DessertLitFragment(Varyings input, out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                DessertSetOverlaySpace(input.positionWS, input.normalWS);
            #if defined(_SOFT_TOON)
                DessertToonFragment(input, outColor);
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            #else
                LitPassFragment(input, outColor
            #ifdef _WRITE_RENDERING_LAYERS
                    , outRenderingLayers
            #endif
                );
            #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // -------------------------------------
            // Material Keywords
            
            

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            

            // -------------------------------------
            // Universal Pipeline keywords

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // -------------------------------------
            // Includes
            #define _NORMALMAP 1
            #define _CLEARCOAT 1
            #include "DessertSoftLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }



        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            
            

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            

            // -------------------------------------
            // Includes
            #define _NORMALMAP 1
            #define _CLEARCOAT 1
            #include "DessertSoftLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // This pass is used when drawing to a _CameraNormalsTexture texture
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormalsOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            
            
            
            
            

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // -------------------------------------
            // Universal Pipeline keywords
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            

            // -------------------------------------
            // Includes
            #define _NORMALMAP 1
            #define _CLEARCOAT 1
            #include "DessertSoftLitInput.hlsl"
            #include "DessertDepthNormalsPass.hlsl"
            ENDHLSL
        }

        // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            // -------------------------------------
            // Render State Commands
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex DessertMetaVertex
            #pragma fragment UniversalFragmentMetaLit

            // -------------------------------------
            // Material Keywords
            
            
            
            
            
            
            
            

            // -------------------------------------
            // Includes
            #define _NORMALMAP 1
            #define _CLEARCOAT 1
            #include "DessertSoftLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            Varyings DessertMetaVertex(Attributes input) { input.uv0 = lerp(input.uv0,input.uv1,saturate(_UVChannel)); return UniversalVertexMeta(input); }

            ENDHLSL
        }



        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            HLSLPROGRAM
            
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            

            #define _NORMALMAP 1
            #define _CLEARCOAT 1
            #include "DessertSoftLitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }


    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"

}



