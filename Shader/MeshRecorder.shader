Shader "Motion/MeshRecorder" {
Properties {
	[Header(Motion)]
	[ToggleUI] _AutoHide ("AutoHide (only visible in camera with farClip=0)", Float) = 1
	_Layer ("Layer (location of motion stripe)", Float) = 0
}
SubShader {
	Tags { "Queue"="Overlay" "RenderType"="Overlay" "PreviewType"="Plane" }
	Pass {
		Tags { "LightMode"="ForwardBase" }
		Cull Off
		ZTest Always ZWrite Off
CGPROGRAM
#pragma target 4.0
#pragma vertex vert
#pragma fragment fragTile
#pragma geometry geom
#pragma shader_feature _REQUIRE_UV2 // used for grabpass output

#include <UnityCG.cginc>
#include "MeshRecorder.hlsl"

struct VertInput {
	float3 vertex  : POSITION;
	float3 normal  : NORMAL;
	float4 tangent : TANGENT;
	float2 uv      : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct GeomInput {
	float3 vertex  : TEXCOORD0;
	float3 normal  : TEXCOORD1;
	float4 tangent : TEXCOORD2;
	float2 uv      : TEXCOORD3;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
float4x4 getMatrix(GeomInput i) {
	float4x4 m;
	m.c0 = cross(normalize(i.normal), i.tangent.xyz);
	m.c1 = i.normal;
	m.c2 = i.tangent.xyz;
	m.c3 = i.vertex;
	m._41_42_43_44 = float4(0,0,0,1);
	return m;
}
void vert(VertInput i, out GeomInput o) {
	o = i;
}
[maxvertexcount(4)]
void geom(line GeomInput i[2], inout TriangleStream<FragInputTile> stream) {
	FragInputTile o;
	UNITY_SETUP_INSTANCE_ID(i[0]);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	VertInputTile I;
	I.slot = i[0].uv.x;
	I.axis = i[1].uv.x;
	I.sign = i[1].uv.y;
	I.mat0 = getMatrix(i[0]);
	I.mat1 = getMatrix(i[1]);
	if(I.sign == 0)
		I.mat0 = float4x4(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1);

	o.uv = 0;
	float4 rect = EncodeTransform(I, o);
	float4 uv = float4(0,0,1,1);
	o.uv = uv.xy, o.pos.xy = rect.xy, stream.Append(o);
	o.uv = uv.xw, o.pos.xy = rect.xw, stream.Append(o);
	o.uv = uv.zy, o.pos.xy = rect.zy, stream.Append(o);
	o.uv = uv.zw, o.pos.xy = rect.zw, stream.Append(o);
}
ENDCG
	}
}
}