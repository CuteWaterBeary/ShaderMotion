Shader "Motion/Recorder" {
Properties {
	[ToggleUI] _ShowInDesktop ("ShowInDesktop", Float) = 1
	_Id ("Id", Float) = 0
}
SubShader {
	Tags { "Queue"="Overlay" "RenderType"="Overlay" "PreviewType"="Plane" }
	Pass {
		Lighting Off
		Cull Off
		ZTest Always ZWrite Off
CGPROGRAM
#pragma target 5.0
#pragma vertex vert
#pragma fragment frag
#pragma geometry geom
#include <UnityCG.cginc>
#include "Rotation.hlsl"
#include "Codec.hlsl"
#include "Condition.hlsl"

float _ShowInDesktop;
float _Id;
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

	if(!( !IsStereo && (IsOrtho || IsTilted || _ShowInDesktop) && !IsInMirror ))
		return;

	uint  idx = i[0].tangent.w;
	uint  typ = abs(i[1].tangent.w) - 1;
	float sgn = i[1].tangent.w > 0 ? +1 : -1;

	float3 pos1 = i[1].vertex / length(i[1].normal);
	float3x3 r0 = i[0].GetRotation();
	float3x3 r1 = i[1].GetRotation();
	float3x3 rot;
	rot.c1 = normalize(typ < 3 ? mul(transpose(r0), r1.c1) : r1.c1);
	rot.c2 = normalize(typ < 3 ? mul(transpose(r0), r1.c2) : r1.c2);
	rot.c0 = cross(rot.c1, rot.c2);

	FragInput o;
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	uint base=0;

	float2 flip = float2(_Id == 0 ? 1 : -1, _ProjectionParams.x);
	float4 uv = float4(0,0,1,1);
	float4 rec = (LocateSlot(base+idx) * 2 - 1) * flip.xyxy;
	float data = typ < 3 ? rotationToMuscle(rot)[typ] * sgn / PI
				: typ < 9 ? pos1[typ-(typ < 6 ? 3 : 6)] / _PositionLimit
				: typ < 12 ? rot.c1[typ-9] : rot.c2[typ-12];

	// background quad
	if(i[0].tangent.w < 0) { 
		rec = (float4(0, 0, 6.0/80, 1) * 2 - 1) * flip.xyxy;
		data = 0;
	}

	float3 c0, c1, c2, c3;
	EncodeSigned(data, c0, c1, o.color[0], o.color[1]);
	if(typ >= 3 && typ < 6)
		o.color[0] = c0, o.color[1] = c1;

	rec = round(rec * _ScreenParams.xyxy) / _ScreenParams.xyxy;

	o.uv = uv.xy;
	o.pos = float4(rec.xy, UNITY_NEAR_CLIP_VALUE, 1);
	stream.Append(o);
	o.uv = uv.xw;
	o.pos = float4(rec.xw, UNITY_NEAR_CLIP_VALUE, 1);
	stream.Append(o);
	o.uv = uv.zy;
	o.pos = float4(rec.zy, UNITY_NEAR_CLIP_VALUE, 1);
	stream.Append(o);
	o.uv = uv.zw;
	o.pos = float4(rec.zw, UNITY_NEAR_CLIP_VALUE, 1);
	stream.Append(o);
}
float4 frag(FragInput i) : SV_Target {
	return float4(RenderSlot(i.color, i.uv), 1);
}
ENDCG
	}
}
}