Shader "Unlit/Gamma" {
Properties {
	_MainTex ("MainTex", 2D) = "black" {}
}
SubShader {
	Tags { "Queue"="Geometry" "RenderType"="Opaque" }
	Pass {
		Lighting Off
		Cull Off
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include <UnityCG.cginc>
#include <Lighting.cginc>

sampler2D _MainTex;
float4 _MainTex_ST;

struct VertInput {
	float3 vertex  : POSITION;
	float2 uv      : TEXCOORD0;
};
struct FragInput {
	float2 tex : TEXCOORD1;
	float4 pos : SV_Position;
};

float3 GammaToLinear(float3 value) {
	 return value <= 0.04045F? value / 12.92F : pow((value + 0.055F)/1.055F, 2.4F);
}
void vert(VertInput i, out FragInput o) {
	o.pos = UnityObjectToClipPos(i.vertex);
	o.tex = i.uv * _MainTex_ST.xy + _MainTex_ST.zw;
}
float4 frag(FragInput i) : SV_Target {
	float3 sample = tex2Dlod(_MainTex, float4(i.tex, 0, 0));
	return float4(GammaToLinear(sample), 1);
}
ENDCG
	}
}
}