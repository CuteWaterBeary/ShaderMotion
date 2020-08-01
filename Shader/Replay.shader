Shader "Motion/Replay" {
Properties {
	[Header(Texture)]
	[NoScaleOffset]
	_MainTex ("Albedo", 2D) = "white" {}
	_Color ("Color", Color) = (1,1,1,1)

	[Header(Clipping)]
	[Enum(UnityEngine.Rendering.CullMode)] _Cull("Face Culling", Float) = 2
	[Toggle(_ALPHATEST_ON)] _AlphaTest("Alpha Test", Float) = 0
	_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0
	_NearClip ("NearClip", Float) = 0

	[Header(Motion)]
	[NoScaleOffset]_Armature ("_Armature", 2D) = "black" {}
	[Toggle(_ALPHAPREMULTIPLY_ON)] _Decoded ("Decoded", Float) = 0
	_Id ("Id", Float) = 0
}
SubShader {
	Tags { "Queue"="Geometry" "RenderType"="Opaque" }
	Pass {
		Tags { "LightMode"="ForwardBase" }
		Cull Off
CGPROGRAM
#pragma exclude_renderers gles
#pragma target 5.0
#pragma vertex vert
#pragma fragment frag
#pragma shader_feature _ALPHATEST_ON
#pragma multi_compile _ _ALPHAPREMULTIPLY_ON
#pragma multi_compile_instancing
#include <UnityCG.cginc>
#include <Lighting.cginc>
#include "Rotation.hlsl"
#include "Codec.hlsl"
#include "Frag.hlsl"

Texture2D _Armature;
Texture2D _Motion, _MotionDecoded;
static float _PositionLimit = 2;

UNITY_INSTANCING_BUFFER_START(Props)
	UNITY_DEFINE_INSTANCED_PROP(float, _Id)
UNITY_INSTANCING_BUFFER_END(Props)

#define QUALITY 2
struct VertInput {
	float4 uvSkin[QUALITY][2] : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
	void GetSkin(uint idx, out uint bone, out float4 vertex, out float3 normal) {
		bone = floor(uvSkin[idx][0].w);
		vertex = float4(uvSkin[idx][0].xyz, frac(uvSkin[idx][0].w)*2);
		normal = uvSkin[idx][1].xyz;
	}
	float2 GetUV() {
		return float2(uvSkin[0][1].w, uvSkin[1][1].w);
	}
};


float sampleUnorm(uint idx) {
	float4 rect = GetRect(idx);
	float id = UNITY_ACCESS_INSTANCED_PROP(Props, _Id);
	if(id != 0)
		rect.xz = 1-rect.xz;
#ifdef _ALPHAPREMULTIPLY_ON
	return SampleUnorm(_MotionDecoded, rect);
#else
	return SampleUnormDecode(_Motion, rect);
#endif
}

float3 sampleUnorm3(uint idx) {
	float o[3] = {0,0,0};
	UNITY_LOOP
	for(uint K=0; K<3; K++)
		o[K] = sampleUnorm(idx+K);
	return float3(o[0], o[1], o[2]);
}
float3x3 rotationYZ(float3 c1, float3 c2) {
	float3x3 rot;
	rot.c1 = c1 = normalize(c1);
	rot.c2 = c2 = normalize(c2 - dot(c2, c1) * c1);
	rot.c0 = cross(c1, c2);
	return rot;
}
void vert(VertInput i, out FragInput o) {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	
	float3 vertex = 0;
	float3 normal = 0;
	uint base = 0;

	float4 color = 1;

	for(uint J=0; J<QUALITY; J++) {
		uint bone; float4 vtx; float3 nml;
		i.GetSkin(J, bone, vtx, nml);
		if(vtx.w < 1e-5)
			break;

		float3 pos = 0;
		float3x3 rot = float3x3(1,0,0, 0,1,0, 0,0,1);
		{
			float4 data0 = _Armature.Load(uint3(0, bone, 0));
			float4 data1 = _Armature.Load(uint3(1, bone, 0));
			uint idx = data0.w;

			float3 offset = lerp(-_PositionLimit, _PositionLimit, sampleUnorm3(base+idx+0));
			pos += offset * data0.y;
			rot  = mulEulerYXZ(rot, data1.xyz);

			float3 c1 = lerp(-1, 1, sampleUnorm3(base+idx+3));
			float3 c2 = lerp(-1, 1, sampleUnorm3(base+idx+6));
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
			float3 muscle = lerp(-PI, PI, sampleUnorm3(base+idx));
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
	}
	
	
	o.vertex = mul(unity_ObjectToWorld, float4(vertex, 1));
	o.normal = mul(unity_ObjectToWorld, float4(normal, 0));
	o.pos = UnityWorldToClipPos(o.vertex);
	o.tex = i.GetUV();
	o.color = color;
}
ENDCG
	}
}
}