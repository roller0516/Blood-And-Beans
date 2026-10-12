// 6면 스카이박스 텍스처를 경계 돔 메시(안쪽 면만 보이는 구)에 입힌다.
// `Skybox/6 Sided`는 큐브 6면 메시에 면별로 그리는 셰이더라 구 메시에는 그려지지 않는다.
Shader "BloodAndBeans/Sky/Six Sided Dome"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
        [Gamma] _Exposure ("Exposure", Range(0,8)) = 1.0
        _FrontTex ("Front (+Z)", 2D) = "grey" {}
        _BackTex ("Back (-Z)", 2D) = "grey" {}
        _LeftTex ("Left (+X)", 2D) = "grey" {}
        _RightTex ("Right (-X)", 2D) = "grey" {}
        _UpTex ("Up (+Y)", 2D) = "grey" {}
        _DownTex ("Down (-Y)", 2D) = "grey" {}
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1
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
            #include "UnityCG.cginc"
            struct Attributes { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 position : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _FrontTex, _BackTex, _LeftTex, _RightTex, _UpTex, _DownTex;
            half4 _Tint;
            half _Exposure;
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.vertex);
                // 방향은 카메라 기준이다. 돔 안에서 움직여도 하늘은 시차 없이 무한히 멀다.
                o.direction = mul(unity_ObjectToWorld, v.vertex).xyz - _WorldSpaceCameraPos;
                return o;
            }
            // 면 가장자리 한 텍셀 안으로 당긴다. 텍스처가 Repeat이라 그대로 두면 반대편 가장자리가 섞인다.
            float2 Face(float s, float t) { return clamp(float2(s, t) * 0.5 + 0.5, 0.001, 0.999); }
            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float3 a = abs(d);
                half4 tex;
                if (a.x >= a.y && a.x >= a.z)
                    tex = d.x > 0 ? tex2D(_LeftTex, Face(-d.z / a.x, d.y / a.x)) : tex2D(_RightTex, Face(d.z / a.x, d.y / a.x));
                else if (a.y >= a.z)
                    tex = d.y > 0 ? tex2D(_UpTex, Face(d.x / a.y, -d.z / a.y)) : tex2D(_DownTex, Face(d.x / a.y, -d.z / a.y));
                else
                    tex = d.z > 0 ? tex2D(_FrontTex, Face(d.x / a.z, d.y / a.z)) : tex2D(_BackTex, Face(-d.x / a.z, d.y / a.z));
                half3 c = tex.rgb * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
