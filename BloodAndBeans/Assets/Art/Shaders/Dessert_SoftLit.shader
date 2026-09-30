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
            #pragma fragment LitPassFragment

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
            #include "DessertSoftLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            Varyings DessertLitVertex(Attributes input) { input.texcoord = lerp(input.texcoord,input.staticLightmapUV,saturate(_UVChannel)); return LitPassVertex(input); }
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



