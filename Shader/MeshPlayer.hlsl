#include "Rotation.hlsl"
#include "Codec.hlsl"
#include "VideoLayout.hlsl"
#include "Skinning.hlsl"

float _HumanScale;
float _RotationTolerance;
static const float _PositionScale = 2;

sampler2D_float _MotionDec;
static float4 _MotionDec_ST;
float3 sampleSnorm3(uint idx) {
	// NOTE: the tile for an unused component may lie in a different line! 
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
	float scale = data.z;

	float3 motion[4];
	UNITY_LOOP
	for(uint I=0; I<4; I++)
		motion[I] = sampleSnorm3(idx+3*I);
	motion[1] = mergeSnorm3(motion[0],motion[1]);

	float3 pos = motion[1] * _PositionScale;
	float3x3 rot;
	float orthoErrSq = orthogonalize(motion[2], motion[3], rot.c1, rot.c2);
	float scaleErrSq = pow(sqrt(max(dot(rot.c1,rot.c1), dot(rot.c2,rot.c2)))-1, 2);
	if(orthoErrSq + scaleErrSq > _RotationTolerance * _RotationTolerance)
		mat.c0 = sqrt(-unity_ObjectToWorld._44); //NaN

	rot.c1 *= rsqrt(dot(rot.c2,rot.c2));
	if(_HumanScale >= 0) {
		float humanScale = _HumanScale ? _HumanScale : rcp(scale);
		pos    *= rsqrt(dot(rot.c1,rot.c1)) * humanScale;
		rot.c1 *= rsqrt(dot(rot.c1,rot.c1)) * humanScale;
	}
	rot.c1 *= scale;
	rot.c0 = cross(rot.c1, normalize(rot.c2));
	rot.c2 = normalize(rot.c2) * length(rot.c1);

	mat = mul(rot, mat);
	mat.c0 += pos;
}
float2 GetBlendCoord(float4 data) {
	return sampleSnorm3(uint(data.w)).xy;
}

Texture2D_float _Bone;
Texture2D_float _Shape;
void MorphAndSkinVertex(inout VertInputSkin i, uint layer) {
	_MotionDec_ST = float4(1, 1, layer/2 * layerRect.z, 0);
	if(layer & 1)
		_MotionDec_ST.xz = float2(0, 1) - _MotionDec_ST.xz;
	MorphVertex(i, _Shape);
	SkinVertex(i, _Bone);
}