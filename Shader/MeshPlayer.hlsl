#include "Rotation.hlsl"
#include "Codec.hlsl"

const static float _PositionScale = 2;
float _RotationTolerance;
Texture2D _Armature;

#define QUALITY 2
struct VertInputPlayer {
	float4 uvSkin[QUALITY][2] : TEXCOORD0;
	float4 uvShape[4] : TEXCOORD4;
	float3 normal  : NORMAL;
	float4 tangent : TANGENT;
	UNITY_VERTEX_INPUT_INSTANCE_ID
	void GetSkin(uint idx, out uint bone, out float4 vertex, out float3 normal, out float3 tangent) {
		bone = floor(uvSkin[idx][0].w);
		vertex = float4(uvSkin[idx][0].xyz, frac(uvSkin[idx][0].w)*2);
		normal = uvSkin[idx][1].xyz;
		tangent = idx == 0 ? this.normal : this.tangent.xyz;
	}
	void GetShape(uint idx, out uint shape, out float3 dvertex) {
		shape = uvShape[idx].w;
		dvertex = uvShape[idx].xyz;
	}
	float2 GetUV() {
		return float2(uvSkin[0][1].w, uvSkin[1][1].w);
	}
};
struct VertInputSkinned {
	float3 vertex  : POSITION;
	float3 normal  : NORMAL;
	float4 tangent : TANGENT;
	float2 texcoord : TEXCOORD0;
};

float sampleSigned(uint idx0, uint idx1, float4 st, bool highRange) {
	float4 rect0 = LocateSlot(idx0) * st.xyxy + st.zwzw;
	float4 rect1 = LocateSlot(idx1) * st.xyxy + st.zwzw;
#ifdef _Motion_Decoded
	return SampleSlot_MergeSigned(_Motion_Decoded, rect0, rect1, highRange);
#else
	return SampleSlot_DecodeSigned(_Motion_Encoded, rect0, rect1, highRange);
#endif
}
float3 sampleSigned3(uint idx0, uint idx1, float4 st, bool highRange=true) {
	float o[3] = {0,0,0};
	UNITY_LOOP
	for(uint K=0; K<3; K++)
		o[K] = sampleSigned(idx0+K, idx1+K, st, highRange);
	return float3(o[0], o[1], o[2]);
}
float sampleSigned(uint idx, float4 st) {
	return sampleSigned(idx, idx, st, false);
}
float3 sampleSigned3(uint idx, float4 st) {
	return sampleSigned3(idx, idx, st, false);
}
void SkinVertex(VertInputPlayer i, out VertInputSkinned o, float layer, bool highRange=true) {
	float4 st = layer == 0 ? float4(1,1,0,0) : float4(-1,1,1,0);
	float NaN = sqrt(-unity_ObjectToWorld._44);

	float3 vertex = 0, normal = 0, tangent = 0;
	for(uint J=0; J<QUALITY; J++) {
		uint bone; float4 vtx; float3 nml, tng;
		i.GetSkin(J, bone, vtx, nml, tng);
		if(vtx.w < 1e-5)
			break;

		float3 pos = 0;
		float3x3 rot = float3x3(1,0,0, 0,1,0, 0,0,1);
		{
			float4 data0 = _Armature.Load(uint3(0, bone, 0));
			float4 data1 = _Armature.Load(uint3(1, bone, 0));
			uint idx = data0.w;

			float3 offset = _PositionScale * sampleSigned3(idx, idx+3, st, highRange);
			pos += offset * data0.y;
			rot  = mulEulerYXZ(rot, data1.xyz);

			float3 c1 = sampleSigned3(idx+6, st);
			float3 c2 = sampleSigned3(idx+9, st);
			float3x3 r;
			orthonormalize(c1, c2, r.c1, r.c2);
			r.c0 = cross(r.c1, r.c2);
			rot = mul(rot, r);
			if(dot(r.c1-c1, r.c1-c1) + dot(r.c2-c2, r.c2-c2) > _RotationTolerance*_RotationTolerance)
				pos = NaN;
		}
		for(uint I=2; I<30; I+=2) {
			float4 data0 = _Armature.Load(uint3(I+0, bone, 0));
			float4 data1 = _Armature.Load(uint3(I+1, bone, 0));
			uint idx = data0.w;
			if(data0.w < 0)
				break;

			pos += mul(rot, data0.xyz);
			rot  = mulEulerYXZ(rot, data1.xyz);
			
			uint maskSign = data1.w;
			float3 muscle = PI * sampleSigned3(idx, st);
			muscle = !(maskSign & uint3(1,2,4)) ? 0 : maskSign & 8 ? -muscle : muscle;
			rot = mul(rot, muscleToRotation(muscle));
		}
		float3x4 mat;
		mat.c0 = rot.c0;
		mat.c1 = rot.c1;
		mat.c2 = rot.c2;
		mat.c3 = pos;

		vertex += mul(mat, vtx);
		normal += mul(mat, nml);
		tangent += mul(mat, tng);
	}

	{
		float4 wts = float4(0.2, 0, 0.8, 0);
		float3x3 mat = transpose(float3x3(normal, tangent, cross(normalize(normal), tangent)));
		UNITY_LOOP
		for(uint J=0; J<4; J++) {
			uint shape;
			float3 dvertex;
			i.GetShape(J, shape, dvertex);
			if(shape > 0) {
				float wt = sampleSigned(shape, st);
				vertex += mul(mat, dvertex) * wt;
			}
		}
	}

	o.vertex = vertex;
	o.normal = normal;
	o.tangent.xyz = tangent;
	o.tangent.w = i.tangent.w;
	o.texcoord = i.GetUV();
}
