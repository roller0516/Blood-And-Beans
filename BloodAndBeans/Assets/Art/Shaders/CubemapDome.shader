// 큐브맵 스카이박스를 경계 돔 메시(안쪽 면만 보이는 구)에 입힌다.
// `Skybox/Cubemap`은 스카이박스 메시용이라 구 메시에는 그려지지 않는다.
Shader "BloodAndBeans/Sky/Cubemap Dome"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
        [Gamma] _Exposure ("Exposure", Range(0,8)) = 1.0
        _Rotation ("Rotation", Range(0,360)) = 0
        [NoScaleOffset] _Tex ("Cubemap (HDR)", Cube) = "grey" {}
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
            samplerCUBE _Tex;
            half4 _Tex_HDR;
            half4 _Tint;
            half _Exposure;
            float _Rotation;
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.vertex);
                // 방향은 카메라 기준이다. 돔 안에서 움직여도 하늘은 시차 없이 무한히 멀다.
                float3 d = mul(unity_ObjectToWorld, v.vertex).xyz - _WorldSpaceCameraPos;
                float angle = fmod(_Rotation + _Time.y * 2 , 360);
                float s, c;

                //_Rotation += 
                sincos(radians(angle), s, c);
                o.direction = float3(c * d.x - s * d.z, d.y, s * d.x + c * d.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = texCUBE(_Tex, i.direction);
                half3 c = DecodeHDR(tex, _Tex_HDR);
                c = c * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
