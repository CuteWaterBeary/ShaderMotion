#include "Rotation.hlsl"
#include "Codec.hlsl"

Texture2D _Armature;
Texture2D _Motion, _MotionDecoded;
static float _PositionScale = 2;

UNITY_INSTANCING_BUFFER_START(Props)
	UNITY_DEFINE_INSTANCED_PROP(float, _Id)
UNITY_INSTANCING_BUFFER_END(Props)

#define QUALITY 2
struct VertInputPlayer {
	float4 uvSkin[QUALITY][2] : TEXCOORD0;
	float4 uvShape[4] : TEXCOORD4;
	float3 normal  : NORMAL;
	float3 tangent : TANGENT;
	UNITY_VERTEX_INPUT_INSTANCE_ID
	void GetSkin(uint idx, out uint bone, out float4 vertex, out float3 normal, out float3 tangent) {
		bone = floor(uvSkin[idx][0].w);
		vertex = float4(uvSkin[idx][0].xyz, frac(uvSkin[idx][0].w)*2);
		normal = uvSkin[idx][1].xyz;
		tangent = idx == 0 ? this.normal : this.tangent;
	}
	void GetShape(uint idx, out uint shape, out float3 dvertex) {
		shape = uvShape[idx].w;
		dvertex = uvShape[idx].xyz;
	}
	float2 GetUV() {
		return float2(uvSkin[0][1].w, uvSkin[1][1].w);
	}
};


float sampleSigned(uint idx) {
	float4 rect = LocateSlot(idx);
	float id = UNITY_ACCESS_INSTANCED_PROP(Props, _Id);
	if(id != 0)
		rect.xz = 1-rect.xz;
#ifdef _ALPHAPREMULTIPLY_ON
	return SampleSlot(_MotionDecoded, rect);
#else
	return SampleSlot_DecodeSigned(_Motion, rect);
#endif
}
float sampleSigned(uint idx0, uint idx1) {
	float4 rect0 = LocateSlot(idx0);
	float4 rect1 = LocateSlot(idx1);
	float id = UNITY_ACCESS_INSTANCED_PROP(Props, _Id);
	if(id != 0)
		rect0.xz = 1-rect0.xz, rect1.xz = 1-rect1.xz;
#ifdef _ALPHAPREMULTIPLY_ON
	return SampleSlot_MergeSigned(_MotionDecoded, rect0, rect1);
#else
	return SampleSlot_DecodeSigned(_Motion, rect0, rect1);
#endif
}

float3 sampleSigned3(uint idx) {
	float o[3] = {0,0,0};
	UNITY_LOOP
	for(uint K=0; K<3; K++)
		o[K] = sampleSigned(idx+K);
	return float3(o[0], o[1], o[2]);
}
float3 sampleSigned3(uint idx0, uint idx1) {
	float o[3] = {0,0,0};
	UNITY_LOOP
	for(uint K=0; K<3; K++)
		o[K] = sampleSigned(idx0+K, idx1+K);
	return float3(o[0], o[1], o[2]);
}
float3x3 rotationYZ(float3 c1, float3 c2) {
	float3x3 rot;
	rot.c1 = c1 = normalize(c1);
	rot.c2 = c2 = normalize(c2 - dot(c2, c1) * c1);
	rot.c0 = cross(c1, c2);
	return rot;
}
void SkinVertex(VertInputPlayer i, out float3 vertex, out float3 normal) {
	vertex = normal = 0;
	float3 tangent = 0;
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

			float3 offset = _PositionScale * sampleSigned3(idx, idx+3);
			pos += offset * data0.y;
			rot  = mulEulerYXZ(rot, data1.xyz);

			float3 c1 = sampleSigned3(idx+6);
			float3 c2 = sampleSigned3(idx+9);
			rot = mul(rot, rotationYZ(c1, c2));
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
			float3 muscle = PI * sampleSigned3(idx);
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
		float3x3 mat = transpose(float3x3(normal, tangent, cross(normal, tangent)));
		UNITY_LOOP
		for(uint J=0; J<4; J++) {
			uint shape;
			float3 dvertex;
			i.GetShape(J, shape, dvertex);
			if(shape > 0) {
				float wt = sampleSigned(shape);
				// wt = shape == 82;
				vertex += mul(mat, dvertex) * wt;
			}
		}
	}
}
