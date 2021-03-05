#include "Rotation.hlsl"
#include "Codec.hlsl"
#include "VideoLayout.hlsl"

float _AutoHide;
float _Layer;
static const float _PositionScale = 2;

struct VertInputTile {
	uint axis;
	float sign, slot;
	float4x4 mat0, mat1;
};
struct FragInputTile {
	nointerpolation ColorTile color : COLOR;
	float2 uv : TEXCOORD0;
	float4 pos : SV_Position;
	UNITY_VERTEX_OUTPUT_STEREO
};
float4 EncodeTransform(VertInputTile i, inout FragInputTile o) {
	// pos, rot, scale
	float3 rotY = i.mat1.c1;
	float3 rotZ = i.mat1.c2;
	float3 pos  = i.mat1.c3 - i.mat0.c3;
	pos  = mul(transpose(i.mat0), pos)  / dot(i.mat0.c1, i.mat0.c1);
	rotY = mul(transpose(i.mat0), rotY) / dot(i.mat0.c1, i.mat0.c1);
	rotZ = mul(transpose(i.mat0), rotZ) / dot(i.mat0.c1, i.mat0.c1);
	float scale = length(rotY);
	rotY = normalize(rotY);
	rotZ = normalize(rotZ);

	// data
	float data;
	if(i.axis < 3) {
		float3x3 rot;
		rot.c1 = rotY;
		rot.c2 = rotZ;
		rot.c0 = cross(rot.c1, rot.c2);
		data = swingTwistAngles(rot)[i.axis] / UNITY_PI / i.sign;
	}
	else if(i.axis < 9)
		data = pos[i.axis-(i.axis < 6 ? 3 : 6)] / _PositionScale;
	else if(i.axis < 12)
		data = rotY[i.axis-9 ] * min(1, scale);
	else
		data = rotZ[i.axis-12] * min(1, rcp(scale));

	// color, rect
	float4 rect = GetTileRect(uint(i.slot));
	if(i.slot < 0) // background
		rect = layerRect, data = 0;
	EncodeVideoSnorm(o.color, data, i.axis >= 3 && i.axis < 6);

	// pos
	uint layer = _Layer;
	rect.xz += layer/2 * layerRect.z;
	if(layer & 1)
		rect.xz = 1-rect.xz;

	float2 screenSize = _ScreenParams.xy/2;
	rect = round(rect * screenSize.xyxy) / screenSize.xyxy;
	rect = rect*2-1;
	#if !defined(_REQUIRE_UV2)
		rect.yw *= _ProjectionParams.x;
	#elif UNITY_UV_STARTS_AT_TOP
		rect.yw *= -1;
	#endif

	o.pos = float4(0, 0, UNITY_NEAR_CLIP_VALUE, 1);
	#if !defined(_REQUIRE_UV2)
		#if defined(USING_STEREO_MATRICES)
			return 0; // hide in VR
		#endif
		if(any(UNITY_MATRIX_P[2].xy))
			return 0; // hide in mirror (near plane normal != Z axis)
		if(_AutoHide && _ProjectionParams.z != 0)
			return 0;
	#endif
	return rect;
}
float4 fragTile(FragInputTile i) : SV_Target {
	return RenderTile(i.color, i.uv);
}