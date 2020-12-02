#include "Rotation.hlsl"
#include "Codec.hlsl"
#include "VideoLayout.hlsl"
#include "Skinning.hlsl"

float _RotationTolerance;
static const float _PositionScale = 2;

sampler2D _MotionDec;
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
float3 mergeSnorm3(float3 f0, float3 f1) {
	float o[3] = {0,0,0};
	UNITY_LOOP // save instruction
	for(uint K=0; K<3; K++)
		o[K] = DecodeVideoFloat(f0[K], f1[K]);
	return float3(o[0], o[1], o[2]);
}
void TransformBone(inout float3x3 mat, float4 data) {
	mat = mul(fromSwingTwist(UNITY_PI * sampleSnorm3(uint(data.w)) * data.xyz), mat);
}
void TransformRoot(inout float3x3 mat, float4 data) {
	bool highRange = true;
	#if defined(SHADER_API_GLES3)
		highRange = false;
	#endif

	float3 motion[4];
	uint idx = -1-data.w;
	UNITY_LOOP
	for(uint I=0; I<4; I++)
		motion[I] = sampleSnorm3(idx+3*I);
	if(highRange)
		motion[1] = mergeSnorm3(motion[0],motion[1]);

	float3 pos = motion[1] * _PositionScale;
	float3x3 rot;
	float err = orthogonalize(motion[2], motion[3], rot.c1, rot.c2);
	if(err + pow(max(length(rot.c1), length(rot.c2)) - 1, 2) > _RotationTolerance * _RotationTolerance)
		mat.c0 = sqrt(-unity_ObjectToWorld._44); //NaN

	float rlen2 = rsqrt(dot(rot.c2,rot.c2));
	rot.c1 *= rlen2 * data.z;
	rot.c2 *= rlen2 * length(rot.c1);
	rot.c0 = cross(normalize(rot.c1), rot.c2);

	mat = mul(rot, mat);
	mat.c0 += pos;
}
float GetShapeWeight(float data) {
	return sampleSnorm(uint(data));
}

Texture2D _Bone;
Texture2D _Shape;
void SkinVertex(inout VertInputSkin i, uint layer, bool highRange=true) {
	_MotionDec_ST = float4(1, 1, layer/2 * layerRect.z, 0);
	if(layer & 1)
		_MotionDec_ST.xz = float2(0, 1) - _MotionDec_ST.xz;
	SkinVertex(i, _Bone, _Shape);
}