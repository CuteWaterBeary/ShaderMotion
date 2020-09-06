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
static float _PositionScale = 2;

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
	float3 GetPosition() {
		return vertex;
	}
	float3x3 GetRotationScale() {
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

	float3x3 mat0 = i[0].GetRotationScale();
	float3x3 mat1 = i[1].GetRotationScale();
	float3 pos  = i[1].GetPosition() - i[0].GetPosition();
	float3 matY = mat1.c1;
	float3 matZ = mat1.c2;
	if(sign != 0) { // relative to mat0
		matY = mul(transpose(mat0), matY) / dot(mat0.c1, mat0.c1);
		matZ = mul(transpose(mat0), matZ) / dot(mat0.c1, mat0.c1);
		pos  = mul(transpose(mat0), pos)  / dot(mat0.c1, mat0.c1);
	}
	float data;
	if(chan < 3) {
		float3x3 rot;
		rot.c1 = normalize(matY);
		rot.c2 = normalize(matZ);
		rot.c0 = cross(rot.c1, rot.c2);
		data = toSwingTwist(rot)[chan] * sign / PI;
	} else {
		// scale down pos/mat if scale is too big
		float scale = min(rsqrt(dot(matY, matY)), rcp(_PositionScale));
		data = (chan < 9 ? pos[chan-(chan < 6 ? 3 : 6)]
				: chan < 12 ? matY[chan-9] : matZ[chan-12]) * scale;
	}

	float4 rect = GetSlotRect(slot);
	// background quad
	if(i[0].tangent.w < 0) { 
		rect = float4(0, 0, 6.0/80, 1);
		data = 0;
	}
	if(_Layer == 1)
		rect.xz = 1-rect.xz;

	FragInput o;
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	float3 c0, c1, c2, c3;
	VideoEncodeFloat(data, c0, c1, o.color[0], o.color[1]);
	if(chan >= 3 && chan < 6)
		o.color[0] = c0, o.color[1] = c1;

	float2 screenSize = _ScreenParams.xy/2;
	rect = round(rect * screenSize.xyxy) / screenSize.xyxy;
	rect = rect*2-1;
	rect.yw *= _ProjectionParams.x;

	float4 uv = float4(0,0,1,1);
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