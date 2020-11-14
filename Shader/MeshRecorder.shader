Shader "Motion/MeshRecorder" {
Properties {
	[ToggleUI] _AutoHide ("AutoHide", Float) = 1
	_Layer ("Layer", Float) = 0
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
#pragma fragment frag
#pragma geometry geom
#pragma shader_feature _REQUIRE_UV2 // used for grabpass output
#include <UnityCG.cginc>
#include "Rotation.hlsl"
#include "Codec.hlsl"
#include "VideoLayout.hlsl"

float _AutoHide;
float _Layer;
static const float _PositionScale = 2;

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
	nointerpolation ColorTile color : COLOR;
	float2 uv : TEXCOORD0;
	float4 pos : SV_Position;
	UNITY_VERTEX_OUTPUT_STEREO
};
void vert(VertInput i, out GeomInput o) {
	o = i;
}
[maxvertexcount(4)]
void geom(line GeomInput i[2], inout TriangleStream<FragInput> stream) {
	UNITY_SETUP_INSTANCE_ID(i[0]);

	#if !defined(_REQUIRE_UV2)
		#if defined(USING_STEREO_MATRICES)
			return; // hide in VR
		#endif
		if(_AutoHide && _ProjectionParams.z != 0)
			return; // require farClip == 0 when autohide is on 
		if(determinant((float3x3)UNITY_MATRIX_V) > 0)
			return; // hide in mirror
	#endif

	bool  background = i[0].uv.x < 0;
	uint  slot = i[0].uv.x;
	uint  chan = i[1].uv.x;
	float sign = i[1].uv.y;

	float3x3 mat1 = i[1].GetRotationScale();
	float3 pos  = i[1].GetPosition();
	float3 matY = mat1.c1;
	float3 matZ = mat1.c2;
	if(sign != 0) { // relative to mat0
		float3x3 mat0 = i[0].GetRotationScale();
		pos  -= i[0].GetPosition();
		pos  = mul(transpose(mat0), pos)  / dot(mat0.c1, mat0.c1);
		matY = mul(transpose(mat0), matY) / dot(mat0.c1, mat0.c1);
		matZ = mul(transpose(mat0), matZ) / dot(mat0.c1, mat0.c1);
	}
	float scale = length(matY);
	matY = normalize(matY);
	matZ = normalize(matZ);

	float data;
	if(chan < 3) {
		float3x3 rot;
		rot.c1 = matY;
		rot.c2 = matZ;
		rot.c0 = cross(rot.c1, rot.c2);
		data = toSwingTwist(rot)[chan] * sign / UNITY_PI;
	} else {
		matY *= min(1, scale);
		matZ *= min(1, rcp(scale));
		pos /= _PositionScale;
		data = chan < 9 ? pos[chan-(chan < 6 ? 3 : 6)] : chan < 12 ? matY[chan-9] : matZ[chan-12];
	}

	uint layer = _Layer;
	float4 rect = GetTileRect(slot);
	if(background) { 
		rect = layerRect;
		data = 0;
	}
	rect.xz += layer/2 * layerRect.z;
	if(layer & 1)
		rect.xz = 1-rect.xz;

	FragInput o;
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	EncodeVideoSnorm(o.color, data, chan >= 3 && chan < 6);

	float2 screenSize = _ScreenParams.xy/2;
	rect = round(rect * screenSize.xyxy) / screenSize.xyxy;
	rect = rect*2-1;
	#if !defined(_REQUIRE_UV2)
		rect.yw *= _ProjectionParams.x;
	#elif UNITY_UV_STARTS_AT_TOP
		rect.yw *= -1;
	#endif

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
	return RenderTile(i.color, i.uv);
}
ENDCG
	}
}
}