#include "Rotation.hlsl"
#include "Codec.hlsl"
#include "VideoLayout.hlsl"
#include "Skinning.hlsl"

float _ApplyScale;
float _RotationTolerance;
static const float _PositionScale = 2;

sampler2D_float _MotionDec;
static float4 _MotionDec_ST;
float sampleSnorm(uint idx) {
	float2 uv = float2(GetTileX(idx).x, GetTileY(idx).x) * _MotionDec_ST.xy + _MotionDec_ST.zw;
	return DecodeBufferSnorm(tex2Dlod(_MotionDec, float4(uv, 0, 0)));
}
float3 sampleSnorm3(uint idx) {
	float3 u = GetTileX(idx+uint4(0,1,2,3)) * _MotionDec_ST.x + _MotionDec_ST.z;
	float3 v = GetTileY(idx+uint4(0,1,2,3)) * _MotionDec_ST.y + _MotionDec_ST.w;
	return float3(	DecodeBufferSnorm(tex2Dlod(_MotionDec, float4(u[0], v[0], 0, 0))),
					DecodeBufferSnorm(tex2Dlod(_MotionDec, float4(u[1], v[1], 0, 0))),
					DecodeBufferSnorm(tex2Dlod(_MotionDec, float4(u[2], v[2], 0, 0))));
}
static const float4x4 Identity = {{1,0,0,0},{0,1,0,0},{0,0,1,0},{0,0,0,1}};
float3 mergeSnorm3(float3 f0, float3 f1) {
	float3 o = 0;
	UNITY_LOOP // fewer instructions
	for(uint K=0; K<3; K++)
		o += DecodeVideoFloat(f0[K], f1[K]) * Identity[K];
	return o;
}
void TransformBone(float4 data, inout float3x3 mat) {
	// data == {sign, idx}
	mat = mul(swingTwistRotate(UNITY_PI * data.xyz * sampleSnorm3(uint(data.w))), mat);
}
void TransformRoot(float4 data, inout float3x3 mat) {
	uint  idx = -1-data.w;
	float scaler = data.z;

	float3 motion[4];
	UNITY_LOOP
	for(uint I=0; I<4; I++)
		motion[I] = sampleSnorm3(idx+3*I);
	#if !defined(SHADER_API_GLES3)
		motion[1] = mergeSnorm3(motion[0],motion[1]);
	#endif

	float3 pos = motion[1] * _PositionScale;
	float3x3 rot;
	float err = orthogonalize(motion[2], motion[3], rot.c1, rot.c2);
	if(err + pow(max(length(rot.c1), length(rot.c2)) - 1, 2) > _RotationTolerance * _RotationTolerance)
		mat.c0 = sqrt(-unity_ObjectToWorld._44); //NaN

	float rlen2 = rsqrt(dot(rot.c2,rot.c2));
	rot.c1 *= rlen2 * scaler;
	if(!_ApplyScale) {
		pos *= rsqrt(dot(rot.c1,rot.c1));
		rot.c1 = normalize(rot.c1);
	}
	rot.c2 *= rlen2 * length(rot.c1);
	rot.c0 = cross(normalize(rot.c1), rot.c2);

	mat = mul(rot, mat);
	mat.c0 += pos;
}
void DeformVertex(float4 data, inout float3 vertex) {
	// data == {offset, idx + sign/4}
	uint  idx = floor(data.w+0.5);
	float sign = frac(data.w+0.5)*4-2;
	vertex += data.xyz * saturate(sampleSnorm(idx)*sign);
}

Texture2D_float _Bone;
Texture2D_float _Shape;
void SkinVertex(inout VertInputSkin i, uint layer) {
	_MotionDec_ST = float4(1, 1, layer/2 * layerRect.z, 0);
	if(layer & 1)
		_MotionDec_ST.xz = float2(0, 1) - _MotionDec_ST.xz;
	SkinVertex(i, _Bone, _Shape);
}