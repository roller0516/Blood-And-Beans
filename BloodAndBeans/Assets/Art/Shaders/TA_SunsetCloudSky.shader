Shader "BloodAndBeans/Sky/Sunset Clouds"
{
    Properties
    {
        _CloudLight("Cloud Sunlit Color",Color)=(0.95,0.69,0.53,1)
        _CloudShade("Cloud Shade Color",Color)=(0.38,0.29,0.43,1)
        _CloudCoverage("Cloud Coverage",Range(0,1))=0.5
        _CloudOpacity("Cloud Opacity",Range(0,1))=0.65
        _CloudScale("Cloud Scale",Float)=5
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
        // 경계 돔 메시에 입힐 때만 켠다. 방향을 카메라 기준으로 잡고 깊이를 써서 돔 바깥을 가린다.
        [Toggle] _Dome ("Dome Mesh", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull [_Cull] ZWrite [_ZWrite]
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
            float _MoonSize, _Halo, _Stars, _Exposure, _Gradient, _Dome;
            float4 _CloudLight,_CloudShade;
            float _CloudCoverage,_CloudOpacity,_CloudScale;
            float hashCloud(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
            float noiseCloud(float3 p){float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hashCloud(a),hashCloud(a+float3(1,0,0)),f.x),lerp(hashCloud(a+float3(0,1,0)),hashCloud(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hashCloud(a+float3(0,0,1)),hashCloud(a+float3(1,0,1)),f.x),lerp(hashCloud(a+float3(0,1,1)),hashCloud(a+float3(1,1,1)),f.x),f.y),f.z);}
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.vertex);
                o.direction = _Dome > 0.5 ? mul(unity_ObjectToWorld, v.vertex).xyz - _WorldSpaceCameraPos : v.vertex.xyz;
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
                float3 cp=d*float3(_CloudScale, _CloudScale*2.4, _CloudScale)+float3(_Time.y*0.002,0,0);
                float n=noiseCloud(cp)*0.57+noiseCloud(cp*2.03+7)*0.28+noiseCloud(cp*4.01+13)*0.15;
                float mask=smoothstep(0.76-_CloudCoverage*0.45,0.94-_CloudCoverage*0.45,n);
                mask*=smoothstep(-0.035,0.10,d.y)*(1-smoothstep(0.7,0.97,d.y));
                float silver=pow(saturate(dot(d,normalize(_MoonDirection.xyz))),6);
                float3 cloud=lerp(_CloudShade.rgb,_CloudLight.rgb,saturate(n*0.65+silver*0.5));
                sky=lerp(sky,cloud,mask*_CloudOpacity);
                return half4(sky * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
