Shader "BloodAndBeans/UI/CrewBackdrop"
{
 Properties { [PerRendererData] _MainTex ("Artwork", 2D) = "white" {} _Color ("Tint", Color)=(1,1,1,1) }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct a { float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR; };
   struct v { float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR; };
   sampler2D _MainTex;fixed4 _Color;
   v vert(a i){v o;o.pos=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color*_Color;return o;}
   fixed4 frag(v i):SV_Target{return tex2D(_MainTex,i.uv)*i.color;}
   ENDCG
  }
 }
}
