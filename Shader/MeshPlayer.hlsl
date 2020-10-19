#include "Rotation.hlsl"
#include "Codec.hlsl"
#include "VideoLayout.hlsl"

// helper functions to enforce column-major
// because compiler treats float3x3 like row-major when used across for loop
float3 mad3(float3x3 A, float3 B, float3 C) {
	return C + A.c0 * B[0] + A.c1 * B[1] + A.c2 * B[2];
}
float3x3 get3x3(float3 c[3]) {
	return transpose(float3x3(c[0],c[1],c[2]));
}
void set3x3(out float3 c[3], float3x3 m) {
	c[0] = m.c0, c[1] = m.c1, c[2] = m.c2;
}

float _RotationTolerance;
static const float _PositionScale = 2;

Texture2D _Armature;
float4 sampleArmature(uint2 uv) {
	return _Armature.Load(uint3(uv, 0));
}

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

#define QUALITY 2
struct VertInputPlayer {
	// float3 vertex : POSITION;
	float3 normal  : NORMAL;
	float4 tangent : TANGENT;
	float4 uvSkin[QUALITY][2] : TEXCOORD0;
	float4 uvShape[4] : TEXCOORD4;
	UNITY_VERTEX_INPUT_INSTANCE_ID
	void GetSkin(uint idx, out uint bone, out float4 vertex, out float3 normal, out float3 tangent) {
		#if defined(UNITY_COMPILER_HLSLCC) // avoid compiler bugs on dynamic indexing
			float4 data0 = idx == 0 ? uvSkin[0][0] : uvSkin[1][0];
			float4 data1 = idx == 0 ? uvSkin[0][1] : uvSkin[1][1];
		#else
			float4 data0 = uvSkin[idx][0], data1 = uvSkin[idx][1];
		#endif
		bone = floor(data0.w);
		vertex = float4(data0.xyz, frac(data0.w)*2);
		normal = data1.xyz;
		tangent = idx == 0 ? this.normal : this.tangent.xyz;
	}
	void GetShape(uint idx, out uint shape, out float3 dvertex) {
		#if defined(UNITY_COMPILER_HLSLCC) // avoid compiler bugs on dynamic indexing
			float4 data = idx == 0 ? uvShape[0] : idx == 1 ? uvShape[1] : idx == 2 ? uvShape[2] : uvShape[3];
		#else
			float4 data = uvShape[idx];
		#endif
		shape = data.w;
		dvertex = data.xyz;
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
void SkinVertex(VertInputPlayer i, out VertInputSkinned o, uint layer, bool highRange=true) {
	float4 st = float4(1, 1, layer/2 * layerRect.z, 0);
	if(layer & 1)
		st.xz = float2(0, 1) - st.xz;

	float3 vertex = 0, normal = 0, tangent = 0;
	for(uint J=0; J<QUALITY; J++) {
		uint bone; float4 vtx; float3 nml, tng;
		i.GetSkin(J, bone, vtx, nml, tng);
		if(vtx.w < 1e-5)
			break;

		float3 pos, matc[3];
		{
			float4 data0 = sampleArmature(uint2(0, bone));
			float3 motion[4];
			{
				uint idx = data0.w;
				UNITY_LOOP
				for(int I=0; I<4; I++)
					motion[I] = sampleSnorm3(idx+3*I, st);
				if(highRange)
					motion[1] = mergeSnorm3(motion[0],motion[1]);
			}

			pos = motion[1];
			pos *= _PositionScale;
			float err = orthogonalize(motion[2], motion[3], matc[1], matc[2]);
			if(err + pow(max(length(matc[1]), length(matc[2])) - 1, 2) > _RotationTolerance * _RotationTolerance) {
				vertex = sqrt(-unity_ObjectToWorld._44); //NaN
				break;
			}
			float rlen2 = rsqrt(dot(matc[2],matc[2]));
			matc[1] *= rlen2 / data0.y;
			matc[2] *= rlen2 * length(matc[1]);
			matc[0] = cross(normalize(matc[1]), matc[2]);
		}
		for(uint I=2; I<30; I+=2) {
			float4 data0 = sampleArmature(uint2(I+0, bone));
			float4 data1 = sampleArmature(uint2(I+1, bone));
			if(data0.w < 0)
				break;

			float3x3 mat = get3x3(matc);
			pos = mad3(mat, data0, pos);
			mat = mulEulerYXZ(mat, data1.xyz);
			{
				uint idx = data0.w, maskSign = data1.w;
				float3 swingTwist = PI * sampleSnorm3(idx, st);
				swingTwist = !(maskSign & uint3(1,2,4)) ? 0 : maskSign & 8 ? -swingTwist : swingTwist;
				mat = mul(mat, fromSwingTwist(swingTwist));
			}
			set3x3(matc, mat);
		}
		vertex  = mad3(get3x3(matc), vtx.xyz, vertex ) + pos * vtx.w;
		normal  = mad3(get3x3(matc), nml.xyz, normal );
		tangent = mad3(get3x3(matc), tng.xyz, tangent);
	}
#if !defined(SHADER_API_MOBILE)
	{
		float3x3 mat = transpose(float3x3(normal, tangent, cross(normalize(normal), tangent)));
		UNITY_LOOP
		for(uint J=0; J<4; J++) {
			uint shape;
			float3 dvertex;
			i.GetShape(J, shape, dvertex);
			if(shape > 0) {
				float wt = sampleSnorm(shape, st);
				vertex += mul(mat, dvertex) * wt;
			}
		}
	}
#endif
	o.vertex = vertex;
	o.normal = normal;
	o.tangent.xyz = tangent;
	o.tangent.w = i.tangent.w;
	o.texcoord = i.GetUV();
}
