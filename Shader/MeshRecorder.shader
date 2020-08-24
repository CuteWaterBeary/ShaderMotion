Shader "Motion/MeshRecorder" {
Properties {
	[ToggleUI] _AutoHide ("AutoHide", Float) = 1
	_Layer ("Layer", Float) = 0
}
SubShader {
	Tags { "Queue"="Overlay" "RenderType"="Overlay" "PreviewType"="Plane" }
	Pass {
		Tags { "LightMode"="Vertex" }
		Cull Off
		ZTest Always ZWrite Off
CGPROGRAM
#pragma target 4.0
#pragma vertex vert
#pragma fragment frag
#pragma geometry geom
#include <UnityCG.cginc>
#include "Rotation.hlsl"
#include "Codec.hlsl"
#include "Layout.hlsl"

float _AutoHide;
float _Layer;
static float _PositionLimit = 2;

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
	float3x3 GetRotation() {
		float3x3 m;
		m.c0 = cross(normalize(normal), tangent.xyz);
		m.c1 = normal;
		m.c2 = tangent.xyz;
		return m;
	}
};
struct FragInput {
	nointerpolation float3 color[2] : COLOR;
	float2 uv : TEXCOORD0;
	float4 pos : SV_Position;
	UNITY_VERTEX_OUTPUT_STEREO
};
void vert(VertInput i, out GeomInput o) {
	o = i;
}
[maxvertexcount(4)]
void geom(triangle GeomInput i[3], inout TriangleStream<FragInput> stream) {
	UNITY_SETUP_INSTANCE_ID(i[0]);

	#if defined(USING_STEREO_MATRICES)
		return; // no VR
	#endif
	if(_AutoHide && !(unity_OrthoParams.w != 0 && _ProjectionParams.z == 0))
		return; // require ortho camera with far == 0 when autohide is on 
	if(determinant((float3x3)UNITY_MATRIX_V) > 0)
		return; // no mirror

	uint  slot = i[0].tangent.w;
	uint  chan = i[1].tangent.w;
	float sign = i[2].tangent.w;

	// compute relative rot/pos
	// r0 is seen as (scale(r1) * id) when sign == 0 
	float3x3 r0 = i[0].GetRotation();
	float3x3 r1 = i[1].GetRotation();
	float3x3 rot;
	rot.c1 = normalize(sign != 0 ? mul(transpose(r0), r1.c1) : r1.c1);
	rot.c2 = normalize(sign != 0 ? mul(transpose(r0), r1.c2) : r1.c2);
	rot.c0 = cross(rot.c1, rot.c2);
	float3 pos = sign != 0 ? mul(transpose(r0), i[1].vertex-i[0].vertex) / dot(r0.c1, r0.c1)
							: (i[1].vertex-i[0].vertex) / length(r1.c1);

	FragInput o;
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	float2 flip = float2(_Layer == 0 ? 1 : -1, _ProjectionParams.x);
	float4 rect = (GetSlotRect(slot) * 2 - 1) * flip.xyxy;
	float  data = chan < 3 ? rotationToMuscle(rot)[chan] * sign / PI
				: chan < 9 ? pos[chan-(chan < 6 ? 3 : 6)] / _PositionLimit
				: chan < 12 ? rot.c1[chan-9] : rot.c2[chan-12];

	// background quad
	if(i[0].tangent.w < 0) { 
		rect = (float4(0, 0, 6.0/80, 1) * 2 - 1) * flip.xyxy;
		data = 0;
	}

	float3 c0, c1, c2, c3;
	VideoEncodeFloat(data, c0, c1, o.color[0], o.color[1]);
	if(chan >= 3 && chan < 6)
		o.color[0] = c0, o.color[1] = c1;

	float4 uv = float4(0,0,1,1);
	rect = round(rect * _ScreenParams.xyxy) / _ScreenParams.xyxy;
	o.uv = uv.xy;
	o.pos = float4(rect.xy, UNITY_NEAR_CLIP_VALUE, 1);
	stream.Append(o);
	o.uv = uv.xw;
	o.pos = float4(rect.xw, UNITY_NEAR_CLIP_VALUE, 1);
	stream.Append(o);
	o.uv = uv.zy;
	o.pos = float4(rect.zy, UNITY_NEAR_CLIP_VALUE, 1);
	stream.Append(o);
	o.uv = uv.zw;
	o.pos = float4(rect.zw, UNITY_NEAR_CLIP_VALUE, 1);
	stream.Append(o);
}
float4 frag(FragInput i) : SV_Target {
	return float4(RenderSlot(i.color, i.uv), 1);
}
ENDCG
	}
}
}