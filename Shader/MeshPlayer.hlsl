#include "Rotation.hlsl"
#include "Codec.hlsl"
#include "VideoLayout.hlsl"

float _RotationTolerance;
static const float _PositionScale = 2;

Texture2D _Bone;
Texture2D _Shape;

sampler2D _MotionDec;
float sampleSnorm(uint idx, float4 st) {
	float2 uv = float2(GetTileX(idx).x, GetTileY(idx).x) * st.xy + st.zw;
	return DecodeBufferSnorm(tex2Dlod(_MotionDec, float4(uv, 0, 0)));
}
float3 sampleSnorm3(uint idx, float4 st) {
	float3 u = GetTileX(idx+uint4(0,1,2,3)) * st.x + st.z;
	float3 v = GetTileY(idx+uint4(0,1,2,3)) * st.y + st.w;
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

struct VertInputPlayer {
	float3 vertex : POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float4 texcoord : TEXCOORD0;
	float4 boneIndices : TEXCOORD1;
	float4 boneWeights : TEXCOORD2;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
void SkinVertex(inout VertInputPlayer i, Texture2D boneTex, Texture2D shapeTex, uint layer, bool highRange) {
	float4 st = float4(1, 1, layer/2 * layerRect.z, 0);
	if(layer & 1)
		st.xz = float2(0, 1) - st.xz;

	// blendshape
	{
		uint2 size; shapeTex.GetDimensions(size.x, size.y);
		uint3 shape = round(i.texcoord.zzw);
		shape.xy = uint2(shape.x%size.x, shape.x/size.x);
		UNITY_LOOP
		for(uint J=0; J<16; J++) {
			if(J >= shape.z)
				break;
			float4 dv = shapeTex.Load(uint3(shape.xy, 0));
			float wt = sampleSnorm(uint(dv.w), st);
			i.vertex.xyz += dv.xyz * wt;
			shape.x ++;
		}
	}
	// skinning
	float3 vertex = 0, normal = 0, tangent = 0;
	for(uint J=0; J<4; J++) {
		uint  index  = round(i.boneIndices[J]);
		float weight = i.boneWeights[J];
		if(weight < 1e-4)
			break;

		float4 data4[1];
		float3x3 mat = transpose(float3x3(i.vertex.xyz, i.normal.xyz, i.tangent.xyz));
		for(uint I=0; I<64; I+=4) {
			float4x4 data = transpose(float4x4(
				boneTex.Load(uint3(I, index, 0), uint2(+0, 0)),
				boneTex.Load(uint3(I, index, 0), uint2(+1, 0)),
				boneTex.Load(uint3(I, index, 0), uint2(+2, 0)),
				boneTex.Load(uint3(I, index, 0), uint2(+3, 0))));

			mat = mul((float3x3)data, mat);
			mat.c0 += data.c3;
			if(data._44 < 0) {
				data4[0] = data._41_42_43_44;
				break;
			}
			mat = mul(fromSwingTwist(UNITY_PI * sampleSnorm3(uint(data._44), st) * data._41_42_43), mat);
		}
		{
			float3 motion[4];
			uint idx = -1-data4[0].w;
			UNITY_LOOP
			for(int I=0; I<4; I++)
				motion[I] = sampleSnorm3(idx+3*I, st);
			if(highRange)
				motion[1] = mergeSnorm3(motion[0],motion[1]);

			float3 pos = motion[1] * _PositionScale;
			float3x3 rot;
			float err = orthogonalize(motion[2], motion[3], rot.c1, rot.c2);
			if(err + pow(max(length(rot.c1), length(rot.c2)) - 1, 2) > _RotationTolerance * _RotationTolerance) {
				vertex = sqrt(-unity_ObjectToWorld._44); //NaN
				break;
			}
			float rlen2 = rsqrt(dot(rot.c2,rot.c2));
			rot.c1 *= rlen2 * data4[0].z;
			rot.c2 *= rlen2 * length(rot.c1);
			rot.c0 = cross(normalize(rot.c1), rot.c2);

			mat = mul(rot, mat);
			mat.c0 += pos;
		}
		vertex  += mat.c0 * weight;
		normal  += mat.c1 * weight;
		tangent += mat.c2 * weight;
	}
	i.vertex.xyz = vertex;
	i.normal.xyz = normal;
	i.tangent.xyz = tangent;
}
void SkinVertex(inout VertInputPlayer i, uint layer, bool highRange=true) {
	SkinVertex(i, _Bone, _Shape, layer, highRange);
}