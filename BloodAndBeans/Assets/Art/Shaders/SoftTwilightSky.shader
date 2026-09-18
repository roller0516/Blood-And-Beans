Shader "BloodAndBeans/Sky/Soft Twilight"
{
    Properties
    {
        [HDR] _Zenith ("Zenith - Blue Violet", Color) = (0.065,0.105,0.22,1)
        [HDR] _Horizon ("Horizon - Dusty Lavender", Color) = (0.30,0.34,0.47,1)
        [HDR] _Ground ("Lower Hemisphere", Color) = (0.095,0.13,0.20,1)
        [HDR] _MoonColor ("Moon and Halo", Color) = (0.85,0.91,1.0,1)
        _MoonDirection ("Moon Direction", Vector) = (0.4,0.45,0.8,0)
        _MoonSize ("Moon Radius", Range(0.005,0.08)) = 0.023
        _Halo ("Halo Strength", Range(0,1)) = 0.12
        _Stars ("Star Brightness", Range(0,1)) = 0.15
        _Exposure ("Exposure", Range(0,4)) = 1
        _Gradient ("Gradient Softness", Range(0.2,3)) = 0.65
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct Attributes { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 position : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            float4 _Zenith, _Horizon, _Ground, _MoonColor, _MoonDirection;
            float _MoonSize, _Halo, _Stars, _Exposure, _Gradient;
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float3 sky = lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(d.y), _Gradient));
                sky = lerp(sky, _Ground.rgb, smoothstep(0, 0.5, -d.y));
                float moonDistance = length(d - normalize(_MoonDirection.xyz));
                float aa = max(fwidth(moonDistance), 0.0003);
                float disc = 1 - smoothstep(_MoonSize-aa, _MoonSize+aa, moonDistance);
                float halo = exp(-moonDistance * moonDistance * 45) * _Halo;
                sky += _MoonColor.rgb * (disc + halo) * smoothstep(-0.02,0.08,d.y);
                // Direction-space cells avoid a longitude seam. Derivative filtering prevents tiny stars flickering.
                float3 grid = d * 160;
                float3 cell = floor(grid);
                float hash = frac(sin(dot(cell, float3(127.1,311.7,74.7))) * 43758.5453);
                float starDistance = length(frac(grid) - 0.5);
                float starAA = max(length(fwidth(grid)), 0.04);
                float star = (1-smoothstep(0.08,0.08+starAA,starDistance)) * step(0.985,hash);
                sky += star * _Stars * smoothstep(0.08,0.45,d.y);
                return half4(sky * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
